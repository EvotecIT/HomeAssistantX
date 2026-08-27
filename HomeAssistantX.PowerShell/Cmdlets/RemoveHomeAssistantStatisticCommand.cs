using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Permanently removes long-term statistics for one or more identifiers.</summary>
/// <example><summary>Preview removing obsolete statistics</summary><code>Remove-HomeAssistantStatistic sensor.old_energy -WhatIf</code></example>
[Cmdlet(VerbsCommon.Remove, "HomeAssistantStatistic", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class RemoveHomeAssistantStatisticCommand : HomeAssistantCmdlet
{
    [Parameter(Mandatory = true, Position = 0)][ValidateNotNullOrEmpty] public string[] StatisticId { get; set; } = Array.Empty<string>();

    protected override async Task ProcessRecordAsync()
    {
        var statisticIds = StatisticId
            .Select(value => value?.Trim() ?? string.Empty)
            .ToArray();
        if (statisticIds.Length == 0 || statisticIds.Any(value => value.Length == 0))
        {
            throw new ArgumentException("At least one statistic identifier is required.", nameof(StatisticId));
        }

        statisticIds = statisticIds.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var count = statisticIds.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
        if (!ShouldProcess(ConnectionDisplayName, "Permanently clear " + count + " Recorder statistic identifier(s)")) return;
        await Client.Recorder.ClearStatisticsAsync(statisticIds, CancelToken).ConfigureAwait(false);
    }
}
