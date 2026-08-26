using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Deletes a Home Assistant label.</summary>
/// <example><summary>Preview deleting a label</summary><code>Remove-HomeAssistantLabel -LabelId security -WhatIf</code></example>
[Cmdlet(VerbsCommon.Remove, "HomeAssistantLabel", SupportsShouldProcess = true)]
public sealed class RemoveHomeAssistantLabelCommand : HomeAssistantCmdlet
{
    /// <summary>Native label ID.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string LabelId { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync()
    {
        if (ShouldProcess(LabelId, "Delete Home Assistant label"))
        {
            await Client.Registries.DeleteLabelAsync(LabelId, CancelToken).ConfigureAwait(false);
        }
    }
}
