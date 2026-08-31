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
            ValidateDomains(InputObject, expectedDomain, CancelToken);
            BindInputConnection(InputObject, CancelToken);
            var ids = SelectDistinct(InputObject, x => x.EntityId, StringComparer.OrdinalIgnoreCase, CancelToken);
            return new ResolvedHomeAssistantTarget(HomeAssistantTarget.ForEntity(ids), ids.Length + " " + expectedDomain + " entities (" + string.Join(", ", ids) + ")", ids.Length);
        }

        var labelSelectors = ParameterSetName == LabelParameterSet
            ? Select(Label, value => RequireLookupValue(value, nameof(Label), CancelToken), CancelToken)
            : null;
        var snapshot = await Client.Inventory.GetSnapshotAsync(CancelToken).ConfigureAwait(false);
        switch (ParameterSetName)
        {
            case EntityParameterSet:
            {
                var entities = Select(Entity, value => Client.Inventory.ResolveEntity(snapshot, value, CancelToken), CancelToken);
                ValidateDomains(entities, expectedDomain, CancelToken);
                var ids = SelectDistinct(entities, x => x.EntityId, StringComparer.OrdinalIgnoreCase, CancelToken);
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.ForEntity(ids), Describe("entities", Select(entities, x => x.Name, CancelToken), ids.Length), ids.Length);
            }
            case AreaParameterSet:
            {
                var areas = Select(Area, value => Client.Inventory.ResolveArea(snapshot, value, CancelToken), CancelToken);
                var count = CountEntities(SelectMany(areas, x => x.Entities, CancelToken), expectedDomain, CancelToken);
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.Create().WithAreas(Select(areas, x => x.AreaId, CancelToken)), Describe("areas", Select(areas, x => x.Name, CancelToken), count), count);
            }
            case DeviceParameterSet:
            {
                var devices = Select(Device, value => Client.Inventory.ResolveDevice(snapshot, value, CancelToken), CancelToken);
                var count = CountEntities(SelectMany(devices, x => x.Entities, CancelToken), expectedDomain, CancelToken);
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.Create().WithDevices(Select(devices, x => x.DeviceId, CancelToken)), Describe("devices", Select(devices, x => x.Name, CancelToken), count), count);
            }
            case FloorParameterSet:
            {
                var floors = Select(Floor, value => Client.Inventory.ResolveFloor(snapshot, value, CancelToken), CancelToken);
                var count = CountEntities(SelectMany(floors, x => x.Entities, CancelToken), expectedDomain, CancelToken);
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.Create().WithFloors(Select(floors, x => x.FloorId, CancelToken)), Describe("floors", Select(floors, x => x.Name, CancelToken), count), count);
            }
            case LabelParameterSet:
            {
                var labels = snapshot.Registries.Labels;
                var selectors = labelSelectors!;
                var resolved = snapshot.Registries.IsLabelRegistryAvailable
                    ? Select(selectors, value => ResolveLabel(labels, value, CancelToken), CancelToken)
                    : Array.Empty<Registries.HomeAssistantLabel>();
                var labelIds = snapshot.Registries.IsLabelRegistryAvailable
                    ? SelectDistinct(resolved, x => x.LabelId, StringComparer.Ordinal, CancelToken)
                    : SelectDistinct(selectors, value => ResolveAssignedLabelId(snapshot, value, CancelToken), StringComparer.Ordinal, CancelToken);
                var selection = IndexLabelSelection(snapshot, labelIds, CancelToken);
                var matching = Count(snapshot.Entities, entity => IsSelectedByLabel(entity, selection, CancelToken)
                    && string.Equals(entity.Domain, expectedDomain, StringComparison.OrdinalIgnoreCase), CancelToken);
                if (matching == 0)
                {
                    throw new HomeAssistantLookupException("The selected labels contain no '" + expectedDomain + "' entities.");
                }

                var names = snapshot.Registries.IsLabelRegistryAvailable
                    ? Select(resolved, x => x.Name, CancelToken)
                    : labelIds;
                return new ResolvedHomeAssistantTarget(HomeAssistantTarget.Create().WithLabels(labelIds), Describe("labels", names, matching), matching);
            }
            default:
                throw new InvalidOperationException("A Home Assistant entity, device, area, or floor target is required.");
        }
    }

    private void BindInputConnection(IEnumerable<HomeAssistantEntityInfo> entities, CancellationToken cancellationToken)
    {
        var explicitConnection = MyInvocation.BoundParameters.ContainsKey(nameof(Connection)) ? Connection : null;
        HomeAssistantConnection? sourceConnection = null;

        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
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

    private static void ValidateDomains(IEnumerable<HomeAssistantEntityInfo> entities, string expectedDomain, CancellationToken cancellationToken)
    {
        var mismatches = new List<HomeAssistantEntityInfo>();
        foreach (var entity in entities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(entity.Domain, expectedDomain, StringComparison.OrdinalIgnoreCase)) mismatches.Add(entity);
        }
        if (mismatches.Count > 0)
        {
            throw new HomeAssistantLookupException("The target contains entities outside the '" + expectedDomain + "' domain: " + string.Join(", ", mismatches.Select(x => x.EntityId)) + ".");
        }
    }

    private static int CountEntities(IEnumerable<HomeAssistantEntityInfo> entities, string expectedDomain, CancellationToken cancellationToken)
    {
        var count = Count(entities, x => string.Equals(x.Domain, expectedDomain, StringComparison.OrdinalIgnoreCase), cancellationToken);
        if (count == 0)
        {
            throw new HomeAssistantLookupException("The selected target contains no '" + expectedDomain + "' entities.");
        }

        return count;
    }

    private static Registries.HomeAssistantLabel ResolveLabel(
        IEnumerable<Registries.HomeAssistantLabel> labels,
        string value,
        CancellationToken cancellationToken)
    {
        var materialized = Select(labels, label => label, cancellationToken);
        var exactNativeMatch = FirstOrDefault(materialized, label => string.Equals(label.LabelId, value, StringComparison.Ordinal), cancellationToken);
        if (exactNativeMatch is not null)
        {
            return exactNativeMatch;
        }

        var nativeMatches = Where(materialized, label => string.Equals(label.LabelId, value, StringComparison.OrdinalIgnoreCase), cancellationToken);
        if (nativeMatches.Length == 1)
        {
            return nativeMatches[0];
        }

        if (nativeMatches.Length > 1)
        {
            throw new HomeAssistantLookupException("More than one Home Assistant label has the native ID '" + value + "'.");
        }

        var nameMatches = Where(materialized, label => string.Equals(label.Name, value, StringComparison.OrdinalIgnoreCase), cancellationToken);
        return nameMatches.Length switch
        {
            1 => nameMatches[0],
            0 => throw new HomeAssistantLookupException("No Home Assistant label matches '" + value + "'."),
            _ => throw new HomeAssistantLookupException("More than one Home Assistant label matches '" + value + "'. Use a native label ID.")
        };
    }

    private static string ResolveAssignedLabelId(HomeAssistantInventorySnapshot snapshot, string value, CancellationToken cancellationToken)
    {
        var assigned = SelectDistinct(
            SelectMany(snapshot.Entities, entity => entity.RegistryEntry?.Labels ?? Array.Empty<string>(), cancellationToken)
                .Concat(SelectMany(snapshot.Devices, device => device.Raw.Labels, cancellationToken))
                .Concat(SelectMany(snapshot.Areas, area => area.Raw.Labels, cancellationToken)),
            labelId => labelId,
            StringComparer.Ordinal,
            cancellationToken);
        var exactMatch = FirstOrDefault(assigned, labelId => string.Equals(labelId, value, StringComparison.Ordinal), cancellationToken);
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        var matches = Where(assigned, labelId => string.Equals(labelId, value, StringComparison.OrdinalIgnoreCase), cancellationToken);
        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new HomeAssistantLookupException("No assigned Home Assistant label has the native ID '" + value + "'. Friendly-name lookup requires label-registry access."),
            _ => throw new HomeAssistantLookupException("More than one assigned Home Assistant label differs only by casing for native ID '" + value + "'.")
        };
    }

    private static string RequireLookupValue(
        string value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken))
            throw new ArgumentException("A non-empty lookup value is required.", parameterName);
        return HomeAssistantX.Protocol.CancellationAwareString.Trim(value, cancellationToken);
    }

    private static LabelSelectionIndex IndexLabelSelection(
        HomeAssistantInventorySnapshot snapshot,
        IReadOnlyCollection<string> labelIds,
        CancellationToken cancellationToken)
    {
        var selectedLabels = new HashSet<string>(
            new HomeAssistantX.Protocol.CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
        foreach (var labelId in labelIds)
        {
            cancellationToken.ThrowIfCancellationRequested();
            selectedLabels.Add(labelId);
        }

        var selectedDevices = new HashSet<string>(
            new HomeAssistantX.Protocol.CancellationAwareStringEqualityComparer(cancellationToken));
        foreach (var device in snapshot.Devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AnySelectedLabel(device.Raw.Labels, selectedLabels, cancellationToken))
            {
                selectedDevices.Add(device.DeviceId);
            }
        }

        var selectedAreas = new HashSet<string>(
            new HomeAssistantX.Protocol.CancellationAwareStringEqualityComparer(cancellationToken));
        foreach (var area in snapshot.Areas)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (AnySelectedLabel(area.Raw.Labels, selectedLabels, cancellationToken))
            {
                selectedAreas.Add(area.AreaId);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return new LabelSelectionIndex(selectedLabels, selectedDevices, selectedAreas);
    }

    private static bool IsSelectedByLabel(
        HomeAssistantEntityInfo entity,
        LabelSelectionIndex selection,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (entity.RegistryEntry is not null
            && AnySelectedLabel(entity.RegistryEntry.Labels, selection.LabelIds, cancellationToken))
        {
            return true;
        }

        if (entity.DeviceId is not null
            && selection.DeviceIds.Contains(entity.DeviceId))
        {
            return true;
        }

        return entity.AreaId is not null
            && selection.AreaIds.Contains(entity.AreaId);
    }

    private static bool AnySelectedLabel(
        IEnumerable<string> values,
        HashSet<string> selectedLabels,
        CancellationToken cancellationToken)
    {
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (selectedLabels.Contains(value)) return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private sealed class LabelSelectionIndex
    {
        internal LabelSelectionIndex(
            HashSet<string> labelIds,
            HashSet<string> deviceIds,
            HashSet<string> areaIds)
        {
            LabelIds = labelIds;
            DeviceIds = deviceIds;
            AreaIds = areaIds;
        }

        internal HashSet<string> LabelIds { get; }

        internal HashSet<string> DeviceIds { get; }

        internal HashSet<string> AreaIds { get; }
    }

    private static TTarget[] Select<TSource, TTarget>(IEnumerable<TSource> source, Func<TSource, TTarget> selector, CancellationToken cancellationToken)
    {
        var result = new List<TTarget>();
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(selector(item));
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result.ToArray();
    }

    private static TTarget[] SelectMany<TSource, TTarget>(IEnumerable<TSource> source, Func<TSource, IEnumerable<TTarget>> selector, CancellationToken cancellationToken)
    {
        var result = new List<TTarget>();
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var selected in selector(item))
            {
                cancellationToken.ThrowIfCancellationRequested();
                result.Add(selected);
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result.ToArray();
    }

    private static TTarget[] SelectDistinct<TSource, TTarget>(IEnumerable<TSource> source, Func<TSource, TTarget> selector, IEqualityComparer<TTarget> comparer, CancellationToken cancellationToken)
    {
        var result = new List<TTarget>();
        var seen = new HashSet<TTarget>(comparer);
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var selected = selector(item);
            if (seen.Add(selected)) result.Add(selected);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result.ToArray();
    }

    private static TSource[] Where<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate, CancellationToken cancellationToken)
    {
        var result = new List<TSource>();
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate(item)) result.Add(item);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result.ToArray();
    }

    private static TSource? FirstOrDefault<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate, CancellationToken cancellationToken) where TSource : class
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate(item)) return item;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return null;
    }

    private static int Count<TSource>(IEnumerable<TSource> source, Func<TSource, bool> predicate, CancellationToken cancellationToken)
    {
        var count = 0;
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (predicate(item)) count++;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return count;
    }

    private static bool Contains(IEnumerable<string> source, string value, CancellationToken cancellationToken)
    {
        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.Equals(item, value, StringComparison.Ordinal)) return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
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
