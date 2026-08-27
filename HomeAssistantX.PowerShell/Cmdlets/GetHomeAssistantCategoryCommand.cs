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
        string? filter = null;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Category)))
        {
            if (string.IsNullOrWhiteSpace(Category))
                throw new ArgumentException("A non-empty category name or native ID is required.", nameof(Category));
            filter = Category!.Trim();
        }

        var categories = await Client.Registries.GetCategoriesAsync(Scope, CancelToken).ConfigureAwait(false);
        if (filter is not null)
        {
            var nativeMatches = categories
                .Where(value => string.Equals(value.CategoryId, filter, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            categories = nativeMatches.Length > 0
                ? nativeMatches
                : categories.Where(value => string.Equals(value.Name, filter, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        WriteObject(categories, true);
    }
}
