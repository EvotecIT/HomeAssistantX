using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Supervisor;

public enum HomeAssistantSupervisorLogTarget
{
    Core,
    Supervisor,
    Host,
    App
}

public enum HomeAssistantSupervisorUpdateTarget
{
    Core,
    Supervisor,
    OperatingSystem,
    App
}

public enum HomeAssistantSupervisorRestartTarget
{
    Core,
    Supervisor,
    Host
}

public enum HomeAssistantAppOperation
{
    Install,
    Update,
    Start,
    Stop,
    Restart,
    Uninstall
}

/// <summary>Options for direct Supervisor API access from Home Assistant apps or trusted local tooling.</summary>
public sealed class HomeAssistantSupervisorClientOptions
{
    public HomeAssistantSupervisorClientOptions(
        Uri baseUri,
        Authentication.IHomeAssistantAccessTokenProvider accessTokenProvider)
    {
        BaseUri = Configuration.HomeAssistantUri.NormalizeBaseUri(baseUri);
        AccessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
    }

    public Uri BaseUri { get; }

    public Authentication.IHomeAssistantAccessTokenProvider AccessTokenProvider { get; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public int MaximumResponseBytes { get; set; } = 64 * 1024 * 1024;
}

public sealed class HomeAssistantSupervisorInfo
{
    [JsonPropertyName("supervisor")]
    public string? SupervisorVersion { get; set; }

    [JsonPropertyName("homeassistant")]
    public string? CoreVersion { get; set; }

    [JsonPropertyName("hassos")]
    public string? OperatingSystemVersion { get; set; }

    [JsonPropertyName("hostname")]
    public string? Hostname { get; set; }

    [JsonPropertyName("operating_system")]
    public string? OperatingSystem { get; set; }

    [JsonPropertyName("machine")]
    public string? Machine { get; set; }

    [JsonPropertyName("arch")]
    public string? Architecture { get; set; }

    [JsonPropertyName("supported")]
    public bool Supported { get; set; }

    [JsonPropertyName("channel")]
    public string? Channel { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("features")]
    public string[] Features { get; set; } = Array.Empty<string>();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HomeAssistantSupervisorUpdate
{
    [JsonPropertyName("update_type")]
    public string UpdateType { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("version_latest")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("panel_path")]
    public string? PanelPath { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HomeAssistantApp
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("version_latest")]
    public string? LatestVersion { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("installed")]
    public bool Installed { get; set; }

    [JsonPropertyName("available")]
    public bool Available { get; set; }

    [JsonPropertyName("update_available")]
    public bool UpdateAvailable { get; set; }

    [JsonPropertyName("repository")]
    public string? Repository { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HomeAssistantBackup
{
    [JsonPropertyName("slug")]
    public string Slug { get; set; } = string.Empty;

    [JsonPropertyName("date")]
    public DateTimeOffset? Date { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("size")]
    public double? Size { get; set; }

    [JsonPropertyName("protected")]
    public bool Protected { get; set; }

    [JsonPropertyName("compressed")]
    public bool Compressed { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("content")]
    public JsonElement Content { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HomeAssistantSupervisorJob
{
    [JsonPropertyName("uuid")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("reference")]
    public string? Reference { get; set; }

    [JsonPropertyName("progress")]
    public double? Progress { get; set; }

    [JsonPropertyName("stage")]
    public string? Stage { get; set; }

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("extra")]
    public JsonElement Extra { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HomeAssistantBackupRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("compressed")]
    public bool? Compressed { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }

    [JsonPropertyName("homeassistant_exclude_database")]
    public bool? ExcludeDatabase { get; set; }

    [JsonPropertyName("background")]
    public bool? Background { get; set; }
}
