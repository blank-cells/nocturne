using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Nocturne.API.Configuration;
using Nocturne.API.Extensions;
using Nocturne.API.Hubs;
using Nocturne.API.Middleware;
using Nocturne.API.Multitenancy;
using OpenApi.Remote.Processors;
using Nocturne.Core.Constants;
using Nocturne.Core.Models.Configuration;
using Nocturne.Infrastructure.Cache.Extensions;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Infrastructure.Data.Extensions;
using OpenTelemetry.Logs;
using FluentValidation;
using FluentValidation.AspNetCore;
using Scalar.AspNetCore;
using JwtOptions = Nocturne.Core.Models.Configuration.JwtOptions;

var builder = WebApplication.CreateBuilder(args);

// Try to find appsettings.json in solution root first, fallback to current directory
var configPath = Directory.GetCurrentDirectory();
var solutionRoot = Path.GetFullPath(Path.Combine(configPath, "..", "..", ".."));

if (File.Exists(Path.Combine(solutionRoot, "appsettings.json")))
{
    // Local development - use solution root
    builder.Environment.ContentRootPath = solutionRoot;
    configPath = solutionRoot;
}

// else: Docker or other deployment - use current directory (where files are copied)

var appsettingsPath = Path.Combine(configPath, "appsettings.json");
if (!File.Exists(appsettingsPath))
{
    var examplePath = Path.Combine(configPath, "appsettings.example.json");
    Console.Error.WriteLine("ERROR: appsettings.json not found.");
    Console.Error.WriteLine($"  Expected at: {appsettingsPath}");
    if (File.Exists(examplePath))
        Console.Error.WriteLine($"  Copy the example file to get started: cp appsettings.example.json appsettings.json");
    else
        Console.Error.WriteLine("  No example file found either. Check your repository setup.");
    Environment.Exit(1);
}

builder.Configuration.SetBasePath(configPath);

// Add additional configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddJsonFile(
    $"appsettings.{builder.Environment.EnvironmentName}.json",
    optional: true,
    reloadOnChange: true
);

// Ensure environment variables (injected by Aspire) take precedence over appsettings.json
builder.Configuration.AddEnvironmentVariables();

if (string.IsNullOrEmpty(builder.Configuration["NocturneApiUrl"]))
{
    var baseUrl = builder.Configuration["BaseUrl"];
    if (!string.IsNullOrEmpty(baseUrl))
    {
        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?> { ["NocturneApiUrl"] = baseUrl }
        );
    }
}

// Configure Kestrel to allow larger request bodies for analytics endpoints
// 90 days of demo data can exceed the 30MB default limit
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

builder.AddServiceDefaults();

builder.Host.UseDefaultServiceProvider(options =>
{
    options.ValidateScopes = builder.Environment.IsDevelopment();
    options.ValidateOnBuild = builder.Environment.IsDevelopment();
});

// Configure PostgreSQL database
var aspirePostgreSqlConnection = builder.Configuration.GetConnectionString(ServiceNames.PostgreSql);

if (string.IsNullOrWhiteSpace(aspirePostgreSqlConnection))
{
    throw new InvalidOperationException(
        $"PostgreSQL connection string '{ServiceNames.PostgreSql}' not found. Ensure Aspire is properly configured."
    );
}

builder.Services.AddPostgreSqlInfrastructure(
    aspirePostgreSqlConnection,
    config =>
    {
        config.EnableDetailedErrors = builder.Environment.IsDevelopment();
        config.EnableSensitiveDataLogging = builder.Environment.IsDevelopment();
    }
);

builder.Services.AddDiscrepancyAnalysisRepository();
builder.Services.AddAlertRepositories();

builder.Services.AddDataProtection();

// Add compatibility proxy services
builder.Services.AddCompatibilityProxyServices(builder.Configuration);

// Use in-memory cache for single-user deployments
builder.Services.AddNocturneMemoryCache();

builder.Logging.ClearProviders();
builder.Logging.AddOpenTelemetry(logging => logging.AddConsoleExporter());

var loopApnsKeyId = builder.Configuration["Loop:ApnsKeyId"];
Console.WriteLine(
    $"Loop configuration loaded - APNS Key ID: {(string.IsNullOrEmpty(loopApnsKeyId) ? "Not configured" : $"{loopApnsKeyId[..Math.Min(4, loopApnsKeyId.Length)]}****")}"
);

// Add response caching for GET endpoints
builder.Services.AddResponseCaching();

builder.Services.AddHttpContextAccessor();

// Add native API services for strangler pattern
// Note: NightscoutJsonFilter is added globally to apply null-omission and
// NocturneOnly field exclusion to v1-v3 API responses only
builder.Services.AddControllers(options =>
{
    options.Filters.Add<NightscoutJsonFilter>();
});
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();

// Note: Using NSwag instead of Microsoft.AspNetCore.OpenApi for better compatibility
builder.Services.AddOpenApi();

// Add OpenAPI document generation with NSwag
builder.Services.AddOpenApiDocument(config =>
{
    // Add remote function metadata processor
    config.OperationProcessors.Add(new RemoteFunctionOperationProcessor());

    config.PostProcess = document =>
    {
        document.Info.Version = "v1";
        document.Info.Title = "Nocturne API";
        document.Info.Description = "Modern C# rewrite of Nightscout API with 1:1 compatibility";
        document.Info.Contact = new NSwag.OpenApiContact
        {
            Name = "Nocturne API",
            Url = "https://github.com/ryceg/nocturne",
        };
        document.Info.License = new NSwag.OpenApiLicense
        {
            Name = "Use under LICX",
            Url = "https://example.com/license",
        };
    };
});

// ── Service registration (grouped by concern) ──────────────────────────
builder.Services.AddApiCoreServices(builder.Configuration);
builder.Services.AddAuthenticationAndIdentity(builder.Configuration);
builder.Services.AddDomainServices();
builder.Services.AddV4Infrastructure();
builder.Services.AddRealTimeAndNotifications(builder.Configuration);
builder.Services.AddAlertingAndMonitoring(builder.Configuration);
builder.Services.AddConnectorInfrastructure(builder.Configuration);
builder.Services.AddMigrationServices();


// Configure JWT authentication - derive signing key from instance key
var secretKey =
    builder.Configuration[$"Parameters:{ServiceNames.Parameters.InstanceKey}"]
    ?? builder.Configuration[ServiceNames.ConfigKeys.InstanceKey]
    ?? throw new InvalidOperationException("Instance key must be configured for JWT signing. Set Parameters:instance-key or INSTANCE_KEY.");
var key = Encoding.UTF8.GetBytes(secretKey);

builder
    .Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

// Configure CORS for frontend with credentials support
// Note: AllowAnyOrigin() cannot be combined with AllowCredentials() per CORS spec
// Using SetIsOriginAllowed to dynamically allow origins while supporting cookies
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true) // Allow any origin (development-friendly, restrict in production)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials(); // Required for cookies/auth to work cross-origin
    });
});

var app = builder.Build();

// Configure middleware pipeline
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseResponseCaching();
app.UseCors();

// Add JSON extension middleware to handle .json suffixes for legacy compatibility
app.UseMiddleware<JsonExtensionMiddleware>();

// Block most API traffic when recovery mode is active (orphaned subjects detected)
app.UseMiddleware<RecoveryModeMiddleware>();

// Resolve tenant from subdomain (must run before authentication)
app.UseMiddleware<TenantResolutionMiddleware>();

// Block API traffic for freshly provisioned tenants with no passkey credentials
app.UseMiddleware<TenantSetupMiddleware>();

// Add Nightscout authentication middleware
app.UseMiddleware<AuthenticationMiddleware>();

// Add member scope middleware (resolves membership role and restricts follower scopes)
app.UseMiddleware<MemberScopeMiddleware>();

// Add site security middleware (enforces authentication when site lockdown is enabled)
app.UseMiddleware<SiteSecurityMiddleware>();

// Add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// Add rate limiting
app.UseRateLimiter();

// Add compatibility proxy middleware (background comparison against Nightscout for v1/v2/v3 GET requests)
app.UseMiddleware<CompatibilityProxyMiddleware>();

// Map native API controllers
app.MapControllers();

// Map SignalR hubs for real-time communication
app.MapHub<DataHub>("/hubs/data");
app.MapHub<AlarmHub>("/hubs/alarms");
app.MapHub<AlertHub>("/hubs/alerts");
app.MapHub<ConfigHub>("/hubs/config");

// Note: Using NSwag instead of Microsoft.AspNetCore.OpenApi for better compatibility
// OpenAPI documents are served at /openapi/{documentName}.json
app.MapOpenApi();

// Scalar API Reference provides interactive API documentation at /scalar
app.MapScalarApiReference(options =>
{
    options.WithTitle("Nocturne API Documentation");
    options.WithTheme(Scalar.AspNetCore.ScalarTheme.Mars);
});

// Add root endpoint to serve a basic info page
app.MapGet(
    "/",
    async (IEntryRepository entryRepository) =>
    {
        // Check database connection by fetching the latest entry
        string databaseStatus = "unknown";
        object? latestEntry = null;

        try
        {
            var entry = await entryRepository.GetCurrentEntryAsync();

            if (entry != null)
            {
                databaseStatus = "connected";
                latestEntry = new
                {
                    date = entry.Date,
                    dateString = entry.DateString,
                    sgv = entry.Sgv,
                    mbg = entry.Mbg,
                    direction = entry.Direction,
                };
            }
            else
            {
                databaseStatus = "connected_no_data";
            }
        }
        catch (Exception)
        {
            databaseStatus = "disconnected";
        }

        return Results.Json(
            new
            {
                name = "Nocturne API",
                version = "1.0.0",
                description = "Modern C# rewrite of Nightscout API",
                api_documentation = "/openapi/v1.json",
                aspire_dashboard_note = "API documentation is available via Scalar in the Aspire dashboard",
                database_status = databaseStatus,
                latest_entry = latestEntry,
                endpoints = new
                {
                    status = "/api/v1/status",
                    entries = "/api/v1/entries",
                    treatments = "/api/v1/treatments",
                    profile = "/api/v1/profile",
                    versions = "/api/versions",
                },
            }
        );
    }
);

app.MapDefaultEndpoints();

// Skip database migrations when running in NSwag/OpenAPI generation mode
// NSwag launches the app to extract the OpenAPI schema, but we don't need DB access for that
var isNSwagGeneration = IsRunningInNSwagContext();
if (!isNSwagGeneration && !app.Environment.IsEnvironment("Testing"))
{
    // Initialize PostgreSQL database with migrations
    // IMPORTANT: Do not catch exceptions here - if migrations fail, the app should not start
    // This ensures the database schema is always in a valid state before handling requests
    Console.WriteLine("Running PostgreSQL database migrations...");
    await app.Services.MigrateDatabaseAsync();
    Console.WriteLine("PostgreSQL database migrations completed successfully.");

    // Seed default tenant if none exists and backfill tenant_id on existing rows
    await DefaultTenantSeeder.SeedDefaultTenantAsync(app.Services);
}
else if (isNSwagGeneration)
{
    Console.WriteLine("[NSwag] Skipping database migrations - running in OpenAPI generation mode");
}

app.Run();

// Detects if the application is being run by NSwag for OpenAPI document generation.
// NSwag uses its AspNetCore.Launcher to load and introspect the app without actually running it.
static bool IsRunningInNSwagContext()
{
    // Check if the entry assembly is the NSwag launcher
    var entryAssembly = System.Reflection.Assembly.GetEntryAssembly();
    if (
        entryAssembly?.GetName().Name?.Contains("NSwag", StringComparison.OrdinalIgnoreCase) == true
    )
    {
        return true;
    }

    // Check command line for NSwag invocation (covers dotnet exec scenarios)
    var commandLine = Environment.CommandLine;
    if (
        commandLine.Contains("NSwag", StringComparison.OrdinalIgnoreCase)
        || commandLine.Contains("nswag", StringComparison.OrdinalIgnoreCase)
    )
    {
        return true;
    }

    return false;
}

// Make Program accessible for testing
namespace Nocturne.API
{
    public partial class Program { }
}

// NSwag 14.x discovers the host via reflection on the entry-point type's DeclaringType.
// .NET 10.0.104 compiles top-level statements into a global "Program" class (not Nocturne.API.Program),
// so this partial must be in the global namespace for NSwag to find CreateHostBuilder.
public partial class Program
{
    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(webBuilder =>
            {
                webBuilder.UseStartup<NSwagStartup>();
            });
}

/// <summary>Minimal startup used only by NSwag for OpenAPI schema extraction.</summary>
internal class NSwagStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers()
            .AddApplicationPart(typeof(Nocturne.API.Program).Assembly);
        services.AddOpenApiDocument(config =>
        {
            config.OperationProcessors.Add(new RemoteFunctionOperationProcessor());
        });
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
    }
}
