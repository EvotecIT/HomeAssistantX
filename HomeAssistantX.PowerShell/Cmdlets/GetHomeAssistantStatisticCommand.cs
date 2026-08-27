using System.Management.Automation;
using HomeAssistantX.Recorder;

namespace HomeAssistantX.PowerShell;

/// <summary>Lists Recorder statistics or returns typed aggregated values.</summary>
/// <example><summary>List sum statistics</summary><code>Get-HomeAssistantStatistic -Kind Sum</code></example>
/// <example><summary>Read hourly energy</summary><code>Get-HomeAssistantStatistic -StatisticId sensor.grid_energy -StartTime (Get-Date).AddDays(-1) -Period Hour -Type Change,Sum</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantStatistic", DefaultParameterSetName = CatalogSet)]
[OutputType(typeof(HomeAssistantStatisticMetadata))]
[OutputType(typeof(HomeAssistantStatisticSeries))]
public sealed class GetHomeAssistantStatisticCommand : HomeAssistantCmdlet
{
    private const string CatalogSet = "Catalog";
    private const string MetadataSet = "Metadata";
    private const string ValuesSet = "Values";
    [Parameter(ParameterSetName = CatalogSet)] public HomeAssistantStatisticKind Kind { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = MetadataSet)][ValidateSwitchPresent] public SwitchParameter Metadata { get; set; }
    [Parameter(ParameterSetName = MetadataSet)][Parameter(Mandatory = true, Position = 0, ParameterSetName = ValuesSet)][ValidateNotNullOrEmpty] public string[]? StatisticId { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ValuesSet)] public DateTimeOffset StartTime { get; set; }
    [Parameter(ParameterSetName = ValuesSet)] public DateTimeOffset? EndTime { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ValuesSet)] public HomeAssistantStatisticPeriod Period { get; set; }
    [Parameter(ParameterSetName = ValuesSet)] public HomeAssistantStatisticType[]? Type { get; set; }
    [Parameter(ParameterSetName = ValuesSet)] public Dictionary<string, string>? Unit { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == CatalogSet)
        {
            WriteObject(await Client.Recorder.ListStatisticsAsync(Kind, CancelToken).ConfigureAwait(false), true);
            return;
        }
        if (ParameterSetName == MetadataSet)
        {
            WriteObject(await Client.Recorder.GetStatisticsMetadataAsync(StatisticId, CancelToken).ConfigureAwait(false), true);
            return;
        }
        var query = new HomeAssistantStatisticsQuery(StartTime, Period, StatisticId!) { EndTime = EndTime, Types = Type, Units = Unit };
        WriteObject(await Client.Recorder.GetStatisticsAsync(query, CancelToken).ConfigureAwait(false), true);
    }
}
