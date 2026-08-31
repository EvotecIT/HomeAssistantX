using System.Collections;
using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Protocol;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

public enum HomeAssistantRoutineAction { ActivateScene, RunScript, StopScript, ToggleScript, PressButton, PressInputButton }

/// <summary>Runs a scene, script, or button routine through one task-oriented command.</summary>
/// <example><summary>Activate an evening scene</summary><code>Invoke-HomeAssistantRoutine -Entity scene.evening -Action ActivateScene -WhatIf</code></example>
/// <example><summary>Run a script with variables</summary><code>Invoke-HomeAssistantRoutine -Entity script.welcome -Action RunScript -Variables @{ name = 'Alex' }</code></example>
[Cmdlet(VerbsLifecycle.Invoke, "HomeAssistantRoutine", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class InvokeHomeAssistantRoutineCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Scene, script, or button operation to run.</summary>
    [Parameter(Mandatory = true)] public HomeAssistantRoutineAction Action { get; set; }
    /// <summary>Optional scene transition duration.</summary>
    [Parameter] public TimeSpan? Transition { get; set; }
    /// <summary>Variables supplied when running a script.</summary>
    [Parameter] public Hashtable? Variables { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        if (!Enum.IsDefined(typeof(HomeAssistantRoutineAction), Action)) throw new ArgumentOutOfRangeException(nameof(Action), Action, "Unsupported routine action.");
        if (Transition.HasValue && Action != HomeAssistantRoutineAction.ActivateScene) throw new ArgumentException("Transition is valid only for scene activation.", nameof(Transition));
        if (Transition < TimeSpan.Zero || Transition > TimeSpan.FromSeconds(6553)) throw new ArgumentOutOfRangeException(nameof(Transition));
        if (Variables is not null && Action != HomeAssistantRoutineAction.RunScript) throw new ArgumentException("Variables are valid only when running a script.", nameof(Variables));
        var variables = HomeAssistantJson.FreezeObject(Convert(Variables, CancelToken), nameof(Variables), "Variables", CancelToken);
        var domain = Action switch { HomeAssistantRoutineAction.ActivateScene => "scene", HomeAssistantRoutineAction.PressButton => "button", HomeAssistantRoutineAction.PressInputButton => "input_button", _ => "script" };
        var target = await ResolveTargetAsync(domain).ConfigureAwait(false);
        if (!ShouldProcess(target.Description, Action.ToString())) return;
        HomeAssistantServiceCallResult result = Action switch
        {
            HomeAssistantRoutineAction.ActivateScene => await Client.Controls.Routines.ActivateSceneAsync(target.Target, Transition, CancelToken).ConfigureAwait(false),
            HomeAssistantRoutineAction.RunScript => await Client.Controls.Routines.RunScriptAsync(target.Target, variables, CancelToken).ConfigureAwait(false),
            HomeAssistantRoutineAction.StopScript => await Client.Controls.Routines.StopScriptAsync(target.Target, CancelToken).ConfigureAwait(false),
            HomeAssistantRoutineAction.ToggleScript => await Client.Controls.Routines.ToggleScriptAsync(target.Target, CancelToken).ConfigureAwait(false),
            HomeAssistantRoutineAction.PressButton => await Client.Controls.Routines.PressButtonAsync(target.Target, HomeAssistantButtonDomain.Button, CancelToken).ConfigureAwait(false),
            HomeAssistantRoutineAction.PressInputButton => await Client.Controls.Routines.PressButtonAsync(target.Target, HomeAssistantButtonDomain.InputButton, CancelToken).ConfigureAwait(false),
            _ => throw new ArgumentOutOfRangeException(nameof(Action))
        };
        WriteObject(result);
    }

    private static IReadOnlyDictionary<string, object?>? Convert(Hashtable? values, CancellationToken cancellationToken)
    {
        if (values is null) return null;
        cancellationToken.ThrowIfCancellationRequested();
        var result = new Dictionary<string, object?>(
            new CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
        foreach (DictionaryEntry entry in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Key is not string key) throw new ArgumentException("Variable names must be non-empty strings.", nameof(Variables));
            key = ControlValidation.RequiredUnchanged(key, nameof(Variables), cancellationToken);
            result[key] = entry.Value;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }
}
