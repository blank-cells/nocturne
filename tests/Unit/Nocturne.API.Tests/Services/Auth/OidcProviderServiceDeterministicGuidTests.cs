using FluentAssertions;
using Nocturne.API.Services.Auth;
using Xunit;

namespace Nocturne.API.Tests.Services.Auth;

/// <summary>
/// Tests for the deterministic GUID generation used by both
/// OidcProviderService (config sync) and TenantService (OIDC provisioning).
/// </summary>
public class OidcProviderServiceDeterministicGuidTests
{
    [Fact]
    public void CreateDeterministicGuid_IsDeterministic()
    {
        var a = OidcProviderService.CreateDeterministicGuid("https://accounts.google.com");
        var b = OidcProviderService.CreateDeterministicGuid("https://accounts.google.com");

        a.Should().Be(b);
    }

    [Fact]
    public void CreateDeterministicGuid_DifferentInputs_ProduceDifferentGuids()
    {
        var google = OidcProviderService.CreateDeterministicGuid("https://accounts.google.com");
        var microsoft = OidcProviderService.CreateDeterministicGuid("https://login.microsoftonline.com");

        google.Should().NotBe(microsoft);
    }

    [Fact]
    public void CreateDeterministicGuid_TrailingSlash_ProducesDifferentGuid()
    {
        // This test documents why callers MUST normalize (TrimEnd('/'))
        // before calling CreateDeterministicGuid — the algorithm is
        // input-sensitive by design.
        var withSlash = OidcProviderService.CreateDeterministicGuid("https://accounts.google.com/");
        var withoutSlash = OidcProviderService.CreateDeterministicGuid("https://accounts.google.com");

        withSlash.Should().NotBe(withoutSlash,
            "trailing slash changes the hash — callers must normalize the issuer URL first");
    }

    [Fact]
    public void CreateDeterministicGuid_SetsVersion5AndVariantBits()
    {
        var guid = OidcProviderService.CreateDeterministicGuid("https://accounts.google.com");
        var bytes = guid.ToByteArray();

        // Version nibble (byte 7 high nibble in .NET's mixed-endian layout = byte[7])
        // In the standard UUID layout, version is in octet 6 bits 4-7.
        // .NET Guid.ToByteArray() has a mixed-endian layout, but we set bytes[6] directly
        // so we can check the same position.
        ((bytes[6] & 0xF0) == 0x50).Should().BeTrue("version nibble should be 5");
        ((bytes[8] & 0xC0) == 0x80).Should().BeTrue("variant bits should be RFC 4122");
    }
}
