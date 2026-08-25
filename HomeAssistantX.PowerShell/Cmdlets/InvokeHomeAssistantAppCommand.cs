using System.Management.Automation;
using HomeAssistantX.Supervisor;

namespace HomeAssistantX.PowerShell;

/// <summary>Runs one explicit lifecycle operation for a Supervisor-managed Home Assistant app.</summary>
/// <example>
///   <summary>Preview restarting an app</summary>
///   <code>$ha | Invoke-HomeAssistantApp -App 'example_app' -Action Restart -WhatIf</code>
///   <para>Uses one lifecycle action enum instead of a cmdlet per app operation.</para>
/// </example>
[Cmdlet(VerbsLifecycle.Invoke, "HomeAssistantApp", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class InvokeHomeAssistantAppCommand : HomeAssistantCmdlet
{
    /// <summary>Supervisor app/add-on slug.</summary>
    [Parameter(Mandatory = true, Position = 0)]
    [ValidateNotNullOrEmpty]
    public string App { get; set; } = string.Empty;

    /// <summary>Lifecycle action: Install, Update, Start, Stop, Restart, or Uninstall.</summary>
    [Parameter(Mandatory = true, Position = 1)]
    public HomeAssistantAppAction Action { get; set; }

    /// <summary>Writes the Supervisor result to the pipeline.</summary>
    [Parameter]
    public SwitchParameter PassThru { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (!ShouldProcess("app " + App, Action.ToString()))
        {
            return;
        }

        var operation = (HomeAssistantAppOperation)Action;
        var result = await Client.Supervisor.InvokeAppAsync(App, operation, CancelToken).ConfigureAwait(false);
        if (PassThru)
        {
            WriteObject(result);
        }
    }
}
