using System.Management.Automation;
using HomeAssistantX.Registries;

namespace HomeAssistantX.PowerShell;

/// <summary>Lists Home Assistant categories within an explicit registry scope.</summary>
/// <example><summary>List automation categories</summary><code>Get-HomeAssistantCategory -Scope automation</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantCategory")]
[OutputType(typeof(HomeAssistantCategory))]
public sealed class GetHomeAssistantCategoryCommand : HomeAssistantCmdlet
{
    /// <summary>Category registry scope, such as automation or script.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string Scope { get; set; } = string.Empty;

    /// <summary>Optional category name or native ID.</summary>
    [Parameter(Position = 1)]
    [ValidateNotNullOrEmpty]
    public string? Category { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var categories = await Client.Registries.GetCategoriesAsync(Scope, CancelToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(Category))
        {
            categories = categories.Where(value => string.Equals(value.CategoryId, Category, StringComparison.OrdinalIgnoreCase)
                || string.Equals(value.Name, Category, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        WriteObject(categories, true);
    }
}
