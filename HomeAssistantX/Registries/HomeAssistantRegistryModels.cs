using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Registries;

public sealed class HomeAssistantArea
{
    [JsonPropertyName("area_id")]
    public string AreaId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("aliases")]
    public string[] Aliases { get; set; } = Array.Empty<string>();

    [JsonPropertyName("floor_id")]
    public string? FloorId { get; set; }

    [JsonPropertyName("labels")]
    public string[] Labels { get; set; } = Array.Empty<string>();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantFloor
{
    [JsonPropertyName("floor_id")]
    public string FloorId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("aliases")]
    public string[] Aliases { get; set; } = Array.Empty<string>();

    [JsonPropertyName("level")]
    public int? Level { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantDeviceRegistryEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("area_id")]
    public string? AreaId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("name_by_user")]
    public string? NameByUser { get; set; }

    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    [JsonPropertyName("model")]
    public string? Model { get; set; }

    [JsonPropertyName("via_device_id")]
    public string? ViaDeviceId { get; set; }

    [JsonPropertyName("config_entries")]
    public string[] ConfigEntries { get; set; } = Array.Empty<string>();

    [JsonPropertyName("labels")]
    public string[] Labels { get; set; } = Array.Empty<string>();

    [JsonPropertyName("identifiers")]
    public JsonElement Identifiers { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantEntityRegistryEntry
{
    [JsonPropertyName("entity_id")]
    public string EntityId { get; set; } = string.Empty;

    [JsonPropertyName("unique_id")]
    public string? UniqueId { get; set; }

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }

    [JsonPropertyName("config_entry_id")]
    public string? ConfigEntryId { get; set; }

    [JsonPropertyName("device_class")]
    public string? DeviceClass { get; set; }

    [JsonPropertyName("capabilities")]
    public JsonElement Capabilities { get; set; }

    [JsonPropertyName("device_id")]
    public string? DeviceId { get; set; }

    [JsonPropertyName("area_id")]
    public string? AreaId { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("aliases")]
    public string?[] Aliases { get; set; } = Array.Empty<string?>();

    [JsonPropertyName("has_entity_name")]
    public bool HasEntityName { get; set; }

    [JsonPropertyName("original_name")]
    public string? OriginalName { get; set; }

    [JsonPropertyName("disabled_by")]
    public string? DisabledBy { get; set; }

    [JsonPropertyName("hidden_by")]
    public string? HiddenBy { get; set; }

    [JsonPropertyName("entity_category")]
    public string? EntityCategory { get; set; }

    [JsonPropertyName("labels")]
    public string[] Labels { get; set; } = Array.Empty<string>();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

public sealed class HomeAssistantConfigEntry
{
    [JsonPropertyName("entry_id")]
    public string EntryId { get; set; } = string.Empty;

    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("state")]
    public string? State { get; set; }

    [JsonPropertyName("supports_options")]
    public bool SupportsOptions { get; set; }

    [JsonPropertyName("supports_remove_device")]
    public bool SupportsRemoveDevice { get; set; }

    [JsonPropertyName("supports_unload")]
    public bool SupportsUnload { get; set; }

    [JsonPropertyName("supports_reconfigure")]
    public bool SupportsReconfigure { get; set; }

    [JsonPropertyName("pref_disable_new_entities")]
    public bool PreferDisableNewEntities { get; set; }

    [JsonPropertyName("pref_disable_polling")]
    public bool PreferDisablePolling { get; set; }

    [JsonPropertyName("disabled_by")]
    public string? DisabledBy { get; set; }

    [JsonPropertyName("reason")]
    public string? Reason { get; set; }

    [JsonPropertyName("error_reason_translation_key")]
    public string? ErrorReasonTranslationKey { get; set; }

    [JsonPropertyName("error_reason_translation_placeholders")]
    public Dictionary<string, string>? ErrorReasonTranslationPlaceholders { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A user-defined Home Assistant label.</summary>
public sealed class HomeAssistantLabel
{
    [JsonPropertyName("label_id")]
    public string LabelId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("color")]
    public string? Color { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("created_at")]
    public double? CreatedAtEpochSeconds { get; set; }

    [JsonPropertyName("modified_at")]
    public double? ModifiedAtEpochSeconds { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A Home Assistant category within an explicit registry scope.</summary>
public sealed class HomeAssistantCategory
{
    [JsonPropertyName("category_id")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }

    [JsonPropertyName("created_at")]
    public double? CreatedAtEpochSeconds { get; set; }

    [JsonPropertyName("modified_at")]
    public double? ModifiedAtEpochSeconds { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Fields used to create a Home Assistant label.</summary>
public sealed class HomeAssistantLabelCreate
{
    public HomeAssistantLabelCreate(string name)
    {
        Name = HomeAssistantRegistryValidation.Require(name, nameof(name));
    }

    public string Name { get; }

    public string? Color { get; set; }

    public string? Description { get; set; }

    public string? Icon { get; set; }
}

/// <summary>A tri-state label update. Fluent methods distinguish omission from clearing a nullable field.</summary>
public sealed class HomeAssistantLabelUpdate
{
    private readonly Dictionary<string, object?> _changes = new(StringComparer.Ordinal);

    public HomeAssistantLabelUpdate WithName(string name)
    {
        _changes["name"] = HomeAssistantRegistryValidation.Require(name, nameof(name));
        return this;
    }

    public HomeAssistantLabelUpdate WithColor(string? color)
    {
        _changes["color"] = color;
        return this;
    }

    public HomeAssistantLabelUpdate WithDescription(string? description)
    {
        _changes["description"] = description;
        return this;
    }

    public HomeAssistantLabelUpdate WithIcon(string? icon)
    {
        _changes["icon"] = icon;
        return this;
    }

    internal IReadOnlyDictionary<string, object?> GetChanges() => _changes;
}

/// <summary>Fields used to create a Home Assistant category.</summary>
public sealed class HomeAssistantCategoryCreate
{
    public HomeAssistantCategoryCreate(string name)
    {
        Name = HomeAssistantRegistryValidation.Require(name, nameof(name));
    }

    public string Name { get; }

    public string? Icon { get; set; }
}

/// <summary>A tri-state category update. Fluent methods distinguish omission from clearing the icon.</summary>
public sealed class HomeAssistantCategoryUpdate
{
    private readonly Dictionary<string, object?> _changes = new(StringComparer.Ordinal);

    public HomeAssistantCategoryUpdate WithName(string name)
    {
        _changes["name"] = HomeAssistantRegistryValidation.Require(name, nameof(name));
        return this;
    }

    public HomeAssistantCategoryUpdate WithIcon(string? icon)
    {
        _changes["icon"] = icon;
        return this;
    }

    internal IReadOnlyDictionary<string, object?> GetChanges() => _changes;
}

internal static class HomeAssistantRegistryValidation
{
    public static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        return value;
    }
}

/// <summary>A coherent snapshot of the registries used to map raw entities into rooms and devices.</summary>
public sealed class HomeAssistantRegistrySnapshot
{
    public IReadOnlyList<HomeAssistantArea> Areas { get; set; } = Array.Empty<HomeAssistantArea>();

    public IReadOnlyList<HomeAssistantFloor> Floors { get; set; } = Array.Empty<HomeAssistantFloor>();

    public IReadOnlyList<HomeAssistantDeviceRegistryEntry> Devices { get; set; } = Array.Empty<HomeAssistantDeviceRegistryEntry>();

    public IReadOnlyList<HomeAssistantEntityRegistryEntry> Entities { get; set; } = Array.Empty<HomeAssistantEntityRegistryEntry>();

    public IReadOnlyList<HomeAssistantConfigEntry> ConfigEntries { get; set; } = Array.Empty<HomeAssistantConfigEntry>();

    public IReadOnlyList<HomeAssistantLabel> Labels { get; set; } = Array.Empty<HomeAssistantLabel>();

    /// <summary>Whether configuration-entry enrichment was available to the current Home Assistant user.</summary>
    public bool IsConfigEntryEnrichmentAvailable { get; set; } = true;

    /// <summary>Whether the connected Home Assistant installation exposes the label registry.</summary>
    public bool IsLabelRegistryAvailable { get; set; } = true;
}
