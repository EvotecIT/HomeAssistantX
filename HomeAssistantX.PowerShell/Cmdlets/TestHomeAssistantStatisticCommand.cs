using System.Management.Automation;
using System.Text.Json;

namespace HomeAssistantX.PowerShell;

/// <summary>Validates Recorder long-term statistics and returns every issue reported by Home Assistant.</summary>
/// <example><summary>Find statistics issues</summary><code>Test-HomeAssistantStatistic</code></example>
[Cmdlet(VerbsDiagnostic.Test, "HomeAssistantStatistic")]
[OutputType(typeof(JsonElement))]
public sealed class TestHomeAssistantStatisticCommand : HomeAssistantCmdlet
{
    protected override async Task ProcessRecordAsync() => WriteObject(await Client.Recorder.ValidateStatisticsAsync(CancelToken).ConfigureAwait(false));
}
