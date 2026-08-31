using System.Management.Automation;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Runs selected automation entities without changing their definitions.</summary>
/// <example><summary>Preview running an automation</summary><code>Invoke-HomeAssistantAutomation -Entity automation.morning -WhatIf</code></example>
[Cmdlet(VerbsLifecycle.Invoke, "HomeAssistantAutomation", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class InvokeHomeAssistantAutomationCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Skips configured automation conditions. By default, conditions remain enforced.</summary>
    [Parameter] public SwitchParameter SkipConditions { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        var target = await ResolveTargetAsync("automation").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, SkipConditions ? "Run automation actions without conditions" : "Run automation with conditions")) return;
        WriteObject(await Client.Automations.TriggerAsync(target.Target, SkipConditions, CancelToken).ConfigureAwait(false));
    }
}
