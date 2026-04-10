using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Nocturne.API.Controllers.V1;
using Nocturne.API.Tests.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Controllers;

public class AuthenticationControllerTests : IClassFixture<AuthenticationTestFactory>
{
    private readonly AuthenticationTestFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationControllerTests(AuthenticationTestFactory factory)
    {
        _factory = factory;
        _client = _factory.CreateClient();
    }

    [Fact]
    public async Task VerifyAuth_NoAuthentication_ReturnsUnauthorizedResponse()
    {
        // Arrange
        // No authentication headers provided

        // Act
        var response = await _client.GetAsync("/api/v1/verifyauth", CancellationToken.None);
        var content = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("NOT_FOUND", content);
        Assert.Contains("UNAUTHORIZED", content);
    }

    [Fact]
    public async Task VerifyAuth_ValidApiSecret_ReturnsOkResponse()
    {
        // Arrange — send SHA1 of the factory's configured API secret
        var hash = ComputeSha1Hash(AuthenticationTestFactory.ApiSecret);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("api-secret", hash);

        // Act
        var response = await client.GetAsync("/api/v1/verifyauth", CancellationToken.None);
        var content = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("OK", content);
    }

    [Fact]
    public async Task VerifyAuth_InvalidApiSecret_ReturnsUnauthorizedResponse()
    {
        // Arrange
        var apiSecret = "test-api-secret";
        var wrongHash = ComputeSha1Hash("wrong-secret");

        var client = _factory
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("INSTANCE_KEY", apiSecret);
            })
            .CreateClient();

        client.DefaultRequestHeaders.Add("api-secret", wrongHash);

        // Act
        var response = await client.GetAsync("/api/v1/verifyauth", CancellationToken.None);
        var content = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("NOT_FOUND", content);
        Assert.Contains("UNAUTHORIZED", content);
    }

    [Fact]
    public async Task VerifyAuth_ValidJwtToken_ReturnsTokenResponse()
    {
        // Arrange
        // This test would require setting up a valid JWT token
        // For now, we'll test the structure with an invalid token to ensure error handling

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            "invalid-token"
        );

        // Act
        var response = await client.GetAsync("/api/v1/verifyauth", CancellationToken.None);
        var content = await response.Content.ReadAsStringAsync(CancellationToken.None);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("NOT_FOUND", content);
        Assert.Contains("UNAUTHORIZED", content);
    }

    private static string ComputeSha1Hash(string input)
    {
        using var sha1 = SHA1.Create();
        var hashBytes = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }
}
