using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets the default Home Assistant connection for the current PowerShell runspace.</summary>
/// <example>
///   <summary>Store the current default for explicit pipeline use</summary>
///   <code>$home = Get-HomeAssistantConnection</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantConnection")]
[OutputType(typeof(HomeAssistantConnection))]
public sealed class GetHomeAssistantConnectionCommand : PSCmdlet
{
    protected override void ProcessRecord()
    {
        var connection = HomeAssistantSession.Current;
        if (connection is null)
        {
            throw new InvalidOperationException(
                "No default Home Assistant connection exists in this PowerShell runspace. Run Connect-HomeAssistant first.");
        }

        WriteObject(connection);
    }
}
