using System.Management.Automation;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Sends a persistent notification or a message to selected notify entities.</summary>
/// <example><summary>Create a persistent alert</summary><code>Send-HomeAssistantNotification -Persistent -Message 'Garage is open' -Title Security</code></example>
/// <example><summary>Notify devices in an area</summary><code>Send-HomeAssistantNotification -Area Kitchen -Message 'Dinner is ready' -WhatIf</code></example>
[Cmdlet(VerbsCommunications.Send, "HomeAssistantNotification", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SendHomeAssistantNotificationCommand : HomeAssistantTargetCmdlet
{
    private const string PersistentParameterSet = "Persistent";

    /// <summary>Creates a notification stored in the Home Assistant interface.</summary>
    [Parameter(Mandatory = true, ParameterSetName = PersistentParameterSet)]
    [ValidateSwitchPresent]
    public SwitchParameter Persistent { get; set; }

    /// <summary>Notification message.</summary>
    [Parameter(Mandatory = true)]
    [ValidateNotNullOrEmpty]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional notification title.</summary>
    [Parameter]
    public string? Title { get; set; }

    /// <summary>Stable persistent notification ID. Reusing it updates the existing notification.</summary>
    [Parameter(ParameterSetName = PersistentParameterSet)]
    [ValidateNotNullOrEmpty]
    public string? NotificationId { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        RequireNonBlank(Message, nameof(Message));
        if (NotificationId is not null)
        {
            RequireNonBlank(NotificationId, nameof(NotificationId));
        }

        if (ParameterSetName == PersistentParameterSet)
        {
            if (ShouldProcess(NotificationId ?? "new persistent notification", "Send Home Assistant persistent notification"))
            {
                WriteObject(await Client.Notifications.CreatePersistentAsync(Message, Title, NotificationId, CancelToken).ConfigureAwait(false));
            }

            return;
        }

        var target = await ResolveTargetAsync("notify").ConfigureAwait(false);
        if (ShouldProcess(target.Description, "Send Home Assistant notification"))
        {
            WriteObject(await Client.Notifications.SendAsync(target.Target, Message, Title, CancelToken).ConfigureAwait(false));
        }
    }

    private static void RequireNonBlank(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }
    }
}
