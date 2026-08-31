using System.Management.Automation;
using HomeAssistantX.Discovery;

namespace HomeAssistantX.PowerShell;

/// <summary>Finds Home Assistant instances advertised on the local IPv4 network.</summary>
/// <example><summary>Discover local Home Assistant instances</summary><code>Find-HomeAssistant -TimeoutSeconds 5</code></example>
[Cmdlet(VerbsCommon.Find, "HomeAssistant")]
[OutputType(typeof(HomeAssistantDiscoveredInstance))]
public sealed class FindHomeAssistantCommand : AsyncPSCmdlet
{
    /// <summary>Maximum time to listen for local advertisements, from 1 through 60 seconds.</summary>
    [Parameter]
    [ValidateRange(1, 60)]
    public int TimeoutSeconds { get; set; } = 3;

    protected override async Task ProcessRecordAsync()
    {
        var results = await new HomeAssistantDiscoveryClient().DiscoverAsync(TimeSpan.FromSeconds(TimeoutSeconds), CancelToken).ConfigureAwait(false);
        WriteObject(results, true);
    }
}
