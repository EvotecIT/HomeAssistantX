using System.Management.Automation;
using HomeAssistantX.Registries;

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
        var labelId = HomeAssistantRegistryValidation.Require(LabelId, nameof(LabelId));
        if (ShouldProcess(labelId, "Delete Home Assistant label"))
        {
            await Client.Registries.DeleteLabelAsync(labelId, CancelToken).ConfigureAwait(false);
        }
    }
}
