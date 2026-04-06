using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.Connectors.HomeAssistant.Configurations;
using Nocturne.Connectors.HomeAssistant.Services;
using Nocturne.Connectors.HomeAssistant.WriteBack;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.Connectors.HomeAssistant.Tests.WriteBack;

public class HomeAssistantWriteBackSinkTests
{
    private readonly Mock<IHomeAssistantApiClient> _apiClientMock = new();
    private readonly Mock<IDeviceStatusService> _deviceStatusServiceMock = new();
    private readonly Mock<ILogger<HomeAssistantWriteBackSink>> _loggerMock = new();

    private HomeAssistantWriteBackSink CreateSink(
        bool writeBackEnabled = true,
        HashSet<WriteBackDataType>? writeBackTypes = null)
    {
        var config = new HomeAssistantConnectorConfiguration
        {
            WriteBackEnabled = writeBackEnabled,
            WriteBackTypes = writeBackTypes ?? [WriteBackDataType.Glucose]
        };

        return new HomeAssistantWriteBackSink(
            _apiClientMock.Object, config, _deviceStatusServiceMock.Object, _loggerMock.Object);
    }

    private static Entry CreateRecentEntry(double sgv = 120, string direction = "Flat")
    {
        return new Entry
        {
            Mills = DateTimeOffset.UtcNow.AddSeconds(-30).ToUnixTimeMilliseconds(),
            Sgv = sgv,
            Direction = direction
        };
    }

    private static Entry CreateStaleEntry()
    {
        return new Entry
        {
            Mills = DateTimeOffset.UtcNow.AddMinutes(-15).ToUnixTimeMilliseconds(),
            Sgv = 100,
            Direction = "Flat"
        };
    }

    private void SetupDeviceStatus(DeviceStatus? deviceStatus)
    {
        var statuses = deviceStatus != null
            ? new List<DeviceStatus> { deviceStatus }
            : new List<DeviceStatus>();

        _deviceStatusServiceMock
            .Setup(x => x.GetRecentDeviceStatusAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(statuses);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenWriteBackDisabled_DoesNothing()
    {
        var sink = CreateSink(writeBackEnabled: false);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenEntryFromHomeAssistant_SkipsToPreventSyncLoop()
    {
        var sink = CreateSink();
        var entry = CreateRecentEntry();
        entry.DataSource = DataSources.HomeAssistantConnector;

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenEntryIsStale_DoesNothing()
    {
        var sink = CreateSink();
        var entry = CreateStaleEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenGlucoseEnabled_PushesGlucoseState()
    {
        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.Glucose]);
        var entry = CreateRecentEntry(sgv: 145, direction: "FortyFiveUp");

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_glucose",
                It.Is<string>(s => s.StartsWith("145")),
                It.Is<Dictionary<string, object>>(d =>
                    d["unit_of_measurement"].Equals("mg/dL") &&
                    d["trend"].Equals("FortyFiveUp")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenGlucoseNotInWriteBackTypes_SkipsGlucose()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            Loop = new LoopStatus { Iob = new LoopIob { Iob = 2.5 } }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.Iob]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_glucose",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task OnCreatedAsync_IndividualFailureDoesNotBlockOthers()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            Loop = new LoopStatus { Iob = new LoopIob { Iob = 1.0 } }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(
                "sensor.nocturne_glucose",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        _apiClientMock
            .Setup(x => x.SetStateAsync(
                "sensor.nocturne_iob",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes:
        [
            WriteBackDataType.Glucose,
            WriteBackDataType.Iob
        ]);
        var entry = CreateRecentEntry();

        // Should not throw even though glucose push fails
        var act = () => sink.OnCreatedAsync(entry);
        await act.Should().NotThrowAsync();

        // IOB should still be pushed despite glucose failure
        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_iob",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_BatchUsesLatestEntry()
    {
        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.Glucose]);

        var older = new Entry
        {
            Mills = DateTimeOffset.UtcNow.AddSeconds(-60).ToUnixTimeMilliseconds(),
            Sgv = 100,
            Direction = "Flat"
        };
        var latest = new Entry
        {
            Mills = DateTimeOffset.UtcNow.AddSeconds(-10).ToUnixTimeMilliseconds(),
            Sgv = 180,
            Direction = "SingleUp"
        };

        await sink.OnCreatedAsync(new List<Entry> { older, latest });

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_glucose",
                "180",
                It.Is<Dictionary<string, object>>(d => d["trend"].Equals("SingleUp")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenIobEnabled_PushesIobFromDeviceStatus()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            Loop = new LoopStatus
            {
                Iob = new LoopIob { Iob = 2.55 }
            }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.Iob]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_iob",
                "2.55",
                It.Is<Dictionary<string, object>>(d =>
                    d["unit_of_measurement"].Equals("U") &&
                    d["friendly_name"].Equals("Nocturne IOB")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenIobEnabled_FallsBackToOpenAps()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            OpenAps = new OpenApsStatus
            {
                Iob = new OpenApsIobData { Iob = 3.14 }
            }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.Iob]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_iob",
                "3.14",
                It.Is<Dictionary<string, object>>(d => d["unit_of_measurement"].Equals("U")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenCobEnabled_PushesCobFromDeviceStatus()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            Loop = new LoopStatus
            {
                Cob = new LoopCob { Cob = 45.3 }
            }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.Cob]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_cob",
                "45.3",
                It.Is<Dictionary<string, object>>(d =>
                    d["unit_of_measurement"].Equals("g") &&
                    d["friendly_name"].Equals("Nocturne COB")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenPredictedBgEnabled_PushesEventualBg()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            Loop = new LoopStatus
            {
                Predicted = new LoopPredicted
                {
                    Values = [120.0, 115.0, 110.0, 105.0, 100.0]
                }
            }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.PredictedBg]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_predicted_bg",
                "100",
                It.Is<Dictionary<string, object>>(d =>
                    d["unit_of_measurement"].Equals("mg/dL") &&
                    d["prediction_points"].Equals(5)),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenLoopStatusEnabled_PushesEnactedState()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            Loop = new LoopStatus
            {
                Enacted = new LoopEnacted
                {
                    Rate = 1.5,
                    Duration = 30,
                    Reason = "High BG"
                }
            }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.LoopStatus]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_loop_status",
                "enacted",
                It.Is<Dictionary<string, object>>(d =>
                    d["enacted_rate"].Equals(1.5) &&
                    d["enacted_duration"].Equals(30) &&
                    d["reason"].Equals("High BG")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenLoopFailed_PushesFailedState()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            Loop = new LoopStatus
            {
                FailureReason = "Pump unreachable"
            }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes: [WriteBackDataType.LoopStatus]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_loop_status",
                "failed",
                It.Is<Dictionary<string, object>>(d =>
                    d["failure_reason"].Equals("Pump unreachable")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_WhenNoDeviceStatus_SkipsComputedPushes()
    {
        SetupDeviceStatus(null);

        var sink = CreateSink(writeBackTypes:
        [
            WriteBackDataType.Iob,
            WriteBackDataType.Cob,
            WriteBackDataType.PredictedBg,
            WriteBackDataType.LoopStatus
        ]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        // IOB, COB, PredictedBg should not be pushed (null values)
        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_iob",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_cob",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_predicted_bg",
                It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Never);

        // Loop status still pushes "unknown" when no device status
        _apiClientMock.Verify(
            x => x.SetStateAsync(
                "sensor.nocturne_loop_status",
                "unknown",
                It.IsAny<Dictionary<string, object>>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task OnCreatedAsync_CachesDeviceStatusAcrossPushes()
    {
        SetupDeviceStatus(new DeviceStatus
        {
            Loop = new LoopStatus
            {
                Iob = new LoopIob { Iob = 2.0 },
                Cob = new LoopCob { Cob = 30.0 },
                Predicted = new LoopPredicted { Values = [120.0, 110.0] },
                Enacted = new LoopEnacted { Rate = 1.0, Duration = 30 }
            }
        });

        _apiClientMock
            .Setup(x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var sink = CreateSink(writeBackTypes:
        [
            WriteBackDataType.Iob,
            WriteBackDataType.Cob,
            WriteBackDataType.PredictedBg,
            WriteBackDataType.LoopStatus
        ]);
        var entry = CreateRecentEntry();

        await sink.OnCreatedAsync(entry);

        // All 4 computed types pushed
        _apiClientMock.Verify(
            x => x.SetStateAsync(It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<Dictionary<string, object>>(), It.IsAny<CancellationToken>()),
            Times.Exactly(4));

        // But GetRecentDeviceStatusAsync called only once (cached)
        _deviceStatusServiceMock.Verify(
            x => x.GetRecentDeviceStatusAsync(1, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
