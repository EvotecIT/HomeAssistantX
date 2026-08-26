using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

/// <summary>Invokes standard Home Assistant lock actions with an optional device code.</summary>
public sealed class HomeAssistantLockClient : HomeAssistantControlClientBase
{
    internal HomeAssistantLockClient(HomeAssistantServiceClient services) : base(services, "lock") { }

    public Task<HomeAssistantServiceCallResult> ActAsync(HomeAssistantTarget target, HomeAssistantLockAction action, string? code = null, CancellationToken cancellationToken = default)
        => CallAsync(action switch { HomeAssistantLockAction.Lock => "lock", HomeAssistantLockAction.Unlock => "unlock", _ => "open" }, target, string.IsNullOrWhiteSpace(code) ? null : call => call.WithData("code", code), cancellationToken);
}
