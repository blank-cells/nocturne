using Microsoft.Extensions.Logging;
using Nocturne.Connectors.HomeAssistant.Configurations;
using Nocturne.Connectors.HomeAssistant.Services;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts;
using Nocturne.Core.Contracts.Events;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.HomeAssistant.WriteBack;

/// <summary>
/// Pushes Nocturne data to Home Assistant entities when new glucose entries arrive.
/// Implements IDataEventSink&lt;Entry&gt; to piggyback on glucose domain events.
/// </summary>
public class HomeAssistantWriteBackSink(
    IHomeAssistantApiClient apiClient,
    HomeAssistantConnectorConfiguration config,
    IDeviceStatusService deviceStatusService,
    ILogger<HomeAssistantWriteBackSink> logger) : IDataEventSink<Entry>
{
    private static readonly TimeSpan StalenessThreshold = TimeSpan.FromMinutes(10);

    // Cache the device status for the duration of one write-back cycle
    private DeviceStatus? _cachedDeviceStatus;
    private bool _deviceStatusFetched;

    public async Task OnCreatedAsync(Entry item, CancellationToken ct = default)
    {
        if (!config.WriteBackEnabled)
            return;

        // Prevent sync loop: HA → Nocturne → write-back → HA → repeat
        if (item.DataSource == DataSources.HomeAssistantConnector)
            return;

        if (IsStale(item))
            return;

        // Reset cache for this write-back cycle
        _deviceStatusFetched = false;
        _cachedDeviceStatus = null;

        if (config.WriteBackTypes.Contains(WriteBackDataType.Glucose))
            await PushGlucoseAsync(item, ct);

        if (config.WriteBackTypes.Contains(WriteBackDataType.Iob))
            await PushIobAsync(ct);

        if (config.WriteBackTypes.Contains(WriteBackDataType.Cob))
            await PushCobAsync(ct);

        if (config.WriteBackTypes.Contains(WriteBackDataType.PredictedBg))
            await PushPredictedBgAsync(ct);

        if (config.WriteBackTypes.Contains(WriteBackDataType.LoopStatus))
            await PushLoopStatusAsync(ct);
    }

    public async Task OnCreatedAsync(IReadOnlyList<Entry> items, CancellationToken ct = default)
    {
        if (!config.WriteBackEnabled || items.Count == 0)
            return;

        var latest = items.MaxBy(e => e.Mills);
        if (latest != null)
            await OnCreatedAsync(latest, ct);
    }

    private static bool IsStale(Entry entry)
    {
        var entryTime = DateTimeOffset.FromUnixTimeMilliseconds(entry.Mills);
        return DateTimeOffset.UtcNow - entryTime > StalenessThreshold;
    }

    private async Task PushGlucoseAsync(Entry entry, CancellationToken ct)
    {
        try
        {
            var attributes = new Dictionary<string, object>
            {
                ["unit_of_measurement"] = "mg/dL",
                ["device_class"] = "blood_glucose",
                ["friendly_name"] = "Nocturne Glucose",
                ["icon"] = "mdi:diabetes",
                ["trend"] = entry.Direction ?? "Unknown",
                ["last_updated"] = DateTimeOffset.UtcNow.ToString("o")
            };

            await apiClient.SetStateAsync("sensor.nocturne_glucose",
                entry.Sgv?.ToString() ?? "0", attributes, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push glucose to HA");
        }
    }

    private async Task PushIobAsync(CancellationToken ct)
    {
        try
        {
            var ds = await GetLatestDeviceStatusAsync(ct);
            var iob = ds?.Loop?.Iob?.Iob ?? ds?.OpenAps?.Iob?.Iob;
            if (iob == null) return;

            var attributes = new Dictionary<string, object>
            {
                ["unit_of_measurement"] = "U",
                ["friendly_name"] = "Nocturne IOB",
                ["icon"] = "mdi:needle",
                ["last_updated"] = DateTimeOffset.UtcNow.ToString("o")
            };

            await apiClient.SetStateAsync("sensor.nocturne_iob",
                iob.Value.ToString("F2"), attributes, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push IOB to HA");
        }
    }

    private async Task PushCobAsync(CancellationToken ct)
    {
        try
        {
            var ds = await GetLatestDeviceStatusAsync(ct);
            var cob = ds?.Loop?.Cob?.Cob ?? ds?.OpenAps?.Suggested?.COB;
            if (cob == null) return;

            var attributes = new Dictionary<string, object>
            {
                ["unit_of_measurement"] = "g",
                ["friendly_name"] = "Nocturne COB",
                ["icon"] = "mdi:food-apple",
                ["last_updated"] = DateTimeOffset.UtcNow.ToString("o")
            };

            await apiClient.SetStateAsync("sensor.nocturne_cob",
                cob.Value.ToString("F1"), attributes, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push COB to HA");
        }
    }

    private async Task PushPredictedBgAsync(CancellationToken ct)
    {
        try
        {
            var ds = await GetLatestDeviceStatusAsync(ct);
            var predicted = ds?.Loop?.Predicted?.Values;
            if (predicted == null || predicted.Length == 0) return;

            // Last predicted value = eventual BG
            var eventualBg = predicted[^1];

            var attributes = new Dictionary<string, object>
            {
                ["unit_of_measurement"] = "mg/dL",
                ["device_class"] = "blood_glucose",
                ["friendly_name"] = "Nocturne Predicted BG",
                ["icon"] = "mdi:crystal-ball",
                ["prediction_points"] = predicted.Length,
                ["last_updated"] = DateTimeOffset.UtcNow.ToString("o")
            };

            await apiClient.SetStateAsync("sensor.nocturne_predicted_bg",
                eventualBg.ToString("F0"), attributes, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push predicted BG to HA");
        }
    }

    private async Task PushLoopStatusAsync(CancellationToken ct)
    {
        try
        {
            var ds = await GetLatestDeviceStatusAsync(ct);
            var loop = ds?.Loop;

            var isEnacted = loop?.Enacted != null;
            var state = isEnacted ? "enacted" : (loop != null ? "open" : "unknown");

            var attributes = new Dictionary<string, object>
            {
                ["friendly_name"] = "Nocturne Loop Status",
                ["icon"] = "mdi:sync",
                ["last_updated"] = DateTimeOffset.UtcNow.ToString("o")
            };

            if (loop?.Enacted != null)
            {
                if (loop.Enacted.Rate != null)
                    attributes["enacted_rate"] = loop.Enacted.Rate;
                if (loop.Enacted.Duration != null)
                    attributes["enacted_duration"] = loop.Enacted.Duration;
                if (loop.Enacted.Reason != null)
                    attributes["reason"] = loop.Enacted.Reason;
            }

            if (loop?.FailureReason != null)
            {
                state = "failed";
                attributes["failure_reason"] = loop.FailureReason;
            }

            await apiClient.SetStateAsync("sensor.nocturne_loop_status",
                state, attributes, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push loop status to HA");
        }
    }

    private async Task<DeviceStatus?> GetLatestDeviceStatusAsync(CancellationToken ct)
    {
        if (!_deviceStatusFetched)
        {
            var statuses = await deviceStatusService.GetRecentDeviceStatusAsync(1, ct);
            _cachedDeviceStatus = statuses.FirstOrDefault();
            _deviceStatusFetched = true;
        }
        return _cachedDeviceStatus;
    }
}
