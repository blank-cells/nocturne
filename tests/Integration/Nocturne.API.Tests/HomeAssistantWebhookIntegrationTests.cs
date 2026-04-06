using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Nocturne.API.Tests.Integration.Infrastructure;
using Nocturne.Core.Constants;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration;

/// <summary>
/// Integration tests for the Home Assistant webhook endpoint.
/// The HA connector is not configured in the test environment, so tests verify
/// route registration and appropriate error responses.
/// </summary>
[Trait("Category", "Integration")]
[Parity]
public class HomeAssistantWebhookIntegrationTests : AspireIntegrationTestBase
{
    public HomeAssistantWebhookIntegrationTests(
        AspireIntegrationTestFixture fixture,
        ITestOutputHelper output
    )
        : base(fixture, output) { }

    private static object CreateTestPayload() =>
        new
        {
            entity_id = "sensor.glucose",
            state = "120",
            attributes = new Dictionary<string, object>
            {
                ["unit_of_measurement"] = "mg/dL",
            },
            last_changed = DateTimeOffset.UtcNow.ToString("o"),
            last_updated = DateTimeOffset.UtcNow.ToString("o"),
        };

    [Fact]
    public async Task Webhook_WithWrongSecret_Returns401OrNotConfigured()
    {
        var client = CreateHttpClient(ServiceNames.NocturneApi);
        var payload = CreateTestPayload();

        var response = await client.PostAsJsonAsync(
            "/api/v4/connectors/home-assistant/webhook/wrong-secret-value",
            payload
        );

        // Should be 401 (wrong secret) or 404 (connector not configured in test env)
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Unauthorized, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Webhook_EndpointExists_AndIsReachable()
    {
        var client = CreateHttpClient(ServiceNames.NocturneApi);
        var payload = CreateTestPayload();

        var response = await client.PostAsJsonAsync(
            "/api/v4/connectors/home-assistant/webhook/test-secret",
            payload
        );

        // The route should be registered - should NOT return MethodNotAllowed
        response.StatusCode.Should().NotBe(HttpStatusCode.MethodNotAllowed);
    }
}
