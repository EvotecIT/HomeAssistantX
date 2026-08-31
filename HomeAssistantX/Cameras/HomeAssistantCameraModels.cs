using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Models;

namespace HomeAssistantX.Cameras;

[Flags]
public enum HomeAssistantCameraFeature
{
    None = 0,
    OnOff = 1,
    Stream = 2
}

public enum HomeAssistantCameraOrientation
{
    NoTransform = 1,
    Mirror = 2,
    Rotate180 = 3,
    Flip = 4,
    RotateLeftAndFlip = 5,
    RotateLeft = 6,
    RotateRightAndFlip = 7,
    RotateRight = 8
}

public sealed class HomeAssistantCameraStatus
{
    public string EntityId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string State { get; set; } = string.Empty;
    public string? Brand { get; set; }
    public string? Model { get; set; }
    public bool? MotionDetectionEnabled { get; set; }
    public string? EntityPicture { get; set; }
    public HomeAssistantCameraFeature SupportedFeatures { get; set; }
    public HomeAssistantState RawState { get; set; } = new();
}

public sealed class HomeAssistantCameraCapabilities
{
    [JsonPropertyName("frontend_stream_types")]
    public IReadOnlyList<string> FrontendStreamTypes { get; set; } = Array.Empty<string>();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantCameraPreferences
{
    [JsonPropertyName("preload_stream")]
    public bool PreloadStream { get; set; }

    [JsonPropertyName("orientation")]
    public HomeAssistantCameraOrientation Orientation { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantCameraPreferencesUpdate
{
    public bool? PreloadStream { get; set; }
    public HomeAssistantCameraOrientation? Orientation { get; set; }

    internal IReadOnlyDictionary<string, object?> ToPayload(string entityId)
    {
        if (!PreloadStream.HasValue && !Orientation.HasValue)
        {
            throw new ArgumentException("At least one camera preference is required.");
        }

        var payload = new Dictionary<string, object?> { ["entity_id"] = entityId };
        if (PreloadStream.HasValue) payload["preload_stream"] = PreloadStream.Value;
        if (Orientation.HasValue)
        {
            if (!Enum.IsDefined(typeof(HomeAssistantCameraOrientation), Orientation.Value))
                throw new ArgumentOutOfRangeException(nameof(Orientation));
            payload["orientation"] = (int)Orientation.Value;
        }
        return payload;
    }
}

public sealed class HomeAssistantCameraStream
{
    [JsonIgnore]
    public string EntityId { get; set; } = string.Empty;

    [JsonIgnore]
    public string Format { get; set; } = "hls";

    [JsonPropertyName("url")]
    public string Path { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantCameraStateChange
{
    public HomeAssistantCameraStateChange(string entityId, HomeAssistantCameraStatus? previous, HomeAssistantCameraStatus? current)
    {
        EntityId = entityId;
        Previous = previous;
        Current = current;
    }

    public string EntityId { get; }
    public HomeAssistantCameraStatus? Previous { get; }
    public HomeAssistantCameraStatus? Current { get; }
}
