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
            HomeAssistantCategory? exactNativeMatch = null;
            foreach (var value in categories)
            {
                CancelToken.ThrowIfCancellationRequested();
                if (string.Equals(value.CategoryId, filter, StringComparison.Ordinal))
                {
                    exactNativeMatch = value;
                    break;
                }
            }
            if (exactNativeMatch is not null)
            {
                CancelToken.ThrowIfCancellationRequested();
                categories = new[] { exactNativeMatch };
                WriteObject(categories, true);
                return;
            }

            var nativeMatches = Filter(categories, value => string.Equals(value.CategoryId, filter, StringComparison.OrdinalIgnoreCase));
            categories = nativeMatches.Length > 0
                ? nativeMatches
                : Filter(categories, value => string.Equals(value.Name, filter, StringComparison.OrdinalIgnoreCase));
        }

        CancelToken.ThrowIfCancellationRequested();
        WriteObject(categories, true);
    }

    private HomeAssistantCategory[] Filter(IEnumerable<HomeAssistantCategory> values, Func<HomeAssistantCategory, bool> predicate)
    {
        var result = new List<HomeAssistantCategory>();
        foreach (var value in values)
        {
            CancelToken.ThrowIfCancellationRequested();
            if (predicate(value)) result.Add(value);
        }
        CancelToken.ThrowIfCancellationRequested();
        return result.ToArray();
    }
}
