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
        var labels = await Client.Registries.GetLabelsAsync(CancelToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(Label))
        {
            var nativeMatches = labels
                .Where(value => string.Equals(value.LabelId, Label, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            labels = nativeMatches.Length > 0
                ? nativeMatches
                : labels.Where(value => string.Equals(value.Name, Label, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        WriteObject(labels, true);
    }
}
