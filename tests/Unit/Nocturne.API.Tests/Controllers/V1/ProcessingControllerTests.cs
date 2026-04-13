using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Controllers.V4.TenantAdmin;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4;

/// <summary>
/// Unit tests for ProcessingController
/// </summary>
public class ProcessingControllerTests
{
    private readonly Mock<IProcessingStatusService> _mockProcessingStatusService;
    private readonly Mock<ILogger<ProcessingController>> _mockLogger;
    private readonly ProcessingController _controller;

    public ProcessingControllerTests()
    {
        _mockProcessingStatusService = new Mock<IProcessingStatusService>();
        _mockLogger = new Mock<ILogger<ProcessingController>>();
        _controller = new ProcessingController(
            _mockProcessingStatusService.Object,
            _mockLogger.Object
        );

        // Setup controller context
        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    [Fact]
    public async Task GetProcessingStatus_WithValidCorrelationId_ReturnsOk()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var status = new ProcessingStatus
        {
            CorrelationId = correlationId,
            Status = "processing",
            Progress = 50,
            ProcessedCount = 5,
            TotalCount = 10,
            StartedAt = DateTime.UtcNow,
        };

        _mockProcessingStatusService
            .Setup(x => x.GetStatusAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        // Act
        var result = await _controller.GetProcessingStatus(correlationId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProcessingStatusResponse>().Subject;
        response.CorrelationId.Should().Be(correlationId);
        response.Status.Should().Be("processing");
        response.Progress.Should().Be(50);
    }

    [Fact]
    public async Task GetProcessingStatus_WithNonExistentCorrelationId_ReturnsNotFound()
    {
        // Arrange
        var correlationId = "non-existent-id";

        _mockProcessingStatusService
            .Setup(x => x.GetStatusAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessingStatus?)null);

        // Act
        var result = await _controller.GetProcessingStatus(correlationId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetProcessingStatus_WithEmptyCorrelationId_ReturnsBadRequest()
    {
        // Arrange
        var correlationId = "";

        // Act
        var result = await _controller.GetProcessingStatus(correlationId);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetProcessingStatus_WithCompletedStatus_IncludesResults()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var results = new { ProcessedEntries = 10 };
        var status = new ProcessingStatus
        {
            CorrelationId = correlationId,
            Status = "completed",
            Progress = 100,
            ProcessedCount = 10,
            TotalCount = 10,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Results = results,
        };

        _mockProcessingStatusService
            .Setup(x => x.GetStatusAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        // Act
        var result = await _controller.GetProcessingStatus(correlationId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProcessingStatusResponse>().Subject;
        response.Status.Should().Be("completed");
        response.Results.Should().NotBeNull();
    }

    [Fact]
    public async Task GetProcessingStatus_WithProcessingStatus_ExcludesResults()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var status = new ProcessingStatus
        {
            CorrelationId = correlationId,
            Status = "processing",
            Progress = 50,
            ProcessedCount = 5,
            TotalCount = 10,
            StartedAt = DateTime.UtcNow,
            Results = new { SomeData = "test" }, // Should be excluded for non-completed status
        };

        _mockProcessingStatusService
            .Setup(x => x.GetStatusAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(status);

        // Act
        var result = await _controller.GetProcessingStatus(correlationId);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProcessingStatusResponse>().Subject;
        response.Status.Should().Be("processing");
        response.Results.Should().BeNull();
    }

    [Fact]
    public async Task WaitForCompletion_WithCompletedStatus_ReturnsImmediately()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var completedStatus = new ProcessingStatus
        {
            CorrelationId = correlationId,
            Status = "completed",
            Progress = 100,
            ProcessedCount = 10,
            TotalCount = 10,
            StartedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
        };

        _mockProcessingStatusService
            .Setup(x => x.GetStatusAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedStatus);

        _mockProcessingStatusService
            .Setup(x =>
                x.WaitForCompletionAsync(
                    correlationId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(completedStatus);

        // Act
        var result = await _controller.WaitForCompletion(correlationId, 30);

        // Assert
        result.Should().NotBeNull();
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = okResult.Value.Should().BeOfType<ProcessingStatusResponse>().Subject;
        response.Status.Should().Be("completed");
    }

    [Fact]
    public async Task WaitForCompletion_WithTimeout_ReturnsTimeout()
    {
        // Arrange
        var correlationId = "test-correlation-id";
        var processingStatus = new ProcessingStatus
        {
            CorrelationId = correlationId,
            Status = "processing",
            Progress = 50,
            ProcessedCount = 5,
            TotalCount = 10,
            StartedAt = DateTime.UtcNow,
        };

        _mockProcessingStatusService
            .Setup(x => x.GetStatusAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(processingStatus);

        _mockProcessingStatusService
            .Setup(x =>
                x.WaitForCompletionAsync(
                    correlationId,
                    It.IsAny<TimeSpan>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync((ProcessingStatus?)null);

        // Act
        var result = await _controller.WaitForCompletion(correlationId, 1);

        // Assert
        result.Should().NotBeNull();
        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(408); // Timeout
    }

    [Fact]
    public async Task WaitForCompletion_WithNonExistentCorrelationId_ReturnsNotFound()
    {
        // Arrange
        var correlationId = "non-existent-id";

        _mockProcessingStatusService
            .Setup(x => x.GetStatusAsync(correlationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ProcessingStatus?)null);

        // Act
        var result = await _controller.WaitForCompletion(correlationId, 30);

        // Assert
        result.Should().NotBeNull();
        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task WaitForCompletion_WithInvalidTimeout_ReturnsBadRequest()
    {
        // Arrange
        var correlationId = "test-correlation-id";

        // Act - Test with too low timeout
        var result1 = await _controller.WaitForCompletion(correlationId, 0);

        // Act - Test with too high timeout
        var result2 = await _controller.WaitForCompletion(correlationId, 400);

        // Assert
        result1.Should().NotBeNull();
        result1.Result.Should().BeOfType<BadRequestObjectResult>();

        result2.Should().NotBeNull();
        result2.Result.Should().BeOfType<BadRequestObjectResult>();
    }
}
