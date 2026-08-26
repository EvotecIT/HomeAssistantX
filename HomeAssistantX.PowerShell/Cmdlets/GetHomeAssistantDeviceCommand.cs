using System.Management.Automation;
using HomeAssistantX.Inventory;

namespace HomeAssistantX.PowerShell;

/// <summary>Lists Home Assistant devices, optionally filtered by area or floor.</summary>
/// <example>
///   <summary>List devices assigned to the Kitchen</summary>
///   <code>Get-HomeAssistantDevice -Area Kitchen</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantDevice")]
[OutputType(typeof(HomeAssistantDeviceInfo))]
public sealed class GetHomeAssistantDeviceCommand : HomeAssistantCmdlet
{
    /// <summary>Device friendly name or native device ID.</summary>
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    public string? Device { get; set; }

    /// <summary>Area name, alias, or native ID used to filter devices.</summary>
    [Parameter]
    [Alias("Room")]
    [ValidateNotNullOrEmpty]
    public string? Area { get; set; }

    /// <summary>Floor name, alias, or native ID used to filter devices.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Floor { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var snapshot = await Client.Inventory.GetSnapshotAsync(CancelToken).ConfigureAwait(false);
        IEnumerable<HomeAssistantDeviceInfo> devices = snapshot.Devices;
        if (!string.IsNullOrWhiteSpace(Device)) { var device = Client.Inventory.ResolveDevice(snapshot, Device!); devices = devices.Where(x => string.Equals(x.DeviceId, device.DeviceId, StringComparison.OrdinalIgnoreCase)); }
        if (!string.IsNullOrWhiteSpace(Area)) { var area = Client.Inventory.ResolveArea(snapshot, Area!); devices = devices.Where(x => string.Equals(x.AreaId, area.AreaId, StringComparison.OrdinalIgnoreCase)); }
        if (!string.IsNullOrWhiteSpace(Floor)) { var floor = Client.Inventory.ResolveFloor(snapshot, Floor!); devices = devices.Where(x => string.Equals(x.FloorId, floor.FloorId, StringComparison.OrdinalIgnoreCase)); }
        WriteObject(devices, true);
    }
}
