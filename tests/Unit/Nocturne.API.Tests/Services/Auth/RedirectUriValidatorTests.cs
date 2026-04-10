using FluentAssertions;
using Nocturne.API.Services.Auth;
using Xunit;

namespace Nocturne.API.Tests.Services.Auth;

public class RedirectUriValidatorTests
{
    private readonly RedirectUriValidator _validator = new();

    [Theory]
    [InlineData("org.trio.diabetes:/oauth/callback", true)]
    [InlineData("org.nightscoutfoundation.xdrip://oauth/callback", true)]
    [InlineData("trio:/callback", false)]
    [InlineData("https://trio.app/oauth/callback", true)]
    [InlineData("https://localhost/cb", false)]
    [InlineData("https://127.0.0.1/cb", false)]
    [InlineData("https://[::1]/cb", false)]
    [InlineData("http://example.com/cb", false)]
    [InlineData("http://127.0.0.1/cb", true)]
    [InlineData("http://127.0.0.1:8080/cb", true)]
    [InlineData("http://[::1]/cb", true)]
    [InlineData("https://trio.app/cb#frag", false)]
    [InlineData("", false)]
    public void IsValidForRegistration_Behaves(string uri, bool expected)
    {
        _validator.IsValidForRegistration(uri).Should().Be(expected);
    }

    [Fact]
    public void IsValidForAuthorize_ExactMatch_ReturnsTrue()
    {
        _validator.IsValidForAuthorize(
            "https://trio.app/callback",
            "https://trio.app/callback"
        ).Should().BeTrue();
    }

    [Fact]
    public void IsValidForAuthorize_LoopbackPortVariation_ReturnsTrue()
    {
        _validator.IsValidForAuthorize(
            "http://127.0.0.1/cb",
            "http://127.0.0.1:9876/cb"
        ).Should().BeTrue();
    }

    [Fact]
    public void IsValidForAuthorize_NonLoopbackPortVariation_ReturnsFalse()
    {
        _validator.IsValidForAuthorize(
            "https://trio.app/callback",
            "https://trio.app:9999/callback"
        ).Should().BeFalse();
    }

    [Fact]
    public void IsValidForAuthorize_DifferentPath_ReturnsFalse()
    {
        _validator.IsValidForAuthorize(
            "http://127.0.0.1/cb",
            "http://127.0.0.1/other"
        ).Should().BeFalse();
    }
}
