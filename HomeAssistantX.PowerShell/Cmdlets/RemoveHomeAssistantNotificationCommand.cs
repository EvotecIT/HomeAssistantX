using System.Management.Automation;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Dismisses one or all persistent Home Assistant notifications.</summary>
/// <example><summary>Dismiss one notification</summary><code>Remove-HomeAssistantNotification -NotificationId garage-open -WhatIf</code></example>
/// <example><summary>Dismiss every notification</summary><code>Remove-HomeAssistantNotification -All -WhatIf</code></example>
[Cmdlet(VerbsCommon.Remove, "HomeAssistantNotification", SupportsShouldProcess = true, DefaultParameterSetName = "Id")]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class RemoveHomeAssistantNotificationCommand : HomeAssistantCmdlet
{
    /// <summary>Persistent notification ID to dismiss.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = "Id")]
    [ValidateNotNullOrEmpty]
    public string NotificationId { get; set; } = string.Empty;

    /// <summary>Dismisses every current persistent notification.</summary>
    [Parameter(Mandatory = true, ParameterSetName = "All")]
    public SwitchParameter All { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var target = ParameterSetName == "All" ? "all persistent notifications" : NotificationId;
        if (!ShouldProcess(target, "Dismiss Home Assistant notification"))
        {
            return;
        }

        var result = ParameterSetName == "All"
            ? await Client.Notifications.DismissAllPersistentAsync(CancelToken).ConfigureAwait(false)
            : await Client.Notifications.DismissPersistentAsync(NotificationId, CancelToken).ConfigureAwait(false);
        WriteObject(result);
    }
}
