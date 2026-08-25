using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Closes and disposes an explicit Home Assistant connection.</summary>
/// <example>
///   <summary>Disconnect an explicit session</summary>
///   <code>$ha | Disconnect-HomeAssistant</code>
///   <para>Closes both transports and disposes the connection.</para>
/// </example>
[Cmdlet(VerbsCommunications.Disconnect, "HomeAssistant")]
public sealed class DisconnectHomeAssistantCommand : HomeAssistantCmdlet
{
    protected override Task ProcessRecordAsync()
    {
        Connection.Dispose();
        return Task.CompletedTask;
    }
}
