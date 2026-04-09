using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Controllers;
using Nocturne.API.Services.Auth;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Multitenancy;
using Nocturne.Core.Models.Configuration;
using Subject = Nocturne.Core.Models.Authorization.Subject;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Xunit;

namespace Nocturne.API.Tests.Controllers;

public class PasskeyControllerTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<NocturneDbContext> _dbOptions;
    private readonly NocturneDbContext _dbContext;
    private readonly Mock<IPasskeyService> _passkeyService;
    private readonly Mock<IRecoveryCodeService> _recoveryCodeService;
    private readonly Mock<IJwtService> _jwtService;
    private readonly Mock<IRefreshTokenService> _refreshTokenService;
    private readonly Mock<ISubjectService> _subjectService;
    private readonly Mock<ITenantAccessor> _tenantAccessor;
    private readonly Mock<ITenantService> _tenantService;
    private readonly PasskeyController _controller;

    private readonly Guid _tenantId = Guid.CreateVersion7();

    public PasskeyControllerTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _dbOptions = new DbContextOptionsBuilder<NocturneDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning))
            .Options;

        _dbContext = new NocturneDbContext(_dbOptions);
        _dbContext.Database.EnsureCreated();

        _passkeyService = new Mock<IPasskeyService>();
        _recoveryCodeService = new Mock<IRecoveryCodeService>();
        _jwtService = new Mock<IJwtService>();
        _refreshTokenService = new Mock<IRefreshTokenService>();
        _subjectService = new Mock<ISubjectService>();
        _tenantAccessor = new Mock<ITenantAccessor>();
        _tenantAccessor.Setup(t => t.TenantId).Returns(_tenantId);

        var oidcOptions = Options.Create(new OidcOptions
        {
            Cookie = new CookieSettings
            {
                AccessTokenName = ".Nocturne.AccessToken",
                RefreshTokenName = ".Nocturne.RefreshToken",
                Secure = true,
            },
        });

        var logger = new Mock<ILogger<PasskeyController>>();

        var auditService = new Mock<IAuthAuditService>();

        _tenantService = new Mock<ITenantService>();

        _controller = new PasskeyController(
            _passkeyService.Object,
            _recoveryCodeService.Object,
            _jwtService.Object,
            _refreshTokenService.Object,
            _subjectService.Object,
            auditService.Object,
            _tenantAccessor.Object,
            _tenantService.Object,
            _dbContext,
            oidcOptions,
            logger.Object);

        // Set up HttpContext with response cookies
        var httpContext = new DefaultHttpContext();
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task RegisterOptions_EmptyUsername_ReturnsBadRequest()
    {
        var request = new PasskeyRegisterOptionsRequest
        {
            SubjectId = Guid.CreateVersion7(),
            Username = "",
        };

        var result = await _controller.RegisterOptions(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RegisterOptions_ValidRequest_CallsServiceAndReturnsOptionsWithToken()
    {
        var subjectId = Guid.CreateVersion7();
        _passkeyService
            .Setup(s => s.GenerateRegistrationOptionsAsync(subjectId, "testuser", _tenantId))
            .ReturnsAsync(new PasskeyRegistrationOptions("{\"challenge\":\"abc\"}", "token-data"));

        var request = new PasskeyRegisterOptionsRequest
        {
            SubjectId = subjectId,
            Username = "testuser",
        };

        var result = await _controller.RegisterOptions(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PasskeyOptionsResponse>(okResult.Value);
        Assert.Contains("challenge", response.Options);
        Assert.Equal("token-data", response.ChallengeToken);
        _passkeyService.Verify(s => s.GenerateRegistrationOptionsAsync(subjectId, "testuser", _tenantId), Times.Once);
    }

    [Fact]
    public async Task RegisterComplete_NoChallengeToken_ReturnsBadRequest()
    {
        var request = new PasskeyRegisterCompleteRequest
        {
            AttestationResponseJson = "{}",
            ChallengeToken = "",
        };

        var result = await _controller.RegisterComplete(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task LoginOptions_EmptyUsername_ReturnsBadRequest()
    {
        var request = new PasskeyLoginOptionsRequest { Username = "" };

        var result = await _controller.LoginOptions(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task LoginOptions_ValidRequest_CallsServiceAndReturnsOptionsWithToken()
    {
        _passkeyService
            .Setup(s => s.GenerateAssertionOptionsAsync("testuser", _tenantId))
            .ReturnsAsync(new PasskeyAssertionOptions("{\"challenge\":\"xyz\"}", "assertion-token"));

        var request = new PasskeyLoginOptionsRequest { Username = "testuser" };

        var result = await _controller.LoginOptions(request);

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PasskeyOptionsResponse>(okResult.Value);
        Assert.Contains("challenge", response.Options);
        Assert.Equal("assertion-token", response.ChallengeToken);
        _passkeyService.Verify(s => s.GenerateAssertionOptionsAsync("testuser", _tenantId), Times.Once);
    }

    [Fact]
    public async Task DiscoverableLoginOptions_CallsServiceAndReturnsOptionsWithToken()
    {
        _passkeyService
            .Setup(s => s.GenerateDiscoverableAssertionOptionsAsync(_tenantId))
            .ReturnsAsync(new PasskeyAssertionOptions("{\"challenge\":\"disc\"}", "disc-token"));

        var result = await _controller.DiscoverableLoginOptions();

        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PasskeyOptionsResponse>(okResult.Value);
        Assert.Contains("challenge", response.Options);
        Assert.Equal("disc-token", response.ChallengeToken);
        _passkeyService.Verify(s => s.GenerateDiscoverableAssertionOptionsAsync(_tenantId), Times.Once);
    }

    [Fact]
    public async Task LoginComplete_NoChallengeToken_ReturnsBadRequest()
    {
        var request = new PasskeyLoginCompleteRequest { AssertionResponseJson = "{}", ChallengeToken = "" };

        var result = await _controller.LoginComplete(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RecoveryVerify_EmptyFields_ReturnsBadRequest()
    {
        var request = new RecoveryVerifyRequest { Username = "", Code = "" };

        var result = await _controller.RecoveryVerify(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task RecoveryVerify_UnknownUser_ReturnsBadRequest()
    {
        var request = new RecoveryVerifyRequest { Username = "nonexistent", Code = "123456" };

        var result = await _controller.RecoveryVerify(request);

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    #region Setup Flow — Admin Role Assignment

    [Fact]
    public async Task SetupOptions_WhenSetupRequired_CreatesSubjectAndAssignsAdminRole()
    {
        // Arrange — seed a default tenant
        var defaultTenant = new TenantEntity
        {
            Id = Guid.CreateVersion7(),
            Slug = "default",
            DisplayName = "Default",
            IsDefault = true,
        };
        _dbContext.Tenants.Add(defaultTenant);
        await _dbContext.SaveChangesAsync();

        var state = new RecoveryModeState { IsSetupRequired = true };

        _passkeyService
            .Setup(s => s.GenerateRegistrationOptionsAsync(
                It.IsAny<Guid>(), "admin", defaultTenant.Id))
            .ReturnsAsync(new PasskeyRegistrationOptions("{\"challenge\":\"setup\"}", "setup-token"));

        _subjectService
            .Setup(s => s.AssignRoleAsync(It.IsAny<Guid>(), "admin", null))
            .ReturnsAsync(true);

        var request = new SetupOptionsRequest
        {
            Username = "admin",
            DisplayName = "Administrator",
        };

        // Act
        var result = await _controller.SetupOptions(request, state);

        // Assert — response is OK with passkey options
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PasskeyOptionsResponse>(okResult.Value);
        response.ChallengeToken.Should().Be("setup-token");
        response.Options.Should().Contain("challenge");

        // Assert — admin role was assigned
        _subjectService.Verify(
            s => s.AssignRoleAsync(It.IsAny<Guid>(), "admin", null),
            Times.Once);

        // Assert — subject was persisted in the database
        var subjects = await _dbContext.Subjects
            .IgnoreQueryFilters()
            .Where(s => !s.IsSystemSubject)
            .ToListAsync();
        subjects.Should().HaveCount(1);
        subjects[0].Username.Should().Be("admin");
        subjects[0].Name.Should().Be("Administrator");
        subjects[0].IsActive.Should().BeTrue();

        // Assert — tenant membership was created as Owner
        var members = await _dbContext.TenantMembers
            .IgnoreQueryFilters()
            .Where(tm => tm.TenantId == defaultTenant.Id)
            .ToListAsync();
        members.Should().HaveCount(1);
        members[0].SubjectId.Should().Be(subjects[0].Id);
    }

    [Fact]
    public async Task SetupOptions_WhenSetupNotRequired_ReturnsForbidden()
    {
        var state = new RecoveryModeState { IsSetupRequired = false };
        var request = new SetupOptionsRequest
        {
            Username = "admin",
            DisplayName = "Administrator",
        };

        var result = await _controller.SetupOptions(request, state);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SetupOptions_MissingUsername_ReturnsBadRequest()
    {
        var state = new RecoveryModeState { IsSetupRequired = true };
        var request = new SetupOptionsRequest
        {
            Username = "",
            DisplayName = "Administrator",
        };

        var result = await _controller.SetupOptions(request, state);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        objectResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task SetupComplete_IssuesSessionWithAdminRoleAndWildcardPermission()
    {
        // Arrange — seed a default tenant
        var defaultTenant = new TenantEntity
        {
            Id = Guid.CreateVersion7(),
            Slug = "default",
            DisplayName = "Default",
            IsDefault = true,
        };
        _dbContext.Tenants.Add(defaultTenant);
        await _dbContext.SaveChangesAsync();

        var state = new RecoveryModeState { IsSetupRequired = true };
        var subjectId = Guid.CreateVersion7();

        _passkeyService
            .Setup(s => s.CompleteRegistrationAsync("{}", "challenge-token", defaultTenant.Id))
            .ReturnsAsync(new PasskeyCredentialResult(Guid.CreateVersion7(), subjectId));

        _recoveryCodeService
            .Setup(s => s.GenerateCodesAsync(subjectId))
            .ReturnsAsync(new List<string> { "CODE1", "CODE2", "CODE3" });

        _subjectService
            .Setup(s => s.GetSubjectByIdAsync(subjectId))
            .ReturnsAsync(new Subject
            {
                Id = subjectId,
                Name = "Administrator",
                Email = null,
            });

        _subjectService
            .Setup(s => s.GetSubjectRolesAsync(subjectId))
            .ReturnsAsync(new List<string> { "admin" });

        _subjectService
            .Setup(s => s.GetSubjectPermissionsAsync(subjectId))
            .ReturnsAsync(new List<string> { "*" });

        _jwtService
            .Setup(s => s.GenerateAccessToken(
                It.Is<SubjectInfo>(si => si.Id == subjectId && si.Name == "Administrator"),
                It.Is<IEnumerable<string>>(p => p.Contains("*")),
                It.Is<IEnumerable<string>>(r => r.Contains("admin")),
                null))
            .Returns("jwt-access-token");

        _jwtService
            .Setup(s => s.GetAccessTokenLifetime())
            .Returns(TimeSpan.FromMinutes(15));

        _refreshTokenService
            .Setup(s => s.CreateRefreshTokenAsync(
                subjectId,
                null,
                "Setup Passkey",
                It.IsAny<string?>(),
                It.IsAny<string?>()))
            .ReturnsAsync("refresh-token-value");

        var request = new SetupCompleteRequest
        {
            AttestationResponseJson = "{}",
            ChallengeToken = "challenge-token",
        };

        // Act
        var result = await _controller.SetupComplete(request, state);

        // Assert — response is OK with session tokens
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SetupCompleteResponse>(okResult.Value);
        response.Success.Should().BeTrue();
        response.AccessToken.Should().Be("jwt-access-token");
        response.RefreshToken.Should().Be("refresh-token-value");
        response.RecoveryCodes.Should().HaveCount(3);
        response.ExpiresIn.Should().Be(900);

        // Assert — setup mode was deactivated
        state.IsSetupRequired.Should().BeFalse();

        // Assert — JWT was generated with admin role and wildcard permission
        _jwtService.Verify(
            s => s.GenerateAccessToken(
                It.Is<SubjectInfo>(si => si.Id == subjectId),
                It.Is<IEnumerable<string>>(p => p.Contains("*")),
                It.Is<IEnumerable<string>>(r => r.Contains("admin")),
                null),
            Times.Once);

        // Assert — roles and permissions were fetched for the subject
        _subjectService.Verify(s => s.GetSubjectRolesAsync(subjectId), Times.Once);
        _subjectService.Verify(s => s.GetSubjectPermissionsAsync(subjectId), Times.Once);
    }

    [Fact]
    public async Task SetupComplete_WhenSetupNotRequired_ReturnsForbidden()
    {
        var state = new RecoveryModeState { IsSetupRequired = false };
        var request = new SetupCompleteRequest
        {
            AttestationResponseJson = "{}",
            ChallengeToken = "token",
        };

        var result = await _controller.SetupComplete(request, state);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        objectResult.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task SetupComplete_MissingChallengeToken_ReturnsBadRequest()
    {
        var state = new RecoveryModeState { IsSetupRequired = true };
        var request = new SetupCompleteRequest
        {
            AttestationResponseJson = "{}",
            ChallengeToken = "",
        };

        var result = await _controller.SetupComplete(request, state);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        objectResult.StatusCode.Should().Be(400);
    }

    #endregion

    #region Multi-Tenant Setup Flow

    [Fact]
    public async Task SetupOptions_MultiTenant_CreatesUserInResolvedTenant()
    {
        // Arrange — seed the resolved tenant (not IsDefault)
        var tenant = new TenantEntity
        {
            Id = _tenantId,
            Slug = "rhys",
            DisplayName = "Rhys",
            IsDefault = false,
        };
        var ownerRole = new TenantRoleEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantId,
            Slug = "owner",
            Name = "Owner",
            IsSystem = true,
            SysCreatedAt = DateTime.UtcNow,
            SysUpdatedAt = DateTime.UtcNow,
        };
        _dbContext.Tenants.Add(tenant);
        _dbContext.TenantRoles.Add(ownerRole);
        await _dbContext.SaveChangesAsync();

        // IsSetupRequired is false (multi-tenant mode — global flag is never set)
        var state = new RecoveryModeState { IsSetupRequired = false };

        _tenantAccessor.Setup(t => t.IsResolved).Returns(true);

        _tenantService
            .Setup(s => s.AddMemberAsync(_tenantId, It.IsAny<Guid>(), It.IsAny<List<Guid>>(), null, null, false, default))
            .Callback<Guid, Guid, List<Guid>, List<string>?, string?, bool, CancellationToken>((tenantId, subjectId, _, _, _, _, _) =>
            {
                _dbContext.TenantMembers.Add(new TenantMemberEntity
                {
                    Id = Guid.CreateVersion7(),
                    TenantId = tenantId,
                    SubjectId = subjectId,
                    SysCreatedAt = DateTime.UtcNow,
                    SysUpdatedAt = DateTime.UtcNow,
                });
                _dbContext.SaveChanges();
            })
            .Returns(Task.CompletedTask);

        _passkeyService
            .Setup(s => s.GenerateRegistrationOptionsAsync(
                It.IsAny<Guid>(), "rhys", _tenantId))
            .ReturnsAsync(new PasskeyRegistrationOptions("{\"challenge\":\"mt\"}", "mt-token"));

        _subjectService
            .Setup(s => s.AssignRoleAsync(It.IsAny<Guid>(), "admin", null))
            .ReturnsAsync(true);

        var request = new SetupOptionsRequest
        {
            Username = "rhys",
            DisplayName = "Rhys",
        };

        // Act
        var result = await _controller.SetupOptions(request, state);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<PasskeyOptionsResponse>(okResult.Value);
        response.ChallengeToken.Should().Be("mt-token");

        var members = await _dbContext.TenantMembers
            .IgnoreQueryFilters()
            .Where(tm => tm.TenantId == _tenantId)
            .ToListAsync();
        members.Should().HaveCount(1);

        // Assert — subject was persisted with correct properties
        var subjects = await _dbContext.Subjects
            .IgnoreQueryFilters()
            .Where(s => !s.IsSystemSubject)
            .ToListAsync();
        subjects.Should().HaveCount(1);
        subjects[0].Username.Should().Be("rhys");
        subjects[0].Name.Should().Be("Rhys");
        subjects[0].IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SetupComplete_MultiTenant_UsesResolvedTenant()
    {
        // Arrange
        var tenant = new TenantEntity
        {
            Id = _tenantId,
            Slug = "rhys",
            DisplayName = "Rhys",
            IsDefault = false,
        };
        _dbContext.Tenants.Add(tenant);
        await _dbContext.SaveChangesAsync();

        var state = new RecoveryModeState { IsSetupRequired = false };
        var subjectId = Guid.CreateVersion7();

        _tenantAccessor.Setup(t => t.IsResolved).Returns(true);

        _passkeyService
            .Setup(s => s.CompleteRegistrationAsync("{}", "token", _tenantId))
            .ReturnsAsync(new PasskeyCredentialResult(Guid.CreateVersion7(), subjectId));

        _recoveryCodeService
            .Setup(s => s.GenerateCodesAsync(subjectId))
            .ReturnsAsync(["CODE1", "CODE2", "CODE3"]);

        _subjectService
            .Setup(s => s.GetSubjectByIdAsync(subjectId))
            .ReturnsAsync(new Subject { Id = subjectId, Name = "Rhys" });

        _subjectService
            .Setup(s => s.GetSubjectRolesAsync(subjectId))
            .ReturnsAsync([]);

        _subjectService
            .Setup(s => s.GetSubjectPermissionsAsync(subjectId))
            .ReturnsAsync([]);

        _jwtService
            .Setup(s => s.GenerateAccessToken(
                It.IsAny<SubjectInfo>(),
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<IEnumerable<string>>(),
                null))
            .Returns("access-token");

        _jwtService
            .Setup(s => s.GetAccessTokenLifetime())
            .Returns(TimeSpan.FromMinutes(15));

        _refreshTokenService
            .Setup(s => s.CreateRefreshTokenAsync(
                subjectId, null, It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
            .ReturnsAsync("refresh-token");

        var request = new SetupCompleteRequest
        {
            ChallengeToken = "token",
            AttestationResponseJson = "{}",
        };

        // Act
        var result = await _controller.SetupComplete(request, state);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var response = Assert.IsType<SetupCompleteResponse>(okResult.Value);
        response.Success.Should().BeTrue();
        response.RecoveryCodes.Should().HaveCount(3);
        response.AccessToken.Should().Be("access-token");
        response.RefreshToken.Should().Be("refresh-token");

        // Global state is not mutated in multi-tenant mode
        state.IsSetupRequired.Should().BeFalse();
    }

    [Fact]
    public async Task SetupOptions_MultiTenant_WhenAlreadyHasCredential_ReturnsForbidden()
    {
        // Arrange — tenant already has a passkey credential (setup already done)
        var subjectId = Guid.CreateVersion7();
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = subjectId,
            Name = "Existing",
            Username = "existing",
            IsActive = true,
            IsSystemSubject = false,
        });
        _dbContext.PasskeyCredentials.Add(new PasskeyCredentialEntity
        {
            Id = Guid.CreateVersion7(),
            SubjectId = subjectId,
            CredentialId = System.Text.Encoding.UTF8.GetBytes("existing-cred"),
            PublicKey = [],
            SignCount = 0,
        });
        // Link subject to tenant so the membership-based check finds the passkey
        _dbContext.TenantMembers.Add(new TenantMemberEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantId,
            SubjectId = subjectId,
        });
        await _dbContext.SaveChangesAsync();

        var state = new RecoveryModeState { IsSetupRequired = false };
        var request = new SetupOptionsRequest { Username = "admin", DisplayName = "Admin" };

        _tenantAccessor.Setup(t => t.IsResolved).Returns(true);

        // Act
        var result = await _controller.SetupOptions(request, state);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        objectResult.StatusCode.Should().Be(403);
    }

    #endregion

    #region Multi-Tenant Status Endpoints

    [Fact]
    public async Task GetRecoveryModeStatus_MultiTenant_QueriesDb()
    {
        // Arrange — seed the tenant first (FK requirement)
        _dbContext.Tenants.Add(new TenantEntity
        {
            Id = _tenantId,
            Slug = "test",
            DisplayName = "Test",
            IsDefault = false,
        });

        // Arrange — orphaned subject is a member of the resolved tenant
        var orphanedSubjectId = Guid.CreateVersion7();
        _dbContext.Subjects.Add(new SubjectEntity
        {
            Id = orphanedSubjectId,
            Name = "Orphaned",
            IsActive = true,
            IsSystemSubject = false,
        });
        _dbContext.TenantMembers.Add(new TenantMemberEntity
        {
            Id = Guid.CreateVersion7(),
            TenantId = _tenantId,
            SubjectId = orphanedSubjectId,
        });
        await _dbContext.SaveChangesAsync();

        var state = new RecoveryModeState(); // global state is NOT set

        _tenantAccessor.Setup(t => t.IsResolved).Returns(true);

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

        _tenantAccessor.Setup(t => t.IsResolved).Returns(true);

        // Act
        var result = await _controller.GetAuthStatus(state);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthStatusResponse>(okResult.Value);
        response.SetupRequired.Should().BeTrue();
    }

    #endregion
}
