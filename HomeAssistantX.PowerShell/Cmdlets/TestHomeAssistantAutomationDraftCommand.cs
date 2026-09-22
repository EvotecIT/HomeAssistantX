using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.PowerShell;

/// <summary>Asks Home Assistant to validate an automation draft without saving it.</summary>
/// <example><summary>Check an automation before saving it</summary><code>Test-HomeAssistantAutomationDraft -ConfigurationJson $automationJson</code></example>
[Cmdlet(VerbsDiagnostic.Test, "HomeAssistantAutomationDraft")]
[OutputType(typeof(JsonElement))]
public sealed class TestHomeAssistantAutomationDraftCommand : HomeAssistantCmdlet
{
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string ConfigurationJson { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync()
    {
        using var document = await HomeAssistantJson.ParseDocumentAsync(ConfigurationJson, CancelToken).ConfigureAwait(false);
        WriteObject(await Client.Automations.ValidateDraftAsync(document.RootElement, CancelToken).ConfigureAwait(false));
    }
}
