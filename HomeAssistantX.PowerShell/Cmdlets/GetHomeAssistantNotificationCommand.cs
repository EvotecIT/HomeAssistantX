using System.Management.Automation;
using HomeAssistantX.Notifications;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets persistent notifications currently stored by Home Assistant.</summary>
/// <example><summary>List current notifications</summary><code>Get-HomeAssistantNotification</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantNotification")]
[OutputType(typeof(HomeAssistantPersistentNotification))]
public sealed class GetHomeAssistantNotificationCommand : HomeAssistantCmdlet
{
    protected override async Task ProcessRecordAsync()
    {
        WriteObject(await Client.Notifications.GetPersistentAsync(CancelToken).ConfigureAwait(false), true);
    }
}
