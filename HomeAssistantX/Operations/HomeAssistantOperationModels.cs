using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Models;

namespace HomeAssistantX.Operations;

/// <summary>Describes whether an optional Home Assistant capability can be used by the current connection.</summary>
public enum HomeAssistantCapabilityAvailability
{
    Unknown,
    Available,
    NotInstalled,
    NotAuthorized,
    Unavailable
}

/// <summary>A single capability result with a stable name and a non-secret diagnostic detail.</summary>
public sealed class HomeAssistantCapability
{
    public string Name { get; set; } = string.Empty;

    public HomeAssistantCapabilityAvailability Availability { get; set; }

    public string? Detail { get; set; }
}

/// <summary>Installation and permission-aware capabilities discovered without changing Home Assistant.</summary>
public sealed class HomeAssistantCapabilityReport
{
    public string? CoreVersion { get; set; }

    public string? InstallationType { get; set; }

    public bool? IsSupervisorManaged { get; set; }

    public IReadOnlyList<string> LoadedComponents { get; set; } = Array.Empty<string>();

    public IReadOnlyList<HomeAssistantCapability> Capabilities { get; set; } = Array.Empty<HomeAssistantCapability>();
}

/// <summary>A structured entry returned by Home Assistant's in-memory system log.</summary>
public sealed class HomeAssistantSystemLogEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string[] Message { get; set; } = Array.Empty<string>();

    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public JsonElement Source { get; set; }

    [JsonPropertyName("exception")]
    public string? Exception { get; set; }

    [JsonPropertyName("count")]
    public int Count { get; set; }

    [JsonPropertyName("timestamp")]
    public double Timestamp { get; set; }

    [JsonPropertyName("first_occurred")]
    public double FirstOccurred { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>An issue from Home Assistant's repairs registry.</summary>
public sealed class HomeAssistantRepairIssue
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("issue_domain")]
    public string? IssueDomain { get; set; }

    [JsonPropertyName("issue_id")]
    public string IssueId { get; set; } = string.Empty;

    [JsonPropertyName("active")]
    public bool Active { get; set; }

    [JsonPropertyName("is_fixable")]
    public bool IsFixable { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("ignored")]
    public bool Ignored { get; set; }

    [JsonPropertyName("created")]
    public DateTimeOffset? Created { get; set; }

    [JsonPropertyName("breaks_in_ha_version")]
    public string? BreaksInHomeAssistantVersion { get; set; }

    [JsonPropertyName("learn_more_url")]
    public string? LearnMoreUrl { get; set; }

    [JsonPropertyName("translation_key")]
    public string? TranslationKey { get; set; }

    [JsonPropertyName("translation_placeholders")]
    public Dictionary<string, string>? TranslationPlaceholders { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A completed snapshot emitted by the system health subscription.</summary>
public sealed class HomeAssistantSystemHealthSnapshot
{
    public IReadOnlyDictionary<string, JsonElement> Domains { get; set; } =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A short automation or script trace returned by Home Assistant.</summary>
public sealed class HomeAssistantTraceSummary
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("item_id")]
    public string ItemId { get; set; } = string.Empty;

    [JsonPropertyName("run_id")]
    public string RunId { get; set; } = string.Empty;

    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty;

    [JsonPropertyName("script_execution")]
    public string? ScriptExecution { get; set; }

    [JsonPropertyName("last_step")]
    public string? LastStep { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("timestamp")]
    public JsonElement Timestamp { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>An update entity together with normalized version and progress fields.</summary>
public sealed class HomeAssistantUpdate
{
    public HomeAssistantState State { get; set; } = new();

    public string EntityId => State.EntityId;

    public string? Title { get; set; }

    public string? InstalledVersion { get; set; }

    public string? LatestVersion { get; set; }

    public bool IsAvailable { get; set; }

    public bool IsInProgress { get; set; }

    public double? ProgressPercentage { get; set; }
}

/// <summary>The result of an integration operation that may require a Core restart.</summary>
public sealed class HomeAssistantIntegrationOperationResult
{
    [JsonPropertyName("require_restart")]
    public bool RequiresRestart { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
