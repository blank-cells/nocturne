# Per-Tenant Recovery Mode Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make recovery mode per-tenant in multi-tenant deployments so one tenant's orphaned subjects don't lock down the entire instance.

**Architecture:** In multi-tenant mode, the global `RecoveryModeCheckService` and `RecoveryModeMiddleware` become no-ops. All setup/recovery gating moves into `TenantSetupMiddleware`, which runs after tenant resolution and checks the resolved tenant's state via two sequential DB queries. Single-tenant behaviour and the `NOCTURNE_RECOVERY_MODE` env var override are unchanged.

**Tech Stack:** ASP.NET Core 10 middleware, EF Core 10 (InMemory/Sqlite for tests), xUnit + FluentAssertions + Moq, `ITenantAccessor`, `NocturneDbContext`

---

### Task 1: `RecoveryModeCheckService` — skip orphaned-subject scan in multi-tenant mode

**Files:**
- Modify: `src/API/Nocturne.API/Services/Auth/RecoveryModeCheckService.cs:86-105`
- Test: `tests/Unit/Nocturne.API.Tests/Services/Auth/RecoveryModeCheckServiceTests.cs`

**Step 1: Write the failing test**

Add this test to `RecoveryModeCheckServiceTests`:

```csharp
[Fact]
[Trait("Category", "Unit")]
public async Task StartAsync_OrphanedSubject_WithMultitenancy_SkipsRecoveryMode()
{
    _dbContext.Subjects.Add(new SubjectEntity
    {
        Id = Guid.CreateVersion7(),
        Name = "Orphaned User",
        IsActive = true,
        IsSystemSubject = false,
        OidcSubjectId = null,
    });
    await _dbContext.SaveChangesAsync();

    var config = new MultitenancyConfiguration { BaseDomain = "nocturnecgm.com" };
    var service = CreateService(config);

    await service.StartAsync(CancellationToken.None);

    _state.IsEnabled.Should().BeFalse("multi-tenant mode should not set global recovery");
}
```

**Step 2: Run to confirm it fails**

```
cd C:\Users\rhysg\Documents\Github\nocturne-cloud\nocturne
dotnet test tests/Unit/Nocturne.API.Tests -v minimal --filter "FullyQualifiedName~RecoveryModeCheckServiceTests.StartAsync_OrphanedSubject_WithMultitenancy"
```

Expected: FAIL — current code sets `IsEnabled = true` regardless of multi-tenancy.

**Step 3: Implement the fix**

In `RecoveryModeCheckService.cs`, wrap the orphaned-subject scan (lines 88-105) with the existing `IsMultiTenantMode` check. Find:

```csharp
        var hasOrphaned = await db.Subjects
            .IgnoreQueryFilters()
            .Where(s => s.IsActive && !s.IsSystemSubject)
            .Where(s =>
                s.OidcSubjectId == null &&
                !db.PasskeyCredentials
                    .IgnoreQueryFilters()
                    .Any(p => p.SubjectId == s.Id)
            )
            .AnyAsync(cancellationToken);

        if (hasOrphaned)
        {
            _state.IsEnabled = true;
            _logger.LogWarning(
                "Recovery mode enabled: one or more active subjects have no passkey and no OIDC binding"
            );
        }
```

Replace with:

```csharp
        if (IsMultiTenantMode)
        {
            _logger.LogInformation(
                "Multi-tenant mode — per-tenant recovery handled by TenantSetupMiddleware"
            );
            return;
        }

        var hasOrphaned = await db.Subjects
            .IgnoreQueryFilters()
            .Where(s => s.IsActive && !s.IsSystemSubject)
            .Where(s =>
                s.OidcSubjectId == null &&
                !db.PasskeyCredentials
                    .IgnoreQueryFilters()
                    .Any(p => p.SubjectId == s.Id)
            )
            .AnyAsync(cancellationToken);

        if (hasOrphaned)
        {
            _state.IsEnabled = true;
            _logger.LogWarning(
                "Recovery mode enabled: one or more active subjects have no passkey and no OIDC binding"
            );
        }
```

**Step 4: Run tests**

```
dotnet test tests/Unit/Nocturne.API.Tests -v minimal --filter "FullyQualifiedName~RecoveryModeCheckServiceTests"
```

Expected: all pass.

**Step 5: Commit**

```bash
git add src/API/Nocturne.API/Services/Auth/RecoveryModeCheckService.cs
git add tests/Unit/Nocturne.API.Tests/Services/Auth/RecoveryModeCheckServiceTests.cs
git commit -m "feat: skip global recovery mode scan in multi-tenant mode"
```

---

### Task 2: `RecoveryModeMiddleware` — no-op in multi-tenant mode

**Files:**
- Modify: `src/API/Nocturne.API/Middleware/RecoveryModeMiddleware.cs`
- Create: `tests/Unit/Nocturne.API.Tests/Middleware/RecoveryModeMiddlewareTests.cs`

**Step 1: Write the failing tests**

Create `tests/Unit/Nocturne.API.Tests/Middleware/RecoveryModeMiddlewareTests.cs`:

```csharp
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Nocturne.API.Middleware;
using Nocturne.API.Multitenancy;
using Nocturne.API.Services.Auth;
using Xunit;

namespace Nocturne.API.Tests.Middleware;

public class RecoveryModeMiddlewareTests
{
    private static RecoveryModeMiddleware Build(
        out DefaultHttpContext ctx,
        out bool nextCalled,
        string path = "/api/status")
    {
        var called = false;
        var mw = new RecoveryModeMiddleware(
            _ => { called = true; return Task.CompletedTask; },
            NullLogger<RecoveryModeMiddleware>.Instance);

        ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        nextCalled = false;

        // Capture by ref workaround
        return mw;
    }

    [Fact]
    public async Task MultiTenant_RecoveryEnabled_PassesThrough()
    {
        var state = new RecoveryModeState { IsEnabled = true };
        var config = Options.Create(new MultitenancyConfiguration { BaseDomain = "nocturnecgm.com" });

        var nextCalled = false;
        var mw = new RecoveryModeMiddleware(
            _ => { nextCalled = true; return Task.CompletedTask; },
            NullLogger<RecoveryModeMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/status";
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx, state, config);

        nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().NotBe(503);
    }

    [Fact]
    public async Task MultiTenant_EnvVarOverride_StillBlocks()
    {
        var state = new RecoveryModeState { IsEnabled = true };
        var config = Options.Create(new MultitenancyConfiguration { BaseDomain = "nocturnecgm.com" });

        Environment.SetEnvironmentVariable("NOCTURNE_RECOVERY_MODE", "true");
        try
        {
            var mw = new RecoveryModeMiddleware(
                _ => Task.CompletedTask,
                NullLogger<RecoveryModeMiddleware>.Instance);

            var ctx = new DefaultHttpContext();
            ctx.Request.Path = "/api/status";
            ctx.Response.Body = new MemoryStream();

            await mw.InvokeAsync(ctx, state, config);

            ctx.Response.StatusCode.Should().Be(503);
        }
        finally
        {
            Environment.SetEnvironmentVariable("NOCTURNE_RECOVERY_MODE", null);
        }
    }

    [Fact]
    public async Task SingleTenant_RecoveryEnabled_Blocks()
    {
        var state = new RecoveryModeState { IsEnabled = true };
        var config = Options.Create(new MultitenancyConfiguration());

        var mw = new RecoveryModeMiddleware(
            _ => Task.CompletedTask,
            NullLogger<RecoveryModeMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/status";
        ctx.Response.Body = new MemoryStream();

        await mw.InvokeAsync(ctx, state, config);

        ctx.Response.StatusCode.Should().Be(503);
    }
}
```

**Step 2: Run to confirm they fail**

```
dotnet test tests/Unit/Nocturne.API.Tests -v minimal --filter "FullyQualifiedName~RecoveryModeMiddlewareTests"
```

Expected: compile error — `InvokeAsync` doesn't accept `IOptions<MultitenancyConfiguration>`.

**Step 3: Implement the change**

Replace `RecoveryModeMiddleware.cs` entirely:

```csharp
using Microsoft.Extensions.Options;
using Nocturne.API.Multitenancy;
using Nocturne.API.Services.Auth;

namespace Nocturne.API.Middleware;

/// <summary>
/// Middleware that enforces recovery mode restrictions when active.
/// In multi-tenant mode, this middleware is a no-op — per-tenant recovery
/// is handled by TenantSetupMiddleware (which runs after tenant resolution).
/// In single-tenant mode, blocks API traffic when the global RecoveryModeState
/// is active, allowing only passkey/TOTP setup endpoints through.
/// The NOCTURNE_RECOVERY_MODE env var override bypasses the multi-tenant skip.
/// </summary>
public class RecoveryModeMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RecoveryModeMiddleware> _logger;

    public RecoveryModeMiddleware(
        RequestDelegate next,
        ILogger<RecoveryModeMiddleware> logger
    )
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        RecoveryModeState state,
        IOptions<MultitenancyConfiguration> multitenancyConfig)
    {
        if (!state.IsEnabled && !state.IsSetupRequired)
        {
            await _next(context);
            return;
        }

        // In multi-tenant mode, per-tenant recovery is handled by TenantSetupMiddleware.
        // Only the env var override still triggers the global gate.
        var isMultiTenant = !string.IsNullOrEmpty(multitenancyConfig.Value.BaseDomain);
        var envOverride = string.Equals(
            Environment.GetEnvironmentVariable("NOCTURNE_RECOVERY_MODE"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (isMultiTenant && !envOverride)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";

        // Allow passkey, TOTP, metadata, and slug validation endpoints
        if (path.StartsWith("/api/auth/passkey/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/auth/totp/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/api/metadata", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/admin/tenants/validate-slug", StringComparison.OrdinalIgnoreCase) ||
            path.Equals("/api/v4/me/tenants/validate-slug", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Block other API endpoints with a clear message
        if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            var mode = state.IsSetupRequired ? "setup" : "recovery";
            _logger.LogDebug("{Mode} mode: blocking request to {Path}", mode, path);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = state.IsSetupRequired ? "setup_required" : "recovery_mode_active",
                message = state.IsSetupRequired
                    ? "Initial setup required. Please register a passkey or authenticator app."
                    : "Instance is in recovery mode. Please register a passkey or authenticator app to continue.",
                setupRequired = state.IsSetupRequired,
                recoveryMode = state.IsEnabled,
            });
            return;
        }

        // Allow non-API requests (frontend assets, health checks, etc.)
        await _next(context);
    }
}
```

**Step 4: Run tests**

```
dotnet test tests/Unit/Nocturne.API.Tests -v minimal --filter "FullyQualifiedName~RecoveryModeMiddlewareTests"
```

Expected: all pass.

**Step 5: Commit**

```bash
git add src/API/Nocturne.API/Middleware/RecoveryModeMiddleware.cs
git add tests/Unit/Nocturne.API.Tests/Middleware/RecoveryModeMiddlewareTests.cs
git commit -m "feat: make RecoveryModeMiddleware a no-op in multi-tenant mode"
```

---

### Task 3: `TenantSetupMiddleware` — add per-tenant recovery detection

**Files:**
- Modify: `src/API/Nocturne.API/Middleware/TenantSetupMiddleware.cs`
- Modify: `tests/Unit/Nocturne.API.Tests/Middleware/TenantSetupMiddlewareTests.cs`

**Step 1: Write the failing tests**

Add these tests to `TenantSetupMiddlewareTests`:

```csharp
[Fact]
public async Task WhenTenantHasOrphanedSubject_Returns503WithRecoveryMode()
{
    // Arrange — tenant has a passkey (setup is done) but also an orphaned subject
    var healthySubjectId = Guid.CreateVersion7();
    var orphanedSubjectId = Guid.CreateVersion7();

    // Healthy subject with passkey
    _dbContext.Subjects.Add(new SubjectEntity
    {
        Id = healthySubjectId,
        Name = "Healthy User",
        IsActive = true,
        IsSystemSubject = false,
        OidcSubjectId = null,
    });
    _dbContext.PasskeyCredentials.Add(new PasskeyCredentialEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = _tenantId,
        SubjectId = healthySubjectId,
        CredentialId = System.Text.Encoding.UTF8.GetBytes("cred-1"),
        PublicKey = [],
        SignCount = 0,
    });
    _dbContext.TenantMembers.Add(new TenantMemberEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = _tenantId,
        SubjectId = healthySubjectId,
    });

    // Orphaned subject — member of this tenant, no passkey, no OIDC
    _dbContext.Subjects.Add(new SubjectEntity
    {
        Id = orphanedSubjectId,
        Name = "Orphaned User",
        IsActive = true,
        IsSystemSubject = false,
        OidcSubjectId = null,
    });
    _dbContext.TenantMembers.Add(new TenantMemberEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = _tenantId,
        SubjectId = orphanedSubjectId,
    });

    await _dbContext.SaveChangesAsync();

    var (mw, ctx) = Build();

    // Act
    await mw.InvokeAsync(ctx, _tenantAccessor.Object, _dbContext);

    // Assert
    ctx.Response.StatusCode.Should().Be(503);
    ctx.Response.Body.Seek(0, SeekOrigin.Begin);
    var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
    body.Should().Contain("recovery_mode_active");
    body.Should().Contain("\"recoveryMode\":true");
}

[Fact]
public async Task WhenOrphanedSubjectBelongsToDifferentTenant_PassesThrough()
{
    // Arrange — this tenant is healthy, orphaned subject is on another tenant
    var subjectId = Guid.CreateVersion7();
    var orphanedSubjectId = Guid.CreateVersion7();
    var otherTenantId = Guid.CreateVersion7();

    // Healthy subject with passkey on our tenant
    _dbContext.Subjects.Add(new SubjectEntity
    {
        Id = subjectId,
        Name = "Healthy User",
        IsActive = true,
        IsSystemSubject = false,
    });
    _dbContext.PasskeyCredentials.Add(new PasskeyCredentialEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = _tenantId,
        SubjectId = subjectId,
        CredentialId = System.Text.Encoding.UTF8.GetBytes("cred-1"),
        PublicKey = [],
        SignCount = 0,
    });
    _dbContext.TenantMembers.Add(new TenantMemberEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = _tenantId,
        SubjectId = subjectId,
    });

    // Orphaned subject on a different tenant
    _dbContext.Subjects.Add(new SubjectEntity
    {
        Id = orphanedSubjectId,
        Name = "Orphaned User",
        IsActive = true,
        IsSystemSubject = false,
        OidcSubjectId = null,
    });
    _dbContext.TenantMembers.Add(new TenantMemberEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = otherTenantId,
        SubjectId = orphanedSubjectId,
    });

    await _dbContext.SaveChangesAsync();

    var nextCalled = false;
    var (mw, ctx) = Build(onNext: () => nextCalled = true);

    // Act
    await mw.InvokeAsync(ctx, _tenantAccessor.Object, _dbContext);

    // Assert
    nextCalled.Should().BeTrue();
    ctx.Response.StatusCode.Should().NotBe(503);
}

[Fact]
public async Task WhenOrphanedSubjectHasOidc_PassesThrough()
{
    // Arrange — subject has OIDC binding, no passkey needed
    var subjectId = Guid.CreateVersion7();

    _dbContext.Subjects.Add(new SubjectEntity
    {
        Id = subjectId,
        Name = "OIDC User",
        IsActive = true,
        IsSystemSubject = false,
        OidcSubjectId = "oidc-sub-123",
    });
    _dbContext.PasskeyCredentials.Add(new PasskeyCredentialEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = _tenantId,
        SubjectId = subjectId,
        CredentialId = System.Text.Encoding.UTF8.GetBytes("cred-1"),
        PublicKey = [],
        SignCount = 0,
    });
    _dbContext.TenantMembers.Add(new TenantMemberEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = _tenantId,
        SubjectId = subjectId,
    });

    await _dbContext.SaveChangesAsync();

    var nextCalled = false;
    var (mw, ctx) = Build(onNext: () => nextCalled = true);

    // Act
    await mw.InvokeAsync(ctx, _tenantAccessor.Object, _dbContext);

    // Assert
    nextCalled.Should().BeTrue();
}
```

**Step 2: Run to confirm they fail**

```
dotnet test tests/Unit/Nocturne.API.Tests -v minimal --filter "FullyQualifiedName~TenantSetupMiddlewareTests.WhenTenantHasOrphanedSubject|FullyQualifiedName~TenantSetupMiddlewareTests.WhenOrphanedSubject"
```

Expected: FAIL — current middleware doesn't check for orphaned subjects.

**Step 3: Implement the change**

Replace `TenantSetupMiddleware.cs` entirely:

```csharp
using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Middleware;

/// <summary>
/// Middleware that returns 503 for freshly provisioned tenants (no passkey
/// credentials) or tenants in recovery mode (orphaned subjects with no
/// passkey and no OIDC binding). Allows passkey setup, admin, and metadata
/// endpoints through so setup/recovery flows can complete.
///
/// Only active in multi-tenant mode (runs after TenantResolutionMiddleware).
/// Single-tenant setup/recovery is handled by RecoveryModeMiddleware.
/// </summary>
public class TenantSetupMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<TenantSetupMiddleware> _logger;

    private static readonly string[] AllowedPrefixes =
    [
        "/api/admin/",
        "/api/auth/passkey/",
        "/api/auth/totp/",
        "/api/metadata",
    ];

    private static readonly string[] AllowedPaths =
    [
        "/api/admin/tenants/validate-slug",
        "/api/v4/me/tenants/validate-slug",
    ];

    public TenantSetupMiddleware(
        RequestDelegate next,
        ILogger<TenantSetupMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        ITenantAccessor tenantAccessor,
        NocturneDbContext db)
    {
        // Only check when a tenant has been resolved
        if (!tenantAccessor.IsResolved)
        {
            await _next(context);
            return;
        }

        var path = context.Request.Path.Value ?? "";

        // Allow passkey, TOTP, admin, metadata, and slug validation paths
        if (AllowedPaths.Any(p => path.Equals(p, StringComparison.OrdinalIgnoreCase)) ||
            AllowedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
        {
            await _next(context);
            return;
        }

        // Only block API paths
        if (!path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // Check 1: Does this tenant have any passkey credentials at all?
        // PasskeyCredentialEntity is ITenantScoped — query filter applies automatically.
        var hasCredentials = await db.PasskeyCredentials.AnyAsync();
        if (!hasCredentials)
        {
            _logger.LogDebug(
                "Tenant {TenantId} has no passkey credentials — returning setup required",
                tenantAccessor.TenantId);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "setup_required",
                message = "Initial setup required. Please register a passkey to secure your account.",
                setupRequired = true,
                recoveryMode = false,
            });
            return;
        }

        // Check 2: Does this tenant have any orphaned subjects?
        // Subjects are not tenant-scoped — join through TenantMembers to scope to this tenant.
        var tenantId = tenantAccessor.TenantId;
        var hasOrphaned = await db.TenantMembers
            .Where(tm => tm.TenantId == tenantId)
            .Join(
                db.Subjects.Where(s => s.IsActive && !s.IsSystemSubject),
                tm => tm.SubjectId,
                s => s.Id,
                (tm, s) => s)
            .Where(s =>
                s.OidcSubjectId == null &&
                !db.PasskeyCredentials.IgnoreQueryFilters().Any(p => p.SubjectId == s.Id))
            .AnyAsync();

        if (hasOrphaned)
        {
            _logger.LogDebug(
                "Tenant {TenantId} has orphaned subjects — returning recovery mode",
                tenantAccessor.TenantId);

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            await context.Response.WriteAsJsonAsync(new
            {
                error = "recovery_mode_active",
                message = "Instance is in recovery mode. Please register a passkey or authenticator app to continue.",
                setupRequired = false,
                recoveryMode = true,
            });
            return;
        }

        await _next(context);
    }
}
```

**Step 4: Run tests**

```
dotnet test tests/Unit/Nocturne.API.Tests -v minimal --filter "FullyQualifiedName~TenantSetupMiddlewareTests"
```

Expected: all pass (existing + new).

**Step 5: Commit**

```bash
git add src/API/Nocturne.API/Middleware/TenantSetupMiddleware.cs
git add tests/Unit/Nocturne.API.Tests/Middleware/TenantSetupMiddlewareTests.cs
git commit -m "feat: add per-tenant recovery mode detection to TenantSetupMiddleware"
```

---

### Task 4: `PasskeyController` — make status endpoints tenant-aware

**Files:**
- Modify: `src/API/Nocturne.API/Controllers/PasskeyController.cs:446-474`
- Modify: `tests/Unit/Nocturne.API.Tests/Controllers/PasskeyControllerTests.cs`

**Step 1: Write the failing tests**

Add these tests to `PasskeyControllerTests`:

```csharp
[Fact]
public async Task GetRecoveryModeStatus_MultiTenant_QueriesDb()
{
    // Arrange — orphaned subject is a member of the resolved tenant
    var orphanedSubjectId = Guid.CreateVersion7();
    _dbContext.Subjects.Add(new SubjectEntity
    {
        Id = orphanedSubjectId,
        Name = "Orphaned",
        IsActive = true,
        IsSystemSubject = false,
        OidcSubjectId = null,
    });
    _dbContext.TenantMembers.Add(new TenantMemberEntity
    {
        Id = Guid.CreateVersion7(),
        TenantId = _tenantId,
        SubjectId = orphanedSubjectId,
    });
    await _dbContext.SaveChangesAsync();

    var state = new RecoveryModeState(); // global state is NOT set

    // Act
    var result = await _controller.GetRecoveryModeStatus(state);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result);
    var value = okResult.Value;
    var recoveryMode = value!.GetType().GetProperty("recoveryMode")!.GetValue(value);
    recoveryMode.Should().Be(true);
}

[Fact]
public async Task GetAuthStatus_MultiTenant_QueriesDb()
{
    // Arrange — tenant with no credentials (setup required)
    var tenant = new TenantEntity
    {
        Id = _tenantId,
        Slug = "test",
        DisplayName = "Test",
        IsDefault = false,
    };
    _dbContext.Tenants.Add(tenant);
    await _dbContext.SaveChangesAsync();

    var state = new RecoveryModeState(); // global state is NOT set

    // Act
    var result = await _controller.GetAuthStatus(state);

    // Assert
    var okResult = Assert.IsType<OkObjectResult>(result);
    var response = Assert.IsType<AuthStatusResponse>(okResult.Value);
    response.SetupRequired.Should().BeTrue();
}
```

**Step 2: Run to confirm they fail**

```
dotnet test tests/Unit/Nocturne.API.Tests -v minimal --filter "FullyQualifiedName~PasskeyControllerTests.GetRecoveryModeStatus_MultiTenant|FullyQualifiedName~PasskeyControllerTests.GetAuthStatus_MultiTenant"
```

Expected: FAIL — endpoints read global state, not DB.

**Step 3: Implement the change**

Replace `GetRecoveryModeStatus` (around line 446):

```csharp
/// <summary>
/// Returns whether the current tenant is in recovery mode.
/// In multi-tenant mode, queries the database for orphaned subjects.
/// In single-tenant mode, reads from the global RecoveryModeState.
/// </summary>
[HttpGet("recovery-mode-status")]
[AllowAnonymous]
[RemoteQuery]
[ProducesResponseType(StatusCodes.Status200OK)]
public async Task<IActionResult> GetRecoveryModeStatus([FromServices] RecoveryModeState state)
{
    bool recoveryMode;
    if (_tenantAccessor.IsResolved)
    {
        var tenantId = _tenantAccessor.TenantId;
        recoveryMode = await _dbContext.TenantMembers
            .Where(tm => tm.TenantId == tenantId)
            .Join(
                _dbContext.Subjects.Where(s => s.IsActive && !s.IsSystemSubject),
                tm => tm.SubjectId,
                s => s.Id,
                (tm, s) => s)
            .Where(s =>
                s.OidcSubjectId == null &&
                !_dbContext.PasskeyCredentials.IgnoreQueryFilters().Any(p => p.SubjectId == s.Id))
            .AnyAsync();
    }
    else
    {
        recoveryMode = state.IsEnabled;
    }

    return Ok(new { recoveryMode });
}
```

Replace `GetAuthStatus` (around line 458):

```csharp
/// <summary>
/// Returns tenant auth status: whether setup is required or recovery mode is active.
/// In multi-tenant mode, queries the database. In single-tenant mode, reads global state.
/// </summary>
[HttpGet("status")]
[AllowAnonymous]
[RemoteQuery]
[ProducesResponseType(typeof(AuthStatusResponse), StatusCodes.Status200OK)]
public async Task<IActionResult> GetAuthStatus([FromServices] RecoveryModeState state)
{
    bool setupRequired;
    bool recoveryMode;

    if (_tenantAccessor.IsResolved)
    {
        var hasCredentials = await _dbContext.PasskeyCredentials.AnyAsync();
        setupRequired = !hasCredentials;

        if (hasCredentials)
        {
            var tenantId = _tenantAccessor.TenantId;
            recoveryMode = await _dbContext.TenantMembers
                .Where(tm => tm.TenantId == tenantId)
                .Join(
                    _dbContext.Subjects.Where(s => s.IsActive && !s.IsSystemSubject),
                    tm => tm.SubjectId,
                    s => s.Id,
                    (tm, s) => s)
                .Where(s =>
                    s.OidcSubjectId == null &&
                    !_dbContext.PasskeyCredentials.IgnoreQueryFilters().Any(p => p.SubjectId == s.Id))
                .AnyAsync();
        }
        else
        {
            recoveryMode = false;
        }
    }
    else
    {
        setupRequired = state.IsSetupRequired;
        recoveryMode = state.IsEnabled;
    }

    var tenant = _tenantAccessor.IsResolved
        ? await _dbContext.Tenants.FirstOrDefaultAsync(t => t.Id == _tenantAccessor.TenantId)
        : await _dbContext.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.IsDefault);

    return Ok(new AuthStatusResponse
    {
        SetupRequired = setupRequired,
        RecoveryMode = recoveryMode,
        AllowAccessRequests = tenant?.AllowAccessRequests ?? false,
    });
}
```

Note: `GetRecoveryModeStatus` changes from `IActionResult` to `async Task<IActionResult>`.

**Step 4: Run tests**

```
dotnet test tests/Unit/Nocturne.API.Tests -v minimal --filter "FullyQualifiedName~PasskeyControllerTests"
```

Expected: all pass.

**Step 5: Commit**

```bash
git add src/API/Nocturne.API/Controllers/PasskeyController.cs
git add tests/Unit/Nocturne.API.Tests/Controllers/PasskeyControllerTests.cs
git commit -m "feat: make passkey status endpoints tenant-aware"
```

---

### Task 5: Update recovery page copy

**Files:**
- Modify: `src/Web/packages/app/src/routes/auth/recovery/+page.svelte:214,226`
- Modify: `src/Web/locales/en.po`

**Step 1: Update the Svelte page**

In `+page.svelte`, find line 214:

```svelte
              Passkey registered. Recovery mode will be deactivated on next restart.
```

Replace with:

```svelte
              Passkey registered successfully. Recovery mode has been deactivated.
```

Find lines 225-226:

```svelte
      <p class="text-xs text-muted-foreground">
        To disable recovery mode, restart the application after registering a passkey.
      </p>
```

Replace with:

```svelte
      <p class="text-xs text-muted-foreground">
        Register a passkey to restore normal access.
      </p>
```

**Step 2: Update the locale string**

In `src/Web/locales/en.po`, find:

```
msgid "To disable recovery mode, restart the application after registering a passkey."
msgstr "To disable recovery mode, restart the application after registering a passkey."
```

Replace with:

```
msgid "Register a passkey to restore normal access."
msgstr "Register a passkey to restore normal access."
```

**Step 3: Commit**

```bash
git add src/Web/packages/app/src/routes/auth/recovery/+page.svelte
git add src/Web/locales/en.po
git commit -m "fix: update recovery page copy — restart no longer required"
```

---

### Task 6: Cherry-pick slug changes (nocturne-cloud repo)

This task operates on the **nocturne-cloud** repo, not the nocturne submodule.

**Step 1: Cherry-pick the commit**

```bash
cd C:\Users\rhysg\Documents\Github\nocturne-cloud
git cherry-pick origin/claude/fix-tenant-recovery-mode-UP7tn
```

**Step 2: Revert the `RecoveryMode` flag changes**

The cherry-pick includes `RecoveryMode` on `UpdateTenantRequest` and `NocturneAdminClient` which we don't want.

In `services/provisioner/Models/UpdateTenantRequest.cs`, revert to:

```csharp
public record UpdateTenantRequest(string DisplayName, bool IsActive);
```

In `services/provisioner/NocturneAdminClient.cs`, revert `UpdateTenantAsync` signature to:

```csharp
public virtual async Task UpdateTenantAsync(Guid tenantId, string displayName, bool isActive, CancellationToken ct = default)
```

And the request construction to:

```csharp
var request = new UpdateTenantRequest(displayName, isActive);
```

In `services/billing/Controllers/RecoveryController.cs`, revert:

```csharp
await adminClient.UpdateTenantAsync(sub.TenantId, sub.Slug, isActive: true, ct);
```

In test `RecoveryControllerTests.cs`, revert the verify call:

```csharp
_adminClient.Verify(a => a.UpdateTenantAsync(tenantId, "recoverslug", true, It.IsAny<CancellationToken>()), Times.Once);
```

In test `TenantProvisioningServiceTests.cs`, revert:

```csharp
.Setup(x => x.UpdateTenantAsync(tenantId, "My Site", false, It.IsAny<CancellationToken>()))
```

**Step 3: Run tests**

```
dotnet test NocturneCloud.slnx -v minimal
```

Expected: all pass.

**Step 4: Commit**

```bash
git add -A
git commit -m "feat: store slug on subscription entity, fix deactivation display name

Cherry-picked slug storage from claude/fix-tenant-recovery-mode-UP7tn.
Reverted RecoveryMode flag on UpdateTenantRequest — recovery is now
detected per-tenant by Nocturne's TenantSetupMiddleware."
```

---

### Task 7: Final build and full test run

**Step 1: Build nocturne**

```bash
cd C:\Users\rhysg\Documents\Github\nocturne-cloud\nocturne
dotnet build -v minimal
```

Expected: Build succeeded, 0 errors.

**Step 2: Run all nocturne tests**

```
dotnet test -v minimal
```

Expected: all pass.

**Step 3: Build nocturne-cloud**

```bash
cd C:\Users\rhysg\Documents\Github\nocturne-cloud
dotnet build NocturneCloud.slnx -v minimal
```

Expected: Build succeeded, 0 errors.

**Step 4: Run all nocturne-cloud tests**

```
dotnet test NocturneCloud.slnx -v minimal
```

Expected: all pass.
