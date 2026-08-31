using System.Management.Automation;
using HomeAssistantX.Automations;

namespace HomeAssistantX.PowerShell;

/// <summary>Reads automation runtime state or an administrator-only editable configuration.</summary>
/// <example><summary>List automation runtime state</summary><code>Get-HomeAssistantAutomation</code></example>
/// <example><summary>Read an editable automation definition</summary><code>Get-HomeAssistantAutomation -AutomationId 'morning-routine' -Configuration</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantAutomation", DefaultParameterSetName = StatusSet)]
[OutputType(typeof(HomeAssistantAutomationStatus))]
[OutputType(typeof(HomeAssistantAutomationConfiguration))]
public sealed class GetHomeAssistantAutomationCommand : HomeAssistantCmdlet
{
    private const string StatusSet = "Status";
    private const string ConfigurationSet = "Configuration";
    [Parameter(Position = 0, ParameterSetName = StatusSet)][ValidateNotNullOrEmpty] public string? EntityId { get; set; }
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ConfigurationSet)][ValidateNotNullOrEmpty] public string? AutomationId { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ConfigurationSet)][ValidateSwitchPresent] public SwitchParameter Configuration { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == ConfigurationSet)
        {
            WriteObject(await Client.Automations.GetConfigurationAsync(AutomationId!, CancelToken).ConfigureAwait(false));
            return;
        }
        if (EntityId is null) WriteObject(await Client.Automations.GetAsync(CancelToken).ConfigureAwait(false), true);
        else WriteObject(await Client.Automations.GetAsync(EntityId, CancelToken).ConfigureAwait(false));
    }
}
