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
        if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(Device, CancelToken)) { var device = Client.Inventory.ResolveDevice(snapshot, Device!, CancelToken); devices = devices.Where(x => HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(x.DeviceId, device.DeviceId, CancelToken)); }
        if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(Area, CancelToken)) { var area = Client.Inventory.ResolveArea(snapshot, Area!, CancelToken); devices = devices.Where(x => HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(x.AreaId, area.AreaId, CancelToken)); }
        if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(Floor, CancelToken)) { var floor = Client.Inventory.ResolveFloor(snapshot, Floor!, CancelToken); devices = devices.Where(x => HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(x.FloorId, floor.FloorId, CancelToken)); }
        var result = devices.ToArray();
        CancelToken.ThrowIfCancellationRequested();
        WriteObject(result, true);
    }
}
