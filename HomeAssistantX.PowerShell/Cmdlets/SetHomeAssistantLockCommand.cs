using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Locks, unlocks, or opens a lock with high-impact confirmation.</summary>
/// <example>
///   <summary>Preview unlocking the front door</summary>
///   <code>Set-HomeAssistantLock -Entity lock.front_door -Action Unlock -WhatIf</code>
/// </example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantLock", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantLockCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Locks, unlocks, or opens the selected locks.</summary>
    [Parameter(Mandatory = true)]
    public HomeAssistantLockAction Action { get; set; }

    /// <summary>Optional device code required by some locks.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Code { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantLockAction), Action))
        {
            throw new ArgumentOutOfRangeException(nameof(Action), Action, "Unsupported lock action.");
        }

        var target = await ResolveTargetAsync("lock").ConfigureAwait(false);
        if (ShouldProcess(target.Description, Action.ToString()))
        {
            WriteObject(await Client.Controls.Locks.ActAsync(target.Target, Action, Code, CancelToken).ConfigureAwait(false));
        }
    }
}
