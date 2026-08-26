using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Notifications;

public enum HomeAssistantPersistentNotificationUpdateType
{
    Current,
    Added,
    Updated,
    Removed,
    Unknown
}

/// <summary>A persistent notification stored by Home Assistant.</summary>
public sealed class HomeAssistantPersistentNotification
{
    [JsonPropertyName("notification_id")]
    public string NotificationId { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("created_at")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A live persistent-notification change.</summary>
public sealed class HomeAssistantPersistentNotificationUpdate
{
    public HomeAssistantPersistentNotificationUpdateType Type { get; set; }

    public string RawType { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, HomeAssistantPersistentNotification> Notifications { get; set; }
        = new Dictionary<string, HomeAssistantPersistentNotification>(StringComparer.OrdinalIgnoreCase);

    public JsonElement Raw { get; set; }
}
