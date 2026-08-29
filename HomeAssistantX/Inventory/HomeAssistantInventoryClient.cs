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
            await actionsTask.ConfigureAwait(false),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantEntityInfo>> GetEntitiesAsync(
        HomeAssistantEntityQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        ValidateQuery(query);
        var snapshot = await GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return FilterEntities(snapshot, query, cancellationToken).ToArray();
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
        return ResolveUnique(snapshot.Entities, idOrName, x => x.EntityId, x => x.Name, x => x.Aliases, "entity");
    }

    internal static HomeAssistantInventorySnapshot Build(
        HomeAssistantRegistrySnapshot registries,
        IReadOnlyList<HomeAssistantState> states,
        IReadOnlyList<HomeAssistantActionDefinition> actions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var areasById = BuildMap(registries.Areas, x => x.AreaId, cancellationToken);
        var floorsById = BuildMap(registries.Floors, x => x.FloorId, cancellationToken);
        var devicesById = BuildMap(registries.Devices, x => x.Id, cancellationToken);
        var entriesById = BuildEntityMap(registries.Entities, entry => entry.EntityId, cancellationToken);
        var statesById = BuildEntityMap(states, state => state.EntityId, cancellationToken);
        var integrationsById = BuildMap(registries.ConfigEntries, x => x.EntryId, cancellationToken);

        var entityIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddKeys(entityIds, entriesById.Keys, cancellationToken);
        AddKeys(entityIds, statesById.Keys, cancellationToken);
        var entities = new List<HomeAssistantEntityInfo>(entityIds.Count);
        foreach (var entityId in entityIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            entities.Add(CreateEntity(
                entityId,
                entriesById,
                statesById,
                devicesById,
                areasById,
                floorsById,
                integrationsById,
                actions,
                cancellationToken));
        }
        Sort(
            entities,
            (left, right) =>
            {
                var byName = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                return byName != 0
                    ? byName
                    : string.Compare(left.EntityId, right.EntityId, StringComparison.OrdinalIgnoreCase);
            },
            cancellationToken);

        var devices = new List<HomeAssistantDeviceInfo>(registries.Devices.Count);
        foreach (var device in registries.Devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            areasById.TryGetValue(device.AreaId ?? string.Empty, out var area);
            floorsById.TryGetValue(area?.FloorId ?? string.Empty, out var floor);
            var deviceEntities = SelectMatching(
                entities,
                entity => entity.DeviceId,
                device.Id,
                cancellationToken);
            var integrations = new List<HomeAssistantConfigEntry>();
            foreach (var configEntryId in device.ConfigEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (integrationsById.TryGetValue(configEntryId, out var integration))
                {
                    integrations.Add(integration);
                }
            }

            devices.Add(new HomeAssistantDeviceInfo
            {
                DeviceId = device.Id,
                Name = FirstNonEmpty(device.NameByUser, device.Name, device.Model, device.Id),
                AreaId = area?.AreaId,
                AreaName = area?.Name,
                FloorId = floor?.FloorId,
                FloorName = floor?.Name,
                Manufacturer = device.Manufacturer,
                Model = device.Model,
                Entities = deviceEntities,
                Integrations = integrations.ToArray(),
                Raw = device
            });
        }
        Sort(devices, (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase), cancellationToken);

        var areas = new List<HomeAssistantAreaInfo>(registries.Areas.Count);
        foreach (var area in registries.Areas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            floorsById.TryGetValue(area.FloorId ?? string.Empty, out var floor);
            var areaEntities = SelectMatching(entities, entity => entity.AreaId, area.AreaId, cancellationToken);
            var areaDevices = SelectMatching(devices, device => device.AreaId, area.AreaId, cancellationToken);
            var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entity in areaEntities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                domains.Add(entity.Domain);
            }
            var orderedDomains = domains.ToList();
            Sort(orderedDomains, (left, right) => string.Compare(left, right, StringComparison.OrdinalIgnoreCase), cancellationToken);

            areas.Add(new HomeAssistantAreaInfo
            {
                AreaId = area.AreaId,
                Name = area.Name,
                Aliases = area.Aliases,
                FloorId = floor?.FloorId,
                FloorName = floor?.Name,
                Devices = areaDevices,
                Entities = areaEntities,
                Domains = orderedDomains.ToArray(),
                Raw = area
            });
        }
        Sort(areas, (left, right) => string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase), cancellationToken);

        var floors = new List<HomeAssistantFloorInfo>(registries.Floors.Count);
        foreach (var floor in registries.Floors)
        {
            cancellationToken.ThrowIfCancellationRequested();
            floors.Add(new HomeAssistantFloorInfo
            {
                FloorId = floor.FloorId,
                Name = floor.Name,
                Aliases = floor.Aliases,
                Level = floor.Level,
                Areas = SelectMatching(areas, area => area.FloorId, floor.FloorId, cancellationToken),
                Devices = SelectMatching(devices, device => device.FloorId, floor.FloorId, cancellationToken),
                Entities = SelectMatching(entities, entity => entity.FloorId, floor.FloorId, cancellationToken),
                Raw = floor
            });
        }
        Sort(
            floors,
            (left, right) =>
            {
                var byLevel = (left.Level ?? int.MaxValue).CompareTo(right.Level ?? int.MaxValue);
                return byLevel != 0
                    ? byLevel
                    : string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
            },
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();
        return new HomeAssistantInventorySnapshot
        {
            Floors = floors.ToArray(),
            Areas = areas.ToArray(),
            Devices = devices.ToArray(),
            Entities = entities.ToArray(),
            Actions = actions,
            Registries = registries
        };
    }

    private static Dictionary<string, T> BuildEntityMap<T>(
        IEnumerable<T> values,
        Func<T, string?> getEntityId,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var entityId = HomeAssistantEntityId.RequireResponseEntityId(
                getEntityId(value),
                cancellationToken);
            if (result.ContainsKey(entityId))
            {
                throw new HomeAssistantProtocolException(
                    "Home Assistant returned a duplicate entity identifier while building inventory.");
            }

            result.Add(entityId, value);
        }

        return result;
    }

    private static Dictionary<string, T> BuildMap<T>(
        IEnumerable<T> values,
        Func<T, string> getId,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(getId(value), value);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static void AddKeys(
        ISet<string> destination,
        IEnumerable<string> values,
        CancellationToken cancellationToken)
    {
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            destination.Add(value);
        }
    }

    private static T[] SelectMatching<T>(
        IEnumerable<T> values,
        Func<T, string?> selector,
        string expected,
        CancellationToken cancellationToken)
    {
        var result = new List<T>();
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(selector(value), expected, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(value);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return result.ToArray();
    }

    private static void Sort<T>(
        List<T> values,
        Comparison<T> comparison,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        values.Sort((left, right) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return comparison(left, right);
        });
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static HomeAssistantEntityInfo CreateEntity(
        string entityId,
        IReadOnlyDictionary<string, HomeAssistantEntityRegistryEntry> entries,
        IReadOnlyDictionary<string, HomeAssistantState> states,
        IReadOnlyDictionary<string, HomeAssistantDeviceRegistryEntry> devices,
        IReadOnlyDictionary<string, HomeAssistantArea> areas,
        IReadOnlyDictionary<string, HomeAssistantFloor> floors,
        IReadOnlyDictionary<string, HomeAssistantConfigEntry> integrations,
        IReadOnlyList<HomeAssistantActionDefinition> actions,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        entries.TryGetValue(entityId, out var entry);
        states.TryGetValue(entityId, out var state);
        devices.TryGetValue(entry?.DeviceId ?? string.Empty, out var device);
        var effectiveAreaId = FirstNonEmptyOrNull(entry?.AreaId, device?.AreaId);
        areas.TryGetValue(effectiveAreaId ?? string.Empty, out var area);
        floors.TryGetValue(area?.FloorId ?? string.Empty, out var floor);
        integrations.TryGetValue(entry?.ConfigEntryId ?? string.Empty, out var integration);
        var friendlyName = TryGetFriendlyName(state);
        var registryName = GetRegistryFullName(entry, device);
        var name = FirstNonEmpty(friendlyName, registryName, entityId);
        var aliases = new List<string>();
        var uniqueAliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (entry is not null)
        {
            foreach (var alias in entry.Aliases)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var candidate = string.IsNullOrWhiteSpace(alias) ? registryName : alias!.Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && uniqueAliases.Add(candidate!))
                {
                    aliases.Add(candidate!);
                }
            }
        }

        var domain = GetDomain(entityId);
        var domainActions = new List<HomeAssistantActionDefinition>();
        foreach (var action in actions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(action.Domain, domain, StringComparison.OrdinalIgnoreCase))
            {
                domainActions.Add(action);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new HomeAssistantEntityInfo
        {
            EntityId = entityId,
            Name = name,
            Aliases = aliases.ToArray(),
            Domain = domain,
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
            DomainActions = domainActions.ToArray()
        };
    }

    private static IEnumerable<HomeAssistantEntityInfo> FilterEntities(
        HomeAssistantInventorySnapshot snapshot,
        HomeAssistantEntityQuery? query,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IEnumerable<HomeAssistantEntityInfo> entities = snapshot.Entities;
        if (query is null)
        {
            return entities;
        }

        if (query.Entity is { Count: > 0 })
        {
            var selectedEntityIds = new HashSet<string>(
                query.Entity.Select(value => ResolveUnique(
                    snapshot.Entities,
                    value,
                    x => x.EntityId,
                    x => x.Name,
                    x => x.Aliases,
                    "entity")).Select(x => x.EntityId),
                StringComparer.OrdinalIgnoreCase);
            entities = entities.Where(x => selectedEntityIds.Contains(x.EntityId));
        }

        if (query.Name is not null)
        {
            entities = entities.Where(x => x.Name.IndexOf(query.Name.Trim(), StringComparison.OrdinalIgnoreCase) >= 0);
        }

        if (query.Domain is not null)
        {
            entities = entities.Where(x => Matches(x.Domain, query.Domain.Trim()));
        }

        if (query.Device is not null)
        {
            var device = ResolveUnique(snapshot.Devices, query.Device!, x => x.DeviceId, x => x.Name, null, "device");
            entities = entities.Where(x => Matches(x.DeviceId, device.DeviceId));
        }

        if (query.Area is not null)
        {
            var area = ResolveUnique(snapshot.Areas, query.Area!, x => x.AreaId, x => x.Name, x => x.Aliases, "area");
            entities = entities.Where(x => Matches(x.AreaId, area.AreaId));
        }

        if (query.Floor is not null)
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

        return EnumerateWithCancellation(entities, cancellationToken);
    }

    private static IEnumerable<T> EnumerateWithCancellation<T>(
        IEnumerable<T> values,
        CancellationToken cancellationToken)
    {
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return value;
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void ValidateQuery(HomeAssistantEntityQuery? query)
    {
        if (query is null) return;
        if (query.Entity is not null && (query.Entity.Count == 0 || query.Entity.Any(string.IsNullOrWhiteSpace)))
            throw new ArgumentException("Entity filters must contain at least one non-empty value.", nameof(query));
        if (query.Name is not null && string.IsNullOrWhiteSpace(query.Name)) throw new ArgumentException("Name cannot be empty.", nameof(query));
        if (query.Domain is not null && string.IsNullOrWhiteSpace(query.Domain)) throw new ArgumentException("Domain cannot be empty.", nameof(query));
        if (query.Device is not null && string.IsNullOrWhiteSpace(query.Device)) throw new ArgumentException("Device cannot be empty.", nameof(query));
        if (query.Area is not null && string.IsNullOrWhiteSpace(query.Area)) throw new ArgumentException("Area cannot be empty.", nameof(query));
        if (query.Floor is not null && string.IsNullOrWhiteSpace(query.Floor)) throw new ArgumentException("Floor cannot be empty.", nameof(query));
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

    private static string? GetRegistryFullName(
        HomeAssistantEntityRegistryEntry? entry,
        HomeAssistantDeviceRegistryEntry? device)
    {
        if (entry is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(entry.Name))
        {
            return entry.Name;
        }

        if (!entry.HasEntityName)
        {
            return FirstNonEmptyOrNull(entry.OriginalName, entry.EntityId);
        }

        var deviceName = device is null
            ? null
            : FirstNonEmptyOrNull(device.NameByUser, device.Name, device.Model);
        if (!string.IsNullOrWhiteSpace(deviceName))
        {
            return string.IsNullOrWhiteSpace(entry.OriginalName)
                ? deviceName
                : deviceName + " " + entry.OriginalName;
        }

        return FirstNonEmptyOrNull(entry.OriginalName, entry.EntityId);
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
