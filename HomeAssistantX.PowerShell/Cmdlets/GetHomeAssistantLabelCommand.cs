using System.Management.Automation;
using HomeAssistantX.Registries;

namespace HomeAssistantX.PowerShell;

/// <summary>Lists Home Assistant labels, optionally selecting one by name or ID.</summary>
/// <example><summary>List all labels</summary><code>Get-HomeAssistantLabel</code></example>
/// <example><summary>Find a label by name</summary><code>Get-HomeAssistantLabel -Label Security</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantLabel")]
[OutputType(typeof(HomeAssistantLabel))]
public sealed class GetHomeAssistantLabelCommand : HomeAssistantCmdlet
{
    /// <summary>Optional label name or native ID.</summary>
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    public string? Label { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        string? filter = null;
        if (MyInvocation.BoundParameters.ContainsKey(nameof(Label)))
        {
            if (string.IsNullOrWhiteSpace(Label))
                throw new ArgumentException("A non-empty label name or native ID is required.", nameof(Label));
            filter = Label!.Trim();
        }

        var labels = await Client.Registries.GetLabelsAsync(CancelToken).ConfigureAwait(false);
        if (filter is not null)
        {
            HomeAssistantLabel? exactNativeMatch = null;
            foreach (var value in labels)
            {
                CancelToken.ThrowIfCancellationRequested();
                if (string.Equals(value.LabelId, filter, StringComparison.Ordinal))
                {
                    exactNativeMatch = value;
                    break;
                }
            }
            if (exactNativeMatch is not null)
            {
                CancelToken.ThrowIfCancellationRequested();
                labels = new[] { exactNativeMatch };
                WriteObject(labels, true);
                return;
            }

            var nativeMatches = Filter(labels, value => string.Equals(value.LabelId, filter, StringComparison.OrdinalIgnoreCase));
            labels = nativeMatches.Length > 0
                ? nativeMatches
                : Filter(labels, value => string.Equals(value.Name, filter, StringComparison.OrdinalIgnoreCase));
        }

        CancelToken.ThrowIfCancellationRequested();
        WriteObject(labels, true);
    }

    private HomeAssistantLabel[] Filter(IEnumerable<HomeAssistantLabel> values, Func<HomeAssistantLabel, bool> predicate)
    {
        var result = new List<HomeAssistantLabel>();
        foreach (var value in values)
        {
            CancelToken.ThrowIfCancellationRequested();
            if (predicate(value)) result.Add(value);
        }
        CancelToken.ThrowIfCancellationRequested();
        return result.ToArray();
    }
}
