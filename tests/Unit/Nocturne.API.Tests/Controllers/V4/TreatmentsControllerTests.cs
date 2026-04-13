using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Controllers.V4.Treatments;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Repositories;
using Nocturne.Core.Models;
using Nocturne.Core.Models.Authorization;
using Xunit;

namespace Nocturne.API.Tests.Controllers.V4;

public class TreatmentsControllerTests
{
    private readonly Mock<ITreatmentRepository> _mockRepository;
    private readonly Mock<IDocumentProcessingService> _mockDocumentProcessing;
    private readonly Mock<ITrackerTriggerService> _mockTrackerTrigger;
    private readonly Mock<ITrackerSuggestionService> _mockTrackerSuggestion;
    private readonly Mock<ISignalRBroadcastService> _mockBroadcast;
    private readonly Mock<ILogger<TreatmentsController>> _mockLogger;

    public TreatmentsControllerTests()
    {
        _mockRepository = new Mock<ITreatmentRepository>();
        _mockDocumentProcessing = new Mock<IDocumentProcessingService>();
        _mockTrackerTrigger = new Mock<ITrackerTriggerService>();
        _mockTrackerSuggestion = new Mock<ITrackerSuggestionService>();
        _mockBroadcast = new Mock<ISignalRBroadcastService>();
        _mockLogger = new Mock<ILogger<TreatmentsController>>();
    }

    private TreatmentsController CreateController(string? subjectId = null)
    {
        var controller = new TreatmentsController(
            _mockRepository.Object,
            _mockDocumentProcessing.Object,
            _mockTrackerTrigger.Object,
            _mockTrackerSuggestion.Object,
            _mockBroadcast.Object,
            _mockLogger.Object
        );

        var httpContext = new DefaultHttpContext();
        if (subjectId != null)
        {
            httpContext.Items["AuthContext"] = new AuthContext
            {
                IsAuthenticated = true,
                SubjectId = Guid.Parse(subjectId),
                SubjectName = "test-user",
            };
        }

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext,
        };

        return controller;
    }

    #region GetTreatments

    [Fact]
    public async Task GetTreatments_CountZero_ReturnsEmptyArray()
    {
        var controller = CreateController();

        var result = await controller.GetTreatments(count: 0);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var treatments = okResult.Value.Should().BeOfType<Treatment[]>().Subject;
        treatments.Should().BeEmpty();

        _mockRepository.Verify(
            r => r.GetTreatmentsWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GetTreatments_NegativeCount_ReturnsEmptyArray()
    {
        var controller = CreateController();

        var result = await controller.GetTreatments(count: -5);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var treatments = okResult.Value.Should().BeOfType<Treatment[]>().Subject;
        treatments.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTreatments_NegativeSkip_NormalizedToZero()
    {
        _mockRepository
            .Setup(r => r.GetTreatmentsWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Treatment>());

        var controller = CreateController();

        await controller.GetTreatments(skip: -10);

        _mockRepository.Verify(
            r => r.GetTreatmentsWithAdvancedFilterAsync(
                null, 100, 0, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTreatments_PassesEventTypeToRepository()
    {
        _mockRepository
            .Setup(r => r.GetTreatmentsWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Treatment>());

        var controller = CreateController();

        await controller.GetTreatments(eventType: "Meal Bolus", count: 50, skip: 10);

        _mockRepository.Verify(
            r => r.GetTreatmentsWithAdvancedFilterAsync(
                "Meal Bolus", 50, 10, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTreatments_PassesFindQueryToRepository()
    {
        _mockRepository
            .Setup(r => r.GetTreatmentsWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Treatment>());

        var controller = CreateController();

        await controller.GetTreatments(findQuery: "{\"eventType\":\"Site Change\"}");

        _mockRepository.Verify(
            r => r.GetTreatmentsWithAdvancedFilterAsync(
                null, 100, 0, "{\"eventType\":\"Site Change\"}", false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTreatments_ReturnsTreatmentsFromRepository()
    {
        var expected = new[]
        {
            new Treatment { Id = "t1", EventType = "Meal Bolus", Insulin = 5.0 },
            new Treatment { Id = "t2", EventType = "Site Change" },
        };

        _mockRepository
            .Setup(r => r.GetTreatmentsWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var controller = CreateController();

        var result = await controller.GetTreatments();

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var treatments = okResult.Value.Should().BeOfType<Treatment[]>().Subject;
        treatments.Should().HaveCount(2);
        treatments[0].Id.Should().Be("t1");
        treatments[1].Id.Should().Be("t2");
    }

    [Fact]
    public async Task GetTreatments_DefaultCount_Is100()
    {
        _mockRepository
            .Setup(r => r.GetTreatmentsWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Treatment>());

        var controller = CreateController();

        await controller.GetTreatments();

        _mockRepository.Verify(
            r => r.GetTreatmentsWithAdvancedFilterAsync(
                null, 100, 0, null, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetTreatments_AlwaysPassesReverseResultsFalse()
    {
        _mockRepository
            .Setup(r => r.GetTreatmentsWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Enumerable.Empty<Treatment>());

        var controller = CreateController();

        await controller.GetTreatments();

        _mockRepository.Verify(
            r => r.GetTreatmentsWithAdvancedFilterAsync(
                It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string?>(), false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    #endregion

    #region GetTreatment (by ID)

    [Fact]
    public async Task GetTreatment_Found_ReturnsOkWithTreatment()
    {
        var treatment = new Treatment { Id = "abc123", EventType = "Meal Bolus", Insulin = 3.5 };
        _mockRepository
            .Setup(r => r.GetTreatmentByIdAsync("abc123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(treatment);

        var controller = CreateController();

        var result = await controller.GetTreatment("abc123");

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<Treatment>().Subject;
        returned.Id.Should().Be("abc123");
        returned.Insulin.Should().Be(3.5);
    }

    [Fact]
    public async Task GetTreatment_NotFound_Returns404()
    {
        _mockRepository
            .Setup(r => r.GetTreatmentByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treatment?)null);

        var controller = CreateController();

        var result = await controller.GetTreatment("nonexistent");

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region CreateTreatment

    [Fact]
    public async Task CreateTreatment_NullBody_Returns400()
    {
        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.CreateTreatment(null!);

        var problemResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateTreatment_ProcessesTreatmentBeforeSaving()
    {
        var input = new Treatment { EventType = "Meal Bolus", Insulin = 5.0 };
        var processed = new Treatment { EventType = "Meal Bolus", Insulin = 5.0, Id = "processed-id" };
        var created = new Treatment { EventType = "Meal Bolus", Insulin = 5.0, Id = "created-id" };

        _mockDocumentProcessing
            .Setup(d => d.ProcessTreatment(input))
            .Returns(processed);
        _mockRepository
            .Setup(r => r.CreateTreatmentAsync(processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        await controller.CreateTreatment(input);

        _mockDocumentProcessing.Verify(d => d.ProcessTreatment(input), Times.Once);
        _mockRepository.Verify(r => r.CreateTreatmentAsync(processed, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateTreatment_Success_Returns201WithCreatedTreatment()
    {
        var input = new Treatment { EventType = "Meal Bolus" };
        var processed = new Treatment { EventType = "Meal Bolus" };
        var created = new Treatment { Id = "new-id", EventType = "Meal Bolus" };

        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(input)).Returns(processed);
        _mockRepository
            .Setup(r => r.CreateTreatmentAsync(processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.CreateTreatment(input);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.StatusCode.Should().Be(201);
        var returned = createdResult.Value.Should().BeOfType<Treatment>().Subject;
        returned.Id.Should().Be("new-id");
    }

    [Fact]
    public async Task CreateTreatment_RepositoryReturnsNull_Returns500()
    {
        var input = new Treatment { EventType = "Meal Bolus" };
        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(input)).Returns(input);
        _mockRepository
            .Setup(r => r.CreateTreatmentAsync(It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treatment?)null);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.CreateTreatment(input);

        var problemResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task CreateTreatment_TriggersTrackerProcessing()
    {
        var input = new Treatment { EventType = "Site Change" };
        var created = new Treatment { Id = "t1", EventType = "Site Change" };

        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(input)).Returns(input);
        _mockRepository
            .Setup(r => r.CreateTreatmentAsync(It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var userId = "00000000-0000-0000-0000-000000000001";
        var controller = CreateController(userId);

        await controller.CreateTreatment(input);

        _mockTrackerTrigger.Verify(
            t => t.ProcessTreatmentAsync(created, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTreatment_EvaluatesTrackerSuggestions()
    {
        var input = new Treatment { EventType = "Site Change" };
        var created = new Treatment { Id = "t1", EventType = "Site Change" };

        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(input)).Returns(input);
        _mockRepository
            .Setup(r => r.CreateTreatmentAsync(It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var userId = "00000000-0000-0000-0000-000000000001";
        var controller = CreateController(userId);

        await controller.CreateTreatment(input);

        _mockTrackerSuggestion.Verify(
            s => s.EvaluateTreatmentForTrackerSuggestionAsync(created, userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTreatment_BroadcastsViaSignalR()
    {
        var input = new Treatment { EventType = "Meal Bolus" };
        var created = new Treatment { Id = "t1", EventType = "Meal Bolus" };

        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(input)).Returns(input);
        _mockRepository
            .Setup(r => r.CreateTreatmentAsync(It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        await controller.CreateTreatment(input);

        _mockBroadcast.Verify(
            b => b.BroadcastStorageCreateAsync("treatments", created),
            Times.Once);
    }

    #endregion

    #region CreateTreatments (bulk)

    [Fact]
    public async Task CreateTreatments_NullBody_Returns400()
    {
        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.CreateTreatments(null!);

        var problemResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateTreatments_EmptyArray_Returns400()
    {
        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.CreateTreatments(Array.Empty<Treatment>());

        var problemResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateTreatments_Over1000_Returns400()
    {
        var treatments = Enumerable.Range(0, 1001)
            .Select(i => new Treatment { EventType = "Note" })
            .ToArray();

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.CreateTreatments(treatments);

        var problemResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateTreatments_ProcessesAllTreatments()
    {
        var treatments = new[]
        {
            new Treatment { EventType = "Meal Bolus" },
            new Treatment { EventType = "Site Change" },
        };
        var created = new[]
        {
            new Treatment { Id = "t1", EventType = "Meal Bolus" },
            new Treatment { Id = "t2", EventType = "Site Change" },
        };

        _mockDocumentProcessing
            .Setup(d => d.ProcessTreatment(It.IsAny<Treatment>()))
            .Returns<Treatment>(t => t);
        _mockRepository
            .Setup(r => r.CreateTreatmentsAsync(It.IsAny<IEnumerable<Treatment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        await controller.CreateTreatments(treatments);

        _mockDocumentProcessing.Verify(d => d.ProcessTreatment(It.IsAny<Treatment>()), Times.Exactly(2));
    }

    [Fact]
    public async Task CreateTreatments_Returns201WithCreatedArray()
    {
        var treatments = new[] { new Treatment { EventType = "Note" } };
        var created = new[] { new Treatment { Id = "t1", EventType = "Note" } };

        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(It.IsAny<Treatment>())).Returns<Treatment>(t => t);
        _mockRepository
            .Setup(r => r.CreateTreatmentsAsync(It.IsAny<IEnumerable<Treatment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.CreateTreatments(treatments);

        var statusResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        statusResult.StatusCode.Should().Be(201);
        var returned = statusResult.Value.Should().BeOfType<Treatment[]>().Subject;
        returned.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateTreatments_TriggersTrackerForAll()
    {
        var treatments = new[] { new Treatment { EventType = "Meal Bolus" } };
        var created = new[]
        {
            new Treatment { Id = "t1", EventType = "Meal Bolus" },
            new Treatment { Id = "t2", EventType = "Meal Bolus" },
        };

        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(It.IsAny<Treatment>())).Returns<Treatment>(t => t);
        _mockRepository
            .Setup(r => r.CreateTreatmentsAsync(It.IsAny<IEnumerable<Treatment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var userId = "00000000-0000-0000-0000-000000000001";
        var controller = CreateController(userId);

        await controller.CreateTreatments(treatments);

        _mockTrackerTrigger.Verify(
            t => t.ProcessTreatmentsAsync(It.Is<Treatment[]>(arr => arr.Length == 2), userId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreateTreatments_EvaluatesSuggestionsForEach()
    {
        var treatments = new[] { new Treatment { EventType = "Site Change" } };
        var created = new[]
        {
            new Treatment { Id = "t1", EventType = "Site Change" },
            new Treatment { Id = "t2", EventType = "Site Change" },
        };

        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(It.IsAny<Treatment>())).Returns<Treatment>(t => t);
        _mockRepository
            .Setup(r => r.CreateTreatmentsAsync(It.IsAny<IEnumerable<Treatment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var userId = "00000000-0000-0000-0000-000000000001";
        var controller = CreateController(userId);

        await controller.CreateTreatments(treatments);

        _mockTrackerSuggestion.Verify(
            s => s.EvaluateTreatmentForTrackerSuggestionAsync(It.IsAny<Treatment>(), userId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task CreateTreatments_BroadcastsEachViaSignalR()
    {
        var treatments = new[] { new Treatment { EventType = "Note" } };
        var created = new[]
        {
            new Treatment { Id = "t1", EventType = "Note" },
            new Treatment { Id = "t2", EventType = "Note" },
        };

        _mockDocumentProcessing.Setup(d => d.ProcessTreatment(It.IsAny<Treatment>())).Returns<Treatment>(t => t);
        _mockRepository
            .Setup(r => r.CreateTreatmentsAsync(It.IsAny<IEnumerable<Treatment>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(created);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        await controller.CreateTreatments(treatments);

        _mockBroadcast.Verify(
            b => b.BroadcastStorageCreateAsync("treatments", It.IsAny<Treatment>()),
            Times.Exactly(2));
    }

    #endregion

    #region UpdateTreatment

    [Fact]
    public async Task UpdateTreatment_NullBody_Returns400()
    {
        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.UpdateTreatment("some-id", null!);

        var problemResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        problemResult.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateTreatment_SetsIdFromRoute()
    {
        Treatment? capturedTreatment = null;
        var input = new Treatment { Id = "original-id", EventType = "Note" };
        var updated = new Treatment { Id = "route-id", EventType = "Note" };

        _mockRepository
            .Setup(r => r.UpdateTreatmentAsync("route-id", It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .Callback<string, Treatment, CancellationToken>((_, t, _) => capturedTreatment = t)
            .ReturnsAsync(updated);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        await controller.UpdateTreatment("route-id", input);

        input.Id.Should().Be("route-id");
    }

    [Fact]
    public async Task UpdateTreatment_Found_ReturnsOkWithUpdatedTreatment()
    {
        var input = new Treatment { EventType = "Note", Notes = "Updated" };
        var updated = new Treatment { Id = "t1", EventType = "Note", Notes = "Updated" };

        _mockRepository
            .Setup(r => r.UpdateTreatmentAsync("t1", It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.UpdateTreatment("t1", input);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var returned = okResult.Value.Should().BeOfType<Treatment>().Subject;
        returned.Id.Should().Be("t1");
        returned.Notes.Should().Be("Updated");
    }

    [Fact]
    public async Task UpdateTreatment_NotFound_Returns404()
    {
        _mockRepository
            .Setup(r => r.UpdateTreatmentAsync("nonexistent", It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treatment?)null);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.UpdateTreatment("nonexistent", new Treatment());

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task UpdateTreatment_BroadcastsViaSignalR()
    {
        var updated = new Treatment { Id = "t1", EventType = "Note" };
        _mockRepository
            .Setup(r => r.UpdateTreatmentAsync("t1", It.IsAny<Treatment>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(updated);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        await controller.UpdateTreatment("t1", new Treatment());

        _mockBroadcast.Verify(
            b => b.BroadcastStorageUpdateAsync("treatments", updated),
            Times.Once);
    }

    #endregion

    #region DeleteTreatment

    [Fact]
    public async Task DeleteTreatment_Found_Returns204()
    {
        var treatment = new Treatment { Id = "t1", EventType = "Note" };
        _mockRepository
            .Setup(r => r.GetTreatmentByIdAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(treatment);
        _mockRepository
            .Setup(r => r.DeleteTreatmentAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.DeleteTreatment("t1");

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeleteTreatment_TreatmentNotFound_Returns404()
    {
        _mockRepository
            .Setup(r => r.GetTreatmentByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treatment?)null);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.DeleteTreatment("nonexistent");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteTreatment_DeleteReturnsFalse_Returns404()
    {
        var treatment = new Treatment { Id = "t1" };
        _mockRepository
            .Setup(r => r.GetTreatmentByIdAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(treatment);
        _mockRepository
            .Setup(r => r.DeleteTreatmentAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        var result = await controller.DeleteTreatment("t1");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task DeleteTreatment_BroadcastsViaSignalR()
    {
        var treatment = new Treatment { Id = "t1", EventType = "Site Change" };
        _mockRepository
            .Setup(r => r.GetTreatmentByIdAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(treatment);
        _mockRepository
            .Setup(r => r.DeleteTreatmentAsync("t1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        await controller.DeleteTreatment("t1");

        _mockBroadcast.Verify(
            b => b.BroadcastStorageDeleteAsync("treatments", treatment),
            Times.Once);
    }

    [Fact]
    public async Task DeleteTreatment_NotFound_DoesNotBroadcast()
    {
        _mockRepository
            .Setup(r => r.GetTreatmentByIdAsync("nonexistent", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Treatment?)null);

        var controller = CreateController("00000000-0000-0000-0000-000000000001");

        await controller.DeleteTreatment("nonexistent");

        _mockBroadcast.Verify(
            b => b.BroadcastStorageDeleteAsync(It.IsAny<string>(), It.IsAny<object>()),
            Times.Never);
    }

    #endregion
}
