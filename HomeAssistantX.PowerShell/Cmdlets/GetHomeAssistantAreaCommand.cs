using System.Management.Automation;
using HomeAssistantX.Inventory;

namespace HomeAssistantX.PowerShell;

/// <summary>Lists Home Assistant areas (rooms), optionally within a floor.</summary>
/// <example>
///   <summary>List the rooms on one floor</summary>
///   <code>Get-HomeAssistantArea -Floor 'Ground Floor'</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantArea")]
[OutputType(typeof(HomeAssistantAreaInfo))]
public sealed class GetHomeAssistantAreaCommand : HomeAssistantCmdlet
{
    /// <summary>Area name, alias, or native ID. <c>Room</c> is an alias.</summary>
    [Parameter(Position = 0)]
    [Alias("Room")]
    [ValidateNotNullOrEmpty]
    public string? Area { get; set; }

    /// <summary>Floor name, alias, or native ID used to filter areas.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Floor { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var snapshot = await Client.Inventory.GetSnapshotAsync(CancelToken).ConfigureAwait(false);
        IEnumerable<HomeAssistantAreaInfo> areas = snapshot.Areas;
        if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(Floor, CancelToken)) { var floor = Client.Inventory.ResolveFloor(snapshot, Floor!, CancelToken); areas = areas.Where(x => HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(x.FloorId, floor.FloorId, CancelToken)); }
        if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(Area, CancelToken)) { var area = Client.Inventory.ResolveArea(snapshot, Area!, CancelToken); areas = areas.Where(x => HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(x.AreaId, area.AreaId, CancelToken)); }
        var result = areas.ToArray();
        CancelToken.ThrowIfCancellationRequested();
        WriteObject(result, true);
    }
}
