using System.Management.Automation;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Inventory;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Resolves friendly entity, device, area, and floor targets through the joined inventory.</summary>
public abstract class HomeAssistantTargetCmdlet : HomeAssistantCmdlet
{
    protected const string InputObjectParameterSet = "InputObject";
    protected const string EntityParameterSet = "Entity";
    protected const string AreaParameterSet = "Area";
    protected const string DeviceParameterSet = "Device";
    protected const string FloorParameterSet = "Floor";

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

    protected async Task<ResolvedHomeAssistantTarget> ResolveTargetAsync(string expectedDomain)
    {
        if (ParameterSetName == InputObjectParameterSet)
        {
            ValidateDomains(InputObject, expectedDomain);
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
            default:
                throw new InvalidOperationException("A Home Assistant entity, device, area, or floor target is required.");
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
