using System.Text.Json;
using HomeAssistantX.Models;
using HomeAssistantX.Registries;
using HomeAssistantX.Services;

namespace HomeAssistantX.Inventory;

/// <summary>A floor joined with the areas, devices, and entities assigned to it.</summary>
public sealed class HomeAssistantFloorInfo
{
    public string FloorId { get; internal set; } = string.Empty;

    public string Name { get; internal set; } = string.Empty;

    public int? Level { get; internal set; }

    public IReadOnlyList<string> Aliases { get; internal set; } = Array.Empty<string>();

    public IReadOnlyList<HomeAssistantAreaInfo> Areas { get; internal set; } = Array.Empty<HomeAssistantAreaInfo>();

    public IReadOnlyList<HomeAssistantDeviceInfo> Devices { get; internal set; } = Array.Empty<HomeAssistantDeviceInfo>();

    public IReadOnlyList<HomeAssistantEntityInfo> Entities { get; internal set; } = Array.Empty<HomeAssistantEntityInfo>();

    public HomeAssistantFloor Raw { get; internal set; } = new();
}

/// <summary>An area (the Home Assistant room concept) joined with its floor, devices, and entities.</summary>
public sealed class HomeAssistantAreaInfo
{
    public string AreaId { get; internal set; } = string.Empty;

    public string Name { get; internal set; } = string.Empty;

    public IReadOnlyList<string> Aliases { get; internal set; } = Array.Empty<string>();

    public string? FloorId { get; internal set; }

    public string? FloorName { get; internal set; }

    public IReadOnlyList<HomeAssistantDeviceInfo> Devices { get; internal set; } = Array.Empty<HomeAssistantDeviceInfo>();

    public IReadOnlyList<HomeAssistantEntityInfo> Entities { get; internal set; } = Array.Empty<HomeAssistantEntityInfo>();

    public IReadOnlyList<string> Domains { get; internal set; } = Array.Empty<string>();

    public HomeAssistantArea Raw { get; internal set; } = new();
}

/// <summary>A device joined with its effective area, floor, integration entries, and entities.</summary>
public sealed class HomeAssistantDeviceInfo
{
    public string DeviceId { get; internal set; } = string.Empty;

    public string Name { get; internal set; } = string.Empty;

    public string? AreaId { get; internal set; }

    public string? AreaName { get; internal set; }

    public string? FloorId { get; internal set; }

    public string? FloorName { get; internal set; }

    public string? Manufacturer { get; internal set; }

    public string? Model { get; internal set; }

    public IReadOnlyList<HomeAssistantEntityInfo> Entities { get; internal set; } = Array.Empty<HomeAssistantEntityInfo>();

    public IReadOnlyList<HomeAssistantConfigEntry> Integrations { get; internal set; } = Array.Empty<HomeAssistantConfigEntry>();

    public HomeAssistantDeviceRegistryEntry Raw { get; internal set; } = new();
}

/// <summary>An entity registry entry joined with its live state and effective device, area, and floor.</summary>
public sealed class HomeAssistantEntityInfo
{
    public string EntityId { get; internal set; } = string.Empty;

    public string Name { get; internal set; } = string.Empty;

    public IReadOnlyList<string> Aliases { get; internal set; } = Array.Empty<string>();

    public string Domain { get; internal set; } = string.Empty;

    public string? State { get; internal set; }

    public bool IsAvailable => CurrentState is not null
        && !string.Equals(State, "unavailable", StringComparison.OrdinalIgnoreCase);

    public string? DeviceId { get; internal set; }

    public string? DeviceName { get; internal set; }

    public string? AreaId { get; internal set; }

    public string? AreaName { get; internal set; }

    public string? FloorId { get; internal set; }

    public string? FloorName { get; internal set; }

    public string? Platform { get; internal set; }

    public string? ConfigEntryId { get; internal set; }

    public string? IntegrationDomain { get; internal set; }

    public string? IntegrationTitle { get; internal set; }

    public string? DeviceClass => FirstAttribute<string>("device_class") ?? RegistryEntry?.DeviceClass;

    public string? UnitOfMeasurement => FirstAttribute<string>("unit_of_measurement");

    public string? Icon => FirstAttribute<string>("icon");

    public long? SupportedFeatures => HomeAssistantAttributeReader.GetNonNegativeInt64(
        Attributes,
        "supported_features");

    public string? DisabledBy { get; internal set; }

    public string? HiddenBy { get; internal set; }

    public string? EntityCategory { get; internal set; }

    public IReadOnlyDictionary<string, JsonElement> Attributes => CurrentState?.Attributes
        ?? EmptyAttributes;

    public HomeAssistantState? CurrentState { get; internal set; }

    public HomeAssistantEntityRegistryEntry? RegistryEntry { get; internal set; }

    public IReadOnlyList<HomeAssistantActionDefinition> DomainActions { get; internal set; } = Array.Empty<HomeAssistantActionDefinition>();

    private static readonly IReadOnlyDictionary<string, JsonElement> EmptyAttributes =
        new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);

    private T? FirstAttribute<T>(string name)
    {
        return CurrentState is not null && CurrentState.TryGetAttribute<T>(name, out var value)
            ? value
            : default;
    }
}

/// <summary>A coherent point-in-time view of the Home Assistant house and action catalog.</summary>
public sealed class HomeAssistantInventorySnapshot
{
    public IReadOnlyList<HomeAssistantFloorInfo> Floors { get; internal set; } = Array.Empty<HomeAssistantFloorInfo>();

    public IReadOnlyList<HomeAssistantAreaInfo> Areas { get; internal set; } = Array.Empty<HomeAssistantAreaInfo>();

    public IReadOnlyList<HomeAssistantDeviceInfo> Devices { get; internal set; } = Array.Empty<HomeAssistantDeviceInfo>();

    public IReadOnlyList<HomeAssistantEntityInfo> Entities { get; internal set; } = Array.Empty<HomeAssistantEntityInfo>();

    public IReadOnlyList<HomeAssistantActionDefinition> Actions { get; internal set; } = Array.Empty<HomeAssistantActionDefinition>();

    public HomeAssistantRegistrySnapshot Registries { get; internal set; } = new();
}
