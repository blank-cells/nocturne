using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Nocturne.Connectors.HomeAssistant.Models;
using Nocturne.Connectors.HomeAssistant.Services;
using Xunit;

namespace Nocturne.Connectors.HomeAssistant.Tests.Services;

public class HomeAssistantApiClientTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly Mock<ILogger<HomeAssistantApiClient>> _loggerMock = new();
    private readonly HomeAssistantApiClient _sut;

    public HomeAssistantApiClientTests()
    {
        var httpClient = new HttpClient(_handlerMock.Object)
        {
            BaseAddress = new Uri("http://homeassistant.local:8123")
        };

        _sut = new HomeAssistantApiClient(httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task GetStateAsync_ReturnsState_WhenEntityExists()
    {
        var json = JsonSerializer.Serialize(new
        {
            entity_id = "sensor.glucose",
            state = "120",
            attributes = new Dictionary<string, object?> { ["unit_of_measurement"] = "mg/dL" },
            last_changed = "2026-04-06T12:00:00Z",
            last_updated = "2026-04-06T12:00:00Z"
        });

        SetupHandler(HttpStatusCode.OK, json);

        var result = await _sut.GetStateAsync("sensor.glucose");

        result.Should().NotBeNull();
        result!.EntityId.Should().Be("sensor.glucose");
        result.State.Should().Be("120");
    }

    [Fact]
    public async Task GetStateAsync_ReturnsNull_WhenEntityNotFound()
    {
        SetupHandler(HttpStatusCode.NotFound);

        var result = await _sut.GetStateAsync("sensor.nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetStateAsync_ThrowsOnServerError()
    {
        SetupHandler(HttpStatusCode.InternalServerError);

        var act = () => _sut.GetStateAsync("sensor.glucose");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task SetStateAsync_SendsPostToCorrectUrl()
    {
        SetupHandler(HttpStatusCode.OK, "{}");

        var attributes = new Dictionary<string, object> { ["unit_of_measurement"] = "mg/dL" };
        var result = await _sut.SetStateAsync("sensor.glucose", "120", attributes);

        result.Should().BeTrue();

        _handlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(r =>
                r.Method == HttpMethod.Post &&
                r.RequestUri!.PathAndQuery == "/api/states/sensor.glucose"),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SetStateAsync_ReturnsFalse_OnErrorResponse()
    {
        SetupHandler(HttpStatusCode.Forbidden);

        var result = await _sut.SetStateAsync("sensor.glucose", "120", new Dictionary<string, object>());

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateEntityExistsAsync_ReturnsTrue_WhenEntityExists()
    {
        var json = JsonSerializer.Serialize(new
        {
            entity_id = "sensor.glucose",
            state = "120",
            attributes = new Dictionary<string, object?>(),
            last_changed = "2026-04-06T12:00:00Z",
            last_updated = "2026-04-06T12:00:00Z"
        });

        SetupHandler(HttpStatusCode.OK, json);

        var result = await _sut.ValidateEntityExistsAsync("sensor.glucose");

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ValidateEntityExistsAsync_ReturnsFalse_WhenEntityNotFound()
    {
        SetupHandler(HttpStatusCode.NotFound);

        var result = await _sut.ValidateEntityExistsAsync("sensor.nonexistent");

        result.Should().BeFalse();
    }

    private void SetupHandler(HttpStatusCode statusCode, string? content = null)
    {
        var response = new HttpResponseMessage(statusCode);

        if (content != null)
        {
            response.Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json");
        }

        _handlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}
