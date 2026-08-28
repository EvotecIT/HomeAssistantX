using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Automations;

namespace HomeAssistantX.PowerShell;

/// <summary>Creates or replaces one administrator-managed automation definition.</summary>
/// <example><summary>Preview replacing an automation definition</summary><code>Set-HomeAssistantAutomation morning-routine '{"alias":"Morning","triggers":[],"actions":[]}' -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantAutomation", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(JsonElement))]
public sealed class SetHomeAssistantAutomationCommand : HomeAssistantCmdlet
{
    [Parameter(Mandatory = true, Position = 0)][ValidateNotNullOrEmpty] public string AutomationId { get; set; } = string.Empty;
    [Parameter(Mandatory = true, Position = 1)][ValidateNotNullOrEmpty] public string ConfigurationJson { get; set; } = string.Empty;
    [Parameter] public SwitchParameter PassThru { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var automationId = HomeAssistantAutomationIdentifier.NormalizeConfigurationId(AutomationId);
        CancelToken.ThrowIfCancellationRequested();
        using var document = JsonDocument.Parse(ConfigurationJson);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("ConfigurationJson must be a JSON object.", nameof(ConfigurationJson));
        var configuration = document.RootElement;
        HomeAssistantAutomationIdentifier.ValidateDefinitionForSave(
            automationId,
            configuration,
            nameof(ConfigurationJson),
            CancelToken);
        if (!ShouldProcess(automationId, "Create or replace Home Assistant automation configuration")) return;
        var result = await Client.Automations.SaveConfigurationAsync(automationId, configuration, CancelToken).ConfigureAwait(false);
        if (PassThru) WriteObject(result);
    }
}
