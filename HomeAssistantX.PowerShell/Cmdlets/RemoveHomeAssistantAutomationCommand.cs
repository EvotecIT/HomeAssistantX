using System.Management.Automation;
using HomeAssistantX.Automations;

namespace HomeAssistantX.PowerShell;

/// <summary>Deletes one administrator-managed automation definition.</summary>
/// <example><summary>Preview deleting an automation definition</summary><code>Remove-HomeAssistantAutomation morning-routine -WhatIf</code></example>
[Cmdlet(VerbsCommon.Remove, "HomeAssistantAutomation", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveHomeAssistantAutomationCommand : HomeAssistantCmdlet
{
    [Parameter(Mandatory = true, Position = 0)][ValidateNotNullOrEmpty] public string AutomationId { get; set; } = string.Empty;
    protected override async Task ProcessRecordAsync()
    {
        var automationId = HomeAssistantAutomationIdentifier.NormalizeConfigurationId(AutomationId, CancelToken);
        if (ShouldProcess(automationId, "Delete Home Assistant automation configuration"))
            await Client.Automations.DeleteConfigurationAsync(automationId, CancelToken).ConfigureAwait(false);
    }
}
