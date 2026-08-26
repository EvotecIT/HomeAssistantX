using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Closes the supplied connection or the current runspace default.</summary>
/// <example>
///   <summary>Disconnect the current runspace default</summary>
///   <code>Disconnect-HomeAssistant</code>
///   <para>Closes both transports, removes the runspace default, and disposes the connection.</para>
/// </example>
[Cmdlet(VerbsCommunications.Disconnect, "HomeAssistant")]
public sealed class DisconnectHomeAssistantCommand : HomeAssistantCmdlet
{
    protected override Task ProcessRecordAsync()
    {
        var connection = ActiveConnection;
        HomeAssistantSession.Clear(CurrentRunspaceId, connection);
        connection.Dispose();
        return Task.CompletedTask;
    }
}
