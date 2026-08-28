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
        if (!string.IsNullOrWhiteSpace(Floor)) { var floor = Client.Inventory.ResolveFloor(snapshot, Floor!, CancelToken); areas = areas.Where(x => { CancelToken.ThrowIfCancellationRequested(); return string.Equals(x.FloorId, floor.FloorId, StringComparison.OrdinalIgnoreCase); }); }
        if (!string.IsNullOrWhiteSpace(Area)) { var area = Client.Inventory.ResolveArea(snapshot, Area!, CancelToken); areas = areas.Where(x => { CancelToken.ThrowIfCancellationRequested(); return string.Equals(x.AreaId, area.AreaId, StringComparison.OrdinalIgnoreCase); }); }
        var result = areas.ToArray();
        CancelToken.ThrowIfCancellationRequested();
        WriteObject(result, true);
    }
}
