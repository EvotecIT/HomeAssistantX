using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Registries;
using HomeAssistantX.Services;
using HomeAssistantX.States;

namespace HomeAssistantX.Inventory;

/// <summary>Builds and queries a joined view of Home Assistant floors, areas, devices, entities, states, and actions.</summary>
public sealed class HomeAssistantInventoryClient
{
    private readonly HomeAssistantRegistryClient _registries;
    private readonly HomeAssistantStateClient _states;
    private readonly HomeAssistantServiceClient _services;

    internal HomeAssistantInventoryClient(
        HomeAssistantRegistryClient registries,
        HomeAssistantStateClient states,
        HomeAssistantServiceClient services)
    {
        _registries = registries;
        _states = states;
        _services = services;
    }

    public async Task<HomeAssistantInventorySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var registriesTask = _registries.GetSnapshotAsync(cancellationToken);
        var statesTask = _states.GetAllWebSocketAsync(cancellationToken);
        var actionsTask = _services.GetActionsAsync(cancellationToken);
        await Task.WhenAll(registriesTask, statesTask, actionsTask).ConfigureAwait(false);

        return Build(
            await registriesTask.ConfigureAwait(false),
            await statesTask.ConfigureAwait(false),
            await actionsTask.ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<HomeAssistantEntityInfo>> GetEntitiesAsync(
        HomeAssistantEntityQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return FilterEntities(snapshot, query).ToArray();
    }

    public HomeAssistantFloorInfo ResolveFloor(HomeAssistantInventorySnapshot snapshot, string idOrName)
    {
        return ResolveUnique(snapshot.Floors, idOrName, x => x.FloorId, x => x.Name, x => x.Aliases, "floor");
    }

    public HomeAssistantAreaInfo ResolveArea(HomeAssistantInventorySnapshot snapshot, string idOrName)
    {
        return ResolveUnique(snapshot.Areas, idOrName, x => x.AreaId, x => x.Name, x => x.Aliases, "area");
    }

    public HomeAssistantDeviceInfo ResolveDevice(HomeAssistantInventorySnapshot snapshot, string idOrName)
    {
        return ResolveUnique(snapshot.Devices, idOrName, x => x.DeviceId, x => x.Name, null, "device");
    }

    public HomeAssistantEntityInfo ResolveEntity(HomeAssistantInventorySnapshot snapshot, string idOrName)
    {
        return ResolveUnique(snapshot.Entities, idOrName, x => x.EntityId, x => x.Name, null, "entity");
    }

    private static HomeAssistantInventorySnapshot Build(
        HomeAssistantRegistrySnapshot registries,
        IReadOnlyList<HomeAssistantState> states,
        IReadOnlyList<HomeAssistantActionDefinition> actions)
    {
        var areasById = registries.Areas.ToDictionary(x => x.AreaId, StringComparer.OrdinalIgnoreCase);
        var floorsById = registries.Floors.ToDictionary(x => x.FloorId, StringComparer.OrdinalIgnoreCase);
        var devicesById = registries.Devices.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var entriesById = registries.Entities.ToDictionary(x => x.EntityId, StringComparer.OrdinalIgnoreCase);
        var statesById = states.ToDictionary(x => x.EntityId, StringComparer.OrdinalIgnoreCase);
        var integrationsById = registries.ConfigEntries.ToDictionary(x => x.EntryId, StringComparer.OrdinalIgnoreCase);

        var entities = entriesById.Keys
            .Union(statesById.Keys, StringComparer.OrdinalIgnoreCase)
            .Select(entityId => CreateEntity(entityId, entriesById, statesById, devicesById, areasById, floorsById, integrationsById, actions))
            .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.EntityId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var devices = registries.Devices.Select(device =>
        {
            areasById.TryGetValue(device.AreaId ?? string.Empty, out var area);
            floorsById.TryGetValue(area?.FloorId ?? string.Empty, out var floor);
            return new HomeAssistantDeviceInfo
            {
                DeviceId = device.Id,
                Name = FirstNonEmpty(device.NameByUser, device.Name, device.Model, device.Id),
                AreaId = area?.AreaId,
                AreaName = area?.Name,
                FloorId = floor?.FloorId,
                FloorName = floor?.Name,
                Manufacturer = device.Manufacturer,
                Model = device.Model,
                Entities = entities.Where(x => string.Equals(x.DeviceId, device.Id, StringComparison.OrdinalIgnoreCase)).ToArray(),
                Integrations = device.ConfigEntries
                    .Select(id => integrationsById.TryGetValue(id, out var entry) ? entry : null)
                    .Where(x => x is not null)
                    .Cast<HomeAssistantConfigEntry>()
                    .ToArray(),
                Raw = device
            };
        }).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        var areas = registries.Areas.Select(area =>
        {
            floorsById.TryGetValue(area.FloorId ?? string.Empty, out var floor);
            var areaEntities = entities.Where(x => string.Equals(x.AreaId, area.AreaId, StringComparison.OrdinalIgnoreCase)).ToArray();
            return new HomeAssistantAreaInfo
            {
                AreaId = area.AreaId,
                Name = area.Name,
                Aliases = area.Aliases,
                FloorId = floor?.FloorId,
                FloorName = floor?.Name,
                Devices = devices.Where(x => string.Equals(x.AreaId, area.AreaId, StringComparison.OrdinalIgnoreCase)).ToArray(),
                Entities = areaEntities,
                Domains = areaEntities.Select(x => x.Domain).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
                Raw = area
            };
        }).OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        var floors = registries.Floors.Select(floor => new HomeAssistantFloorInfo
        {
            FloorId = floor.FloorId,
            Name = floor.Name,
            Aliases = floor.Aliases,
            Level = floor.Level,
            Areas = areas.Where(x => string.Equals(x.FloorId, floor.FloorId, StringComparison.OrdinalIgnoreCase)).ToArray(),
            Devices = devices.Where(x => string.Equals(x.FloorId, floor.FloorId, StringComparison.OrdinalIgnoreCase)).ToArray(),
            Entities = entities.Where(x => string.Equals(x.FloorId, floor.FloorId, StringComparison.OrdinalIgnoreCase)).ToArray(),
            Raw = floor
        }).OrderBy(x => x.Level ?? int.MaxValue).ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase).ToArray();

        return new HomeAssistantInventorySnapshot
        {
            Floors = floors,
            Areas = areas,
            Devices = devices,
            Entities = entities,
            Actions = actions,
            Registries = registries
        };
    }

    private static HomeAssistantEntityInfo CreateEntity(
        string entityId,
        IReadOnlyDictionary<string, HomeAssistantEntityRegistryEntry> entries,
        IReadOnlyDictionary<string, HomeAssistantState> states,
        IReadOnlyDictionary<string, HomeAssistantDeviceRegistryEntry> devices,
        IReadOnlyDictionary<string, HomeAssistantArea> areas,
        IReadOnlyDictionary<string, HomeAssistantFloor> floors,
        IReadOnlyDictionary<string, HomeAssistantConfigEntry> integrations,
        IReadOnlyList<HomeAssistantActionDefinition> actions)
    {
        entries.TryGetValue(entityId, out var entry);
        states.TryGetValue(entityId, out var state);
        devices.TryGetValue(entry?.DeviceId ?? string.Empty, out var device);
        var effectiveAreaId = FirstNonEmptyOrNull(entry?.AreaId, device?.AreaId);
        areas.TryGetValue(effectiveAreaId ?? string.Empty, out var area);
        floors.TryGetValue(area?.FloorId ?? string.Empty, out var floor);
        integrations.TryGetValue(entry?.ConfigEntryId ?? string.Empty, out var integration);
        var friendlyName = TryGetFriendlyName(state);

        return new HomeAssistantEntityInfo
        {
            EntityId = entityId,
            Name = FirstNonEmpty(entry?.Name, friendlyName, entry?.OriginalName, entityId),
            Domain = GetDomain(entityId),
            State = state?.State,
            DeviceId = device?.Id,
            DeviceName = device is null ? null : FirstNonEmpty(device.NameByUser, device.Name, device.Model, device.Id),
            AreaId = area?.AreaId,
            AreaName = area?.Name,
            FloorId = floor?.FloorId,
            FloorName = floor?.Name,
            Platform = entry?.Platform,
            ConfigEntryId = entry?.ConfigEntryId,
            IntegrationDomain = integration?.Domain,
            IntegrationTitle = integration?.Title,
            DisabledBy = entry?.DisabledBy,
            HiddenBy = entry?.HiddenBy,
            EntityCategory = entry?.EntityCategory,
            CurrentState = state,
            RegistryEntry = entry,
            DomainActions = actions.Where(action => string.Equals(action.Domain, GetDomain(entityId), StringComparison.OrdinalIgnoreCase)).ToArray()
        };
    }

    private static IEnumerable<HomeAssistantEntityInfo> FilterEntities(
        HomeAssistantInventorySnapshot snapshot,
        HomeAssistantEntityQuery? query)
    {
        IEnumerable<HomeAssistantEntityInfo> entities = snapshot.Entities;
        if (query is null)
        {
            return entities;
        }

        if (query.Entity is { Count: > 0 })
        {
            entities = entities.Where(x => query.Entity.Any(value => Matches(x.EntityId, value) || Matches(x.Name, value)));
        }

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            entities = entities.Where(x => x.Name.IndexOf(query.Name!, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (!string.IsNullOrWhiteSpace(query.Domain))
        {
            entities = entities.Where(x => Matches(x.Domain, query.Domain!));
        }

        if (!string.IsNullOrWhiteSpace(query.Device))
        {
            var device = ResolveUnique(snapshot.Devices, query.Device!, x => x.DeviceId, x => x.Name, null, "device");
            entities = entities.Where(x => Matches(x.DeviceId, device.DeviceId));
        }

        if (!string.IsNullOrWhiteSpace(query.Area))
        {
            var area = ResolveUnique(snapshot.Areas, query.Area!, x => x.AreaId, x => x.Name, x => x.Aliases, "area");
            entities = entities.Where(x => Matches(x.AreaId, area.AreaId));
        }

        if (!string.IsNullOrWhiteSpace(query.Floor))
        {
            var floor = ResolveUnique(snapshot.Floors, query.Floor!, x => x.FloorId, x => x.Name, x => x.Aliases, "floor");
            entities = entities.Where(x => Matches(x.FloorId, floor.FloorId));
        }

        if (query.AvailableOnly)
        {
            entities = entities.Where(x => x.IsAvailable);
        }

        if (!query.IncludeDisabled)
        {
            entities = entities.Where(x => string.IsNullOrEmpty(x.DisabledBy));
        }

        if (!query.IncludeHidden)
        {
            entities = entities.Where(x => string.IsNullOrEmpty(x.HiddenBy));
        }

        return entities;
    }

    private static T ResolveUnique<T>(
        IReadOnlyList<T> values,
        string idOrName,
        Func<T, string> id,
        Func<T, string> name,
        Func<T, IReadOnlyList<string>>? aliases,
        string kind)
    {
        if (string.IsNullOrWhiteSpace(idOrName))
        {
            throw new ArgumentException("A non-empty identifier or name is required.", nameof(idOrName));
        }

        var normalized = idOrName.Trim();
        var exactIds = values.Where(x => Matches(id(x), normalized)).ToArray();
        if (exactIds.Length == 1)
        {
            return exactIds[0];
        }

        var exactNames = values.Where(x => Matches(name(x), normalized)).ToArray();
        if (exactNames.Length == 1)
        {
            return exactNames[0];
        }

        if (exactNames.Length > 1)
        {
            throw new HomeAssistantLookupException(
                "The " + kind + " name '" + normalized + "' is ambiguous. Use one of these identifiers: "
                + string.Join(", ", exactNames.Select(id)) + ".");
        }

        var aliasMatches = aliases is null
            ? Array.Empty<T>()
            : values.Where(x => aliases(x).Any(alias => Matches(alias, normalized))).ToArray();
        if (aliasMatches.Length == 1)
        {
            return aliasMatches[0];
        }

        if (aliasMatches.Length > 1)
        {
            throw new HomeAssistantLookupException(
                "The " + kind + " alias '" + normalized + "' is ambiguous. Use one of these identifiers: "
                + string.Join(", ", aliasMatches.Select(id)) + ".");
        }

        throw new HomeAssistantLookupException("No Home Assistant " + kind + " matched '" + normalized + "'.");
    }

    private static string? TryGetFriendlyName(HomeAssistantState? state)
    {
        return state is not null && state.TryGetAttribute<string>("friendly_name", out var value)
            ? value
            : null;
    }

    private static string GetDomain(string entityId)
    {
        var separator = entityId.IndexOf('.');
        return separator > 0 ? entityId.Substring(0, separator) : string.Empty;
    }

    private static bool Matches(string? left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.First(x => !string.IsNullOrWhiteSpace(x))!;
    }

    private static string? FirstNonEmptyOrNull(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
    }
}
