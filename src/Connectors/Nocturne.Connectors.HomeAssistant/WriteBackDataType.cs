using System.Text.Json.Serialization;

namespace Nocturne.Connectors.HomeAssistant;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WriteBackDataType
{
    Glucose,
    Iob,
    Cob,
    PredictedBg,
    LoopStatus
}
