using System.Management.Automation;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets installed Supervisor-managed Home Assistant apps.</summary>
/// <example>
///   <summary>List installed apps</summary>
///   <code>$ha | Get-HomeAssistantApp</code>
///   <para>Returns installed Supervisor apps/add-ons.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantApp")]
[OutputType(typeof(HomeAssistantApp))]
public sealed class GetHomeAssistantAppCommand : HomeAssistantCmdlet
{
    /// <summary>Supervisor app/add-on slug. Omit it to return all installed apps.</summary>
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty]
    public string? App { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var apps = await Client.Supervisor.GetAppsAsync(CancelToken).ConfigureAwait(false);
        WriteObject(
            string.IsNullOrWhiteSpace(App)
                ? apps
                : apps.Where(value => string.Equals(value.Slug, App, StringComparison.OrdinalIgnoreCase)).ToArray(),
            enumerateCollection: true);
    }
}
