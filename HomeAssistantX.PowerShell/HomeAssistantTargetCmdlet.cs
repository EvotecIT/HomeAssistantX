using System.Management.Automation;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Inventory;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Resolves friendly entity, device, area, and floor targets through the joined inventory.</summary>
public abstract class HomeAssistantTargetCmdlet : HomeAssistantCmdlet
{
    private readonly List<HomeAssistantEntityInfo> _pipelineEntities = new();
    protected const string InputObjectParameterSet = "InputObject";
    protected const string EntityParameterSet = "Entity";
    protected const string AreaParameterSet = "Area";
    protected const string DeviceParameterSet = "Device";
    protected const string FloorParameterSet = "Floor";
    protected const string LabelParameterSet = "Label";

    /// <summary>Joined entities accepted from <c>Get-HomeAssistantEntity</c>.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = InputObjectParameterSet)]
    [ValidateNotNull]
    public HomeAssistantEntityInfo[] InputObject { get; set; } = Array.Empty<HomeAssistantEntityInfo>();

    /// <summary>One or more entity friendly names or native entity IDs.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = EntityParameterSet)]
    [Alias("EntityId")]
    [ValidateNotNullOrEmpty]
    public string[] Entity { get; set; } = Array.Empty<string>();

    /// <summary>One or more area names, aliases, or native area IDs. <c>Room</c> is an alias.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = AreaParameterSet)]
    [Alias("AreaId", "Room")]
    [ValidateNotNullOrEmpty]
    public string[] Area { get; set; } = Array.Empty<string>();

    /// <summary>One or more device friendly names or native device IDs.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = DeviceParameterSet)]
    [Alias("DeviceId")]
    [ValidateNotNullOrEmpty]
    public string[] Device { get; set; } = Array.Empty<string>();

    /// <summary>One or more floor names, aliases, or native floor IDs.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = FloorParameterSet)]
    [Alias("FloorId")]
    [ValidateNotNullOrEmpty]
    public string[] Floor { get; set; } = Array.Empty<string>();

    /// <summary>One or more label names or native label IDs.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = LabelParameterSet)]
    [Alias("LabelId")]
    [ValidateNotNullOrEmpty]
    public string[] Label { get; set; } = Array.Empty<string>();

    protected sealed override Task ProcessRecordAsync()
    {
        if (ParameterSetName == InputObjectParameterSet)
        {
            _pipelineEntities.AddRange(InputObject);
            return Task.CompletedTask;
        }

        return ProcessTargetRecordAsync();
    }

    protected sealed override Task EndProcessingAsync()
    {
        if (_pipelineEntities.Count == 0)
        {
            return Task.CompletedTask;
        }

        InputObject = _pipelineEntities.ToArray();
        return ProcessTargetRecordAsync();
    }

    protected abstract Task ProcessTargetRecordAsync();

    protected async Task<ResolvedHomeAssistantTarget> ResolveTargetAsync(string expectedDomain)
    {
        if (ParameterSetName == InputObjectParameterSet)
        {
            ValidateDomains(InputObject, expectedDomain);
            BindInputConnection(InputObject);
            var ids = InputObject.Select(x => x.EntityId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            return new ResolvedHomeAssistantTarget(HomeAssistantTarget.ForEntity(ids), ids.Length + " " + expectedDomain + " entities (" + string.Join(", ", ids) + ")", ids.Length);
        }

        var snapshot = await Client.Inventory.GetSnapshotAsync(CancelToken).ConfigureAwait(false);
        switch (ParameterSetName)
        {
            case EntityParameterSet:
            {
                var entities = Entity.Select(value => Client.Inventory.ResolveEntity(snapshot, value)).ToArray();
                ValidateDomains(entities, expectedDomain);
                var ids = entities.Select(x => x.EntityId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.ForEntity(ids), Describe("entities", entities.Select(x => x.Name), ids.Length), ids.Length);
            }
            case AreaParameterSet:
            {
                var areas = Area.Select(value => Client.Inventory.ResolveArea(snapshot, value)).ToArray();
                var count = CountEntities(areas.SelectMany(x => x.Entities), expectedDomain);
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.Create().WithAreas(areas.Select(x => x.AreaId).ToArray()), Describe("areas", areas.Select(x => x.Name), count), count);
            }
            case DeviceParameterSet:
            {
                var devices = Device.Select(value => Client.Inventory.ResolveDevice(snapshot, value)).ToArray();
                var count = CountEntities(devices.SelectMany(x => x.Entities), expectedDomain);
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.Create().WithDevices(devices.Select(x => x.DeviceId).ToArray()), Describe("devices", devices.Select(x => x.Name), count), count);
            }
            case FloorParameterSet:
            {
                var floors = Floor.Select(value => Client.Inventory.ResolveFloor(snapshot, value)).ToArray();
                var count = CountEntities(floors.SelectMany(x => x.Entities), expectedDomain);
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.Create().WithFloors(floors.Select(x => x.FloorId).ToArray()), Describe("floors", floors.Select(x => x.Name), count), count);
            }
            case LabelParameterSet:
            {
                var labels = snapshot.Registries.Labels;
                var selectors = Label.Select(value => RequireLookupValue(value, nameof(Label))).ToArray();
                var resolved = snapshot.Registries.IsLabelRegistryAvailable
                    ? selectors.Select(value => ResolveLabel(labels, value)).ToArray()
                    : Array.Empty<Registries.HomeAssistantLabel>();
                var labelIds = snapshot.Registries.IsLabelRegistryAvailable
                    ? resolved.Select(x => x.LabelId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                    : selectors.Select(value => ResolveAssignedLabelId(snapshot, value)).Distinct(StringComparer.Ordinal).ToArray();
                var matching = snapshot.Entities.Count(entity => IsSelectedByLabel(snapshot, entity, labelIds)
                    && string.Equals(entity.Domain, expectedDomain, StringComparison.OrdinalIgnoreCase));
                if (matching == 0)
                {
                    throw new HomeAssistantLookupException("The selected labels contain no '" + expectedDomain + "' entities.");
                }

                var names = snapshot.Registries.IsLabelRegistryAvailable
                    ? resolved.Select(x => x.Name)
                    : labelIds;
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.Create().WithLabels(labelIds), Describe("labels", names, matching), matching);
            }
            default:
                throw new InvalidOperationException("A Home Assistant entity, device, area, or floor target is required.");
        }
    }

    private void BindInputConnection(IEnumerable<HomeAssistantEntityInfo> entities)
    {
        var explicitConnection = MyInvocation.BoundParameters.ContainsKey(nameof(Connection)) ? Connection : null;
        HomeAssistantConnection? sourceConnection = null;

        foreach (var entity in entities)
        {
            if (!HomeAssistantEntityProvenance.TryGet(entity, out var entityConnection))
            {
                if (explicitConnection is null)
                {
                    throw new InvalidOperationException(
                        "The piped entity has no HomeAssistantX connection provenance. Pass the source connection with -Connection.");
                }

                continue;
            }

            if (explicitConnection is not null && !ReferenceEquals(explicitConnection, entityConnection))
            {
                throw new InvalidOperationException(
                    "The explicit Home Assistant connection does not match the connection that produced the piped entity.");
            }

            if (sourceConnection is not null && !ReferenceEquals(sourceConnection, entityConnection))
            {
                throw new InvalidOperationException("Piped entities from different Home Assistant connections cannot be combined in one action.");
            }

            sourceConnection = entityConnection;
        }

        Connection = explicitConnection ?? sourceConnection
            ?? throw new InvalidOperationException("A Home Assistant connection is required for the piped entities.");
        if (Connection.IsDisposed)
        {
            throw new ObjectDisposedException(
                nameof(HomeAssistantConnection),
                "The Home Assistant connection associated with the piped entities is disposed.");
        }
    }

    private static void ValidateDomains(IEnumerable<HomeAssistantEntityInfo> entities, string expectedDomain)
    {
        var mismatches = entities.Where(x => !string.Equals(x.Domain, expectedDomain, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (mismatches.Length > 0)
        {
            throw new HomeAssistantLookupException("The target contains entities outside the '" + expectedDomain + "' domain: " + string.Join(", ", mismatches.Select(x => x.EntityId)) + ".");
        }
    }

    private static int CountEntities(IEnumerable<HomeAssistantEntityInfo> entities, string expectedDomain)
    {
        var count = entities.Count(x => string.Equals(x.Domain, expectedDomain, StringComparison.OrdinalIgnoreCase));
        if (count == 0)
        {
            throw new HomeAssistantLookupException("The selected target contains no '" + expectedDomain + "' entities.");
        }

        return count;
    }

    private static Registries.HomeAssistantLabel ResolveLabel(
        IEnumerable<Registries.HomeAssistantLabel> labels,
        string value)
    {
        var materialized = labels.ToArray();
        var exactNativeMatch = materialized
            .FirstOrDefault(label => string.Equals(label.LabelId, value, StringComparison.Ordinal));
        if (exactNativeMatch is not null)
        {
            return exactNativeMatch;
        }

        var nativeMatches = materialized
            .Where(label => string.Equals(label.LabelId, value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (nativeMatches.Length == 1)
        {
            return nativeMatches[0];
        }

        if (nativeMatches.Length > 1)
        {
            throw new HomeAssistantLookupException("More than one Home Assistant label has the native ID '" + value + "'.");
        }

        var nameMatches = materialized
            .Where(label => string.Equals(label.Name, value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return nameMatches.Length switch
        {
            1 => nameMatches[0],
            0 => throw new HomeAssistantLookupException("No Home Assistant label matches '" + value + "'."),
            _ => throw new HomeAssistantLookupException("More than one Home Assistant label matches '" + value + "'. Use a native label ID.")
        };
    }

    private static string ResolveAssignedLabelId(HomeAssistantInventorySnapshot snapshot, string value)
    {
        var assigned = snapshot.Entities.SelectMany(entity => entity.RegistryEntry?.Labels ?? Array.Empty<string>())
            .Concat(snapshot.Devices.SelectMany(device => device.Raw.Labels))
            .Concat(snapshot.Areas.SelectMany(area => area.Raw.Labels))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var exactMatch = assigned.FirstOrDefault(labelId => string.Equals(labelId, value, StringComparison.Ordinal));
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        var matches = assigned
            .Where(labelId => string.Equals(labelId, value, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new HomeAssistantLookupException("No assigned Home Assistant label has the native ID '" + value + "'. Friendly-name lookup requires label-registry access."),
            _ => throw new HomeAssistantLookupException("More than one assigned Home Assistant label differs only by casing for native ID '" + value + "'.")
        };
    }

    private static string RequireLookupValue(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("A non-empty lookup value is required.", parameterName);
        return value.Trim();
    }

    private static bool IsSelectedByLabel(
        HomeAssistantInventorySnapshot snapshot,
        HomeAssistantEntityInfo entity,
        IReadOnlyCollection<string> labelIds)
    {
        if (entity.RegistryEntry?.Labels.Any(value => labelIds.Contains(value, StringComparer.Ordinal)) == true)
        {
            return true;
        }

        if (entity.DeviceId is not null
            && snapshot.Devices.FirstOrDefault(device => string.Equals(device.DeviceId, entity.DeviceId, StringComparison.OrdinalIgnoreCase))
                is { } device
            && device.Raw.Labels.Any(value => labelIds.Contains(value, StringComparer.Ordinal)))
        {
            return true;
        }

        return entity.AreaId is not null
            && snapshot.Areas.FirstOrDefault(area => string.Equals(area.AreaId, entity.AreaId, StringComparison.OrdinalIgnoreCase))
                is { } area
            && area.Raw.Labels.Any(value => labelIds.Contains(value, StringComparer.Ordinal));
    }

    private static string Describe(string kind, IEnumerable<string> names, int count)
    {
        return kind + " " + string.Join(", ", names) + " (" + count + " matching entities)";
    }
}

public sealed class ResolvedHomeAssistantTarget
{
    internal ResolvedHomeAssistantTarget(HomeAssistantTarget target, string description, int entityCount)
    {
        Target = target;
        Description = description;
        EntityCount = entityCount;
    }

    public HomeAssistantTarget Target { get; }

    public string Description { get; }

    public int EntityCount { get; }
}
