using HomeAssistantX.Services;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Controls;

public enum HomeAssistantButtonDomain
{
    Button,
    InputButton
}

/// <summary>Runs scenes, scripts, and button-like helpers without raw service dictionaries.</summary>
public sealed class HomeAssistantRoutineClient
{
    private readonly HomeAssistantServiceClient _services;

    internal HomeAssistantRoutineClient(HomeAssistantServiceClient services)
    {
        _services = services;
    }

    public Task<HomeAssistantServiceCallResult> ActivateSceneAsync(HomeAssistantTarget target, TimeSpan? transition = null, CancellationToken cancellationToken = default)
        => CallAsync("scene", "turn_on", target, call => AddTransition(call, transition), cancellationToken);

    public Task<HomeAssistantServiceCallResult> RunScriptAsync(HomeAssistantTarget target, IReadOnlyDictionary<string, object?>? variables = null, CancellationToken cancellationToken = default)
    {
        var frozenVariables = HomeAssistantJson.FreezeObject(variables, nameof(variables), "Variables", cancellationToken);
        return CallAsync("script", "turn_on", target, call =>
        {
            if (frozenVariables is not null)
            {
                call.WithData("variables", frozenVariables);
            }
        }, cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> StopScriptAsync(HomeAssistantTarget target, CancellationToken cancellationToken = default)
        => CallAsync("script", "turn_off", target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> ToggleScriptAsync(HomeAssistantTarget target, CancellationToken cancellationToken = default)
        => CallAsync("script", "toggle", target, null, cancellationToken);

    public Task<HomeAssistantServiceCallResult> PressButtonAsync(HomeAssistantTarget target, HomeAssistantButtonDomain domain, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return CallAsync(domain switch
        {
            HomeAssistantButtonDomain.Button => "button",
            HomeAssistantButtonDomain.InputButton => "input_button",
            _ => throw new ArgumentOutOfRangeException(nameof(domain), domain, "Unsupported button domain.")
        }, "press", target, null, cancellationToken);
    }

    private async Task<HomeAssistantServiceCallResult> CallAsync(string domain, string action, HomeAssistantTarget target, Action<HomeAssistantServiceCall>? configure, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (target is null) throw new ArgumentNullException(nameof(target));
        var call = HomeAssistantServiceCall.Create(domain, action).ForTarget(
            target.NormalizeRequiredForDomain(domain, cancellationToken: cancellationToken));
        configure?.Invoke(call);
        return await _services.CallControlAsync(call, cancellationToken).ConfigureAwait(false);
    }

    private static void AddTransition(HomeAssistantServiceCall call, TimeSpan? transition)
    {
        var value = ControlValidation.Duration(transition, nameof(transition), TimeSpan.FromSeconds(6553));
        if (value.HasValue)
        {
            call.WithData("transition", value.Value.TotalSeconds);
        }
    }
}
