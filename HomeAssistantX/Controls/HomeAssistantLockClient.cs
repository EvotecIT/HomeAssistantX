using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

/// <summary>Invokes standard Home Assistant lock actions with an optional device code.</summary>
public sealed class HomeAssistantLockClient : HomeAssistantControlClientBase
{
    internal HomeAssistantLockClient(HomeAssistantServiceClient services) : base(services, "lock") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantLockAction action, string? code = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedCode = code is null ? null : ControlValidation.RequiredUnchanged(code, nameof(code), cancellationToken);
        return CallAsync(action switch
        {
            HomeAssistantLockAction.Lock => "lock",
            HomeAssistantLockAction.Unlock => "unlock",
            HomeAssistantLockAction.Open => "open",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported lock action.")
        }, target, normalizedCode is null ? null : call => call.WithData("code", normalizedCode), cancellationToken);
    }
}
