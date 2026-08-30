using System.Management.Automation;
using HomeAssistantX.Models;
using HomeAssistantX.Rest;

namespace HomeAssistantX.PowerShell;

/// <summary>Reads human-oriented Recorder logbook activity for a bounded time range.</summary>
/// <example><summary>Read today's kitchen light activity</summary><code>Get-HomeAssistantLogbook -StartTime (Get-Date).Date -EntityId light.kitchen</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantLogbook")]
[OutputType(typeof(HomeAssistantLogbookEntry))]
public sealed class GetHomeAssistantLogbookCommand : HomeAssistantCmdlet
{
    [Parameter] public DateTimeOffset? StartTime { get; set; }
    [Parameter] public DateTimeOffset? EndTime { get; set; }
    [Parameter(Position = 0)][ValidateNotNullOrEmpty] public string? EntityId { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        CancelToken.ThrowIfCancellationRequested();
        if (StartTime.HasValue && EndTime.HasValue && EndTime <= StartTime) throw new ArgumentOutOfRangeException(nameof(EndTime));
        string? entityId = null;
        if (EntityId is not null && !HomeAssistantEntityId.TryNormalize(EntityId, CancelToken, out entityId))
            throw new ArgumentException("EntityId must be a lowercase native Home Assistant entity identifier.", nameof(EntityId));
        WriteObject(await Client.Rest.GetLogbookAsync(new HomeAssistantLogbookQuery { StartTime = StartTime, EndTime = EndTime, EntityId = entityId }, CancelToken).ConfigureAwait(false), true);
    }
}
