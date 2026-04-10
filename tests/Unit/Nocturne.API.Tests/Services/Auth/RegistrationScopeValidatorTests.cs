using FluentAssertions;
using Nocturne.API.Services.Auth;
using Xunit;

namespace Nocturne.API.Tests.Services.Auth;

public class RegistrationScopeValidatorTests
{
    [Fact]
    public void ValidateScopes_AllValid_ReturnsNull()
    {
        RegistrationScopeValidator.ValidateScopes("entries.read treatments.read")
            .Should().BeNull();
    }

    [Fact]
    public void ValidateScopes_UnknownScope_ReturnsList()
    {
        var result = RegistrationScopeValidator.ValidateScopes("entries.read evil.scope");
        result.Should().NotBeNull();
        result.Should().Contain("evil.scope");
    }

    [Fact]
    public void ValidateScopes_NullOrEmpty_ReturnsNull()
    {
        RegistrationScopeValidator.ValidateScopes(null).Should().BeNull();
        RegistrationScopeValidator.ValidateScopes("").Should().BeNull();
        RegistrationScopeValidator.ValidateScopes("   ").Should().BeNull();
    }

    [Fact]
    public void ValidateScopes_FullAccess_IsValid()
    {
        RegistrationScopeValidator.ValidateScopes("*").Should().BeNull();
    }
}
