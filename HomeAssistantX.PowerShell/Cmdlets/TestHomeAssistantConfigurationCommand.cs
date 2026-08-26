using System.Management.Automation;
using HomeAssistantX.Rest;

namespace HomeAssistantX.PowerShell;

/// <summary>Validates the active Home Assistant configuration without restarting Core.</summary>
/// <example>
///   <summary>Validate configuration before a restart</summary>
///   <code>$ha | Test-HomeAssistantConfiguration</code>
///   <para>Returns Home Assistant's validation result without restarting Core.</para>
/// </example>
[Cmdlet(VerbsDiagnostic.Test, "HomeAssistantConfiguration")]
[OutputType(typeof(HomeAssistantConfigurationCheck))]
public sealed class TestHomeAssistantConfigurationCommand : HomeAssistantCmdlet
{
    protected override async Task ProcessRecordAsync()
    {
        WriteObject(await Client.Rest.CheckConfigurationAsync(CancelToken).ConfigureAwait(false));
    }
}
