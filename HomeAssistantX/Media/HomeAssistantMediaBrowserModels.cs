using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Media;

/// <summary>A provider-neutral media node returned by Home Assistant media-source or media-player browsing.</summary>
public sealed class HomeAssistantMediaItem
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("media_class")]
    public string? MediaClass { get; set; }

    [JsonPropertyName("media_content_id")]
    public string MediaContentId { get; set; } = string.Empty;

    [JsonPropertyName("media_content_type")]
    public string MediaContentType { get; set; } = string.Empty;

    [JsonPropertyName("children_media_class")]
    public string? ChildrenMediaClass { get; set; }

    [JsonPropertyName("can_play")]
    public bool CanPlay { get; set; }

    [JsonPropertyName("can_expand")]
    public bool CanExpand { get; set; }

    [JsonPropertyName("can_search")]
    public bool CanSearch { get; set; }

    [JsonPropertyName("search_media_classes")]
    public IReadOnlyList<string>? SearchMediaClasses { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("not_shown")]
    public int NotShown { get; set; }

    [JsonPropertyName("children")]
    public IReadOnlyList<HomeAssistantMediaItem> Children { get; set; } = Array.Empty<HomeAssistantMediaItem>();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A media-search response including provider-specific response metadata.</summary>
public sealed class HomeAssistantMediaSearchResponse
{
    [JsonPropertyName("result")]
    public IReadOnlyList<HomeAssistantMediaItem> Items { get; set; } = Array.Empty<HomeAssistantMediaItem>();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantResolvedMedia
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}
