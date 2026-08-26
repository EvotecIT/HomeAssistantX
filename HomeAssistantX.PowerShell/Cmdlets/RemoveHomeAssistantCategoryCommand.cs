using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Deletes a Home Assistant category from an explicit scope.</summary>
/// <example><summary>Preview deleting an automation category</summary><code>Remove-HomeAssistantCategory -Scope automation -CategoryId comfort -WhatIf</code></example>
[Cmdlet(VerbsCommon.Remove, "HomeAssistantCategory", SupportsShouldProcess = true)]
public sealed class RemoveHomeAssistantCategoryCommand : HomeAssistantCmdlet
{
    /// <summary>Category registry scope.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Scope { get; set; } = string.Empty;

    /// <summary>Native category ID.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    [ValidateNotNullOrEmpty]
    public string CategoryId { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync()
    {
        if (ShouldProcess(Scope + "/" + CategoryId, "Delete Home Assistant category"))
        {
            await Client.Registries.DeleteCategoryAsync(Scope, CategoryId, CancelToken).ConfigureAwait(false);
        }
    }
}
