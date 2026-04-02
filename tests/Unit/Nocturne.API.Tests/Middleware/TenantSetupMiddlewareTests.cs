using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Middleware;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.API.Tests.Middleware;

public class TenantSetupMiddlewareTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly NocturneDbContext _dbContext;
    private readonly Mock<ITenantAccessor> _tenantAccessor;
    private readonly Guid _tenantId = Guid.CreateVersion7();

    public TenantSetupMiddlewareTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _dbContext = new NocturneDbContext(options);
        _dbContext.TenantId = _tenantId;
        _dbContext.Database.EnsureCreated();

        _tenantAccessor = new Mock<ITenantAccessor>();
        _tenantAccessor.Setup(t => t.IsResolved).Returns(true);
        _tenantAccessor.Setup(t => t.TenantId).Returns(_tenantId);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    private (TenantSetupMiddleware middleware, DefaultHttpContext context) Build(
        string path = "/api/status",
        Action? onNext = null)
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Response.Body = new MemoryStream();
        var mw = new TenantSetupMiddleware(
            _ => { onNext?.Invoke(); return Task.CompletedTask; },
            NullLogger<TenantSetupMiddleware>.Instance);
        return (mw, ctx);
    }

    [Fact]
    public async Task WhenTenantHasNoCredentials_Returns503WithSetupRequired()
    {
        // Arrange — no passkey credentials in db
        var mw = new TenantSetupMiddleware(
            _ => Task.CompletedTask,
            NullLogger<TenantSetupMiddleware>.Instance);

        var ctx = new DefaultHttpContext();
        ctx.Request.Path = "/api/status";
        ctx.Response.Body = new MemoryStream();

        // Act
        await mw.InvokeAsync(ctx, _tenantAccessor.Object, _dbContext);

        // Assert
        ctx.Response.StatusCode.Should().Be(503);
        ctx.Response.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(ctx.Response.Body).ReadToEndAsync();
        body.Should().Contain("setup_required");
        body.Should().Contain("\"setupRequired\":true");
    }

    [Fact]
    public async Task WhenTenantHasCredential_CallsNext()
    {
        // Arrange — seed a passkey credential for this tenant
        var subjectId = Guid.CreateVersion7();
        _dbContext.PasskeyCredentials.Add(new PasskeyCredentialEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantId,
            SubjectId = subjectId,
            CredentialId = System.Text.Encoding.UTF8.GetBytes("cred-id"),
            PublicKey = [],
            SignCount = 0,
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

    [Theory]
    [InlineData("/api/auth/passkey/setup/options")]
    [InlineData("/api/auth/passkey/setup/complete")]
    [InlineData("/api/auth/passkey/register")]
    [InlineData("/api/auth/totp/setup")]
    [InlineData("/api/metadata")]
    [InlineData("/api/admin/tenants/validate-slug")]
    [InlineData("/api/v4/me/tenants/validate-slug")]
    public async Task AllowListPaths_AreNotBlocked_EvenWithNoCredentials(string path)
    {
        // Arrange — no credentials
        var nextCalled = false;
        var (mw, ctx) = Build(path: path, onNext: () => nextCalled = true);

        // Act
        await mw.InvokeAsync(ctx, _tenantAccessor.Object, _dbContext);

        // Assert
        nextCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().NotBe(503);
    }

    [Fact]
    public async Task WhenTenantNotResolved_CallsNext()
    {
        // Arrange — unresolved tenant
        _tenantAccessor.Setup(t => t.IsResolved).Returns(false);
        var nextCalled = false;
        var (mw, ctx) = Build(onNext: () => nextCalled = true);

        // Act
        await mw.InvokeAsync(ctx, _tenantAccessor.Object, _dbContext);

        // Assert
        nextCalled.Should().BeTrue();
    }
}
