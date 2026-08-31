using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Dashboards;

public enum HomeAssistantDashboardResourceType
{
    JavaScript,
    Css,
    Module,
    Html
}

public sealed class HomeAssistantPanel
{
    [JsonPropertyName("url_path")]
    public string UrlPath { get; set; } = string.Empty;

    [JsonPropertyName("component_name")]
    public string? ComponentName { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("require_admin")]
    public bool RequireAdmin { get; set; }

    [JsonPropertyName("show_in_sidebar")]
    public bool ShowInSidebar { get; set; }

    [JsonPropertyName("default_visible")]
    public bool DefaultVisible { get; set; }

    [JsonPropertyName("config")]
    public JsonElement? Configuration { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantDashboard
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url_path")]
    public string UrlPath { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("show_in_sidebar")]
    public bool ShowInSidebar { get; set; }

    [JsonPropertyName("require_admin")]
    public bool RequireAdmin { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("filename")]
    public string? FileName { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantDashboardCreate
{
    public string UrlPath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public bool ShowInSidebar { get; set; } = true;
    public bool RequireAdmin { get; set; }
    public bool AllowSingleWord { get; set; }
}

public sealed class HomeAssistantDashboardUpdate
{
    public string? Title { get; set; }
    public string? Icon { get; set; }
    public bool RemoveIcon { get; set; }
    public bool? ShowInSidebar { get; set; }
    public bool? RequireAdmin { get; set; }
}

public sealed class HomeAssistantDashboardResource
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantLovelaceInfo
{
    [JsonPropertyName("resource_mode")]
    public string ResourceMode { get; set; } = string.Empty;

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}
