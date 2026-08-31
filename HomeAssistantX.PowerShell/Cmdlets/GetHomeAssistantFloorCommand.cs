using System.Management.Automation;
using HomeAssistantX.Inventory;

namespace HomeAssistantX.PowerShell;

/// <summary>Lists Home Assistant floors with their joined areas, devices, and entities.</summary>
/// <example>
///   <summary>List every configured floor</summary>
///   <code>Get-HomeAssistantFloor</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantFloor")]
[OutputType(typeof(HomeAssistantFloorInfo))]
public sealed class GetHomeAssistantFloorCommand : HomeAssistantCmdlet
{
    /// <summary>Optional floor name, alias, or native ID.</summary>
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    public string? Floor { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var snapshot = await Client.Inventory.GetSnapshotAsync(CancelToken).ConfigureAwait(false);
        var result = HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(Floor, CancelToken)
            ? snapshot.Floors
            : new[] { Client.Inventory.ResolveFloor(snapshot, Floor!, CancelToken) };
        CancelToken.ThrowIfCancellationRequested();
        WriteObject(result, true);
    }
}
