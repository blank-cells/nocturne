using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.Infrastructure.Data.Extensions;
using Nocturne.Infrastructure.Data.Interceptors;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nocturne.Infrastructure.Data.Tests.Rls;

/// <summary>
/// Shared xUnit fixture for Row Level Security integration tests. Spins up a
/// PostgreSQL container with the canonical docs/postgres/container-init/00-init.sh bind-mounted
/// into /docker-entrypoint-initdb.d/ so the nocturne_migrator and nocturne_app
/// roles are created during image init, then runs EF migrations under the
/// migrator role.
///
/// Exposes two connection strings (app + migrator) and two seeded tenant ids
/// so tests can assert RLS behavior under each role.
/// </summary>
public class RlsTestFixture : IAsyncLifetime
{
    private const string DbName = "nocturne_rls_test";
    private const string BootstrapUser = "postgres";
    private const string BootstrapPassword = "bootstrap-test-password";
    private const string MigratorPassword = "rls-test-migrator-password";
    private const string AppPassword = "rls-test-app-password";

    private PostgreSqlContainer? _container;

    public string AppConnectionString { get; private set; } = string.Empty;
    public string MigratorConnectionString { get; private set; } = string.Empty;

    public Guid TenantAId { get; } = Guid.NewGuid();
    public Guid TenantBId { get; } = Guid.NewGuid();

    public int TenantAEntryCount { get; private set; }
    public int TenantBEntryCount { get; private set; }

    public async Task InitializeAsync()
    {
        var initScriptPath = ResolveInitScriptPath();

        _container = new PostgreSqlBuilder()
            .WithImage("postgres:17.6")
            .WithDatabase(DbName)
            .WithUsername(BootstrapUser)
            .WithPassword(BootstrapPassword)
            .WithEnvironment("NOCTURNE_MIGRATOR_PASSWORD", MigratorPassword)
            .WithEnvironment("NOCTURNE_APP_PASSWORD", AppPassword)
            .WithBindMount(initScriptPath, "/docker-entrypoint-initdb.d/00-init.sh")
            .Build();

        await _container.StartAsync();

        var host = _container.Hostname;
        var port = _container.GetMappedPublicPort(5432);

        MigratorConnectionString =
            $"Host={host};Port={port};Database={DbName};Username=nocturne_migrator;Password={MigratorPassword}";
        AppConnectionString =
            $"Host={host};Port={port};Database={DbName};Username=nocturne_app;Password={AppPassword}";

        // Run EF Core migrations under the migrator role. This uses the same
        // code path the API's Program.cs uses at startup.
        await DatabaseInitializationExtensions.RunMigrationsAsync(
            MigratorConnectionString,
            NullLogger.Instance,
            new TenantConnectionInterceptor());

        await SeedAsync();
    }

    public async Task DisposeAsync()
    {
        if (_container is not null)
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }
    }

    /// <summary>
    /// Opens a NpgsqlConnection under the nocturne_app role.
    /// </summary>
    public async Task<NpgsqlConnection> OpenAppConnectionAsync()
    {
        var conn = new NpgsqlConnection(AppConnectionString);
        await conn.OpenAsync();
        return conn;
    }

    /// <summary>
    /// Sets the per-connection tenant GUC the same way TenantConnectionInterceptor does.
    /// </summary>
    public static async Task SetTenantAsync(NpgsqlConnection connection, Guid tenantId)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT set_config('app.current_tenant_id', @tenant, false)";
        var p = cmd.CreateParameter();
        p.ParameterName = "tenant";
        p.Value = tenantId.ToString();
        cmd.Parameters.Add(p);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SeedAsync()
    {
        // Seed as the migrator role. Even though migrator owns the tables,
        // FORCE RLS means INSERTs must satisfy the WITH CHECK tenant_id policy,
        // so app.current_tenant_id still has to be set before inserting rows.
        await using var conn = new NpgsqlConnection(MigratorConnectionString);
        await conn.OpenAsync();

        await ExecAsync(conn,
            "INSERT INTO tenants (id, slug, display_name, is_active, is_default, timezone, quiet_hours_override_critical, allow_access_requests, sys_created_at, sys_updated_at) " +
            "VALUES (@id, @slug, @name, true, false, 'UTC', true, true, now(), now())",
            ("id", TenantAId), ("slug", "rls-tenant-a"), ("name", "RLS Tenant A"));

        await ExecAsync(conn,
            "INSERT INTO tenants (id, slug, display_name, is_active, is_default, timezone, quiet_hours_override_critical, allow_access_requests, sys_created_at, sys_updated_at) " +
            "VALUES (@id, @slug, @name, true, false, 'UTC', true, true, now(), now())",
            ("id", TenantBId), ("slug", "rls-tenant-b"), ("name", "RLS Tenant B"));

        TenantAEntryCount = await SeedEntriesAsync(conn, TenantAId, 3);
        TenantBEntryCount = await SeedEntriesAsync(conn, TenantBId, 2);
    }

    private static async Task<int> SeedEntriesAsync(NpgsqlConnection conn, Guid tenantId, int count)
    {
        // Set the tenant GUC so FORCE RLS policies accept the INSERTs.
        await SetTenantAsync(conn, tenantId);

        for (var i = 0; i < count; i++)
        {
            await ExecAsync(conn,
                "INSERT INTO entries (id, tenant_id, mills, mgdl, type, is_calibration, sys_created_at, sys_updated_at) " +
                "VALUES (@id, @tenant, @mills, @mgdl, 'sgv', false, now(), now())",
                ("id", Guid.NewGuid()),
                ("tenant", tenantId),
                ("mills", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() + i),
                ("mgdl", 120.0 + i));
        }

        await ExecAsync(conn, "SELECT set_config('app.current_tenant_id', '', false)");
        return count;
    }

    private static async Task ExecAsync(
        NpgsqlConnection conn,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value;
            cmd.Parameters.Add(p);
        }
        await cmd.ExecuteNonQueryAsync();
    }

    private static string ResolveInitScriptPath()
    {
        // Walk up from the test assembly's base directory to the solution
        // root so we can point Testcontainers at the canonical init script
        // that ships with the repo.
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "docs", "postgres", "container-init", "00-init.sh")))
        {
            dir = dir.Parent;
        }

        if (dir is null)
        {
            throw new InvalidOperationException(
                "Could not locate docs/postgres/container-init/00-init.sh by walking up from " + AppContext.BaseDirectory);
        }

        return Path.Combine(dir.FullName, "docs", "postgres", "container-init", "00-init.sh");
    }
}

[CollectionDefinition("RLS integration")]
public class RlsTestCollection : ICollectionFixture<RlsTestFixture>
{
}
