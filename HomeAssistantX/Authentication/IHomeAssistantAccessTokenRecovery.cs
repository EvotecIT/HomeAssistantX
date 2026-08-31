namespace HomeAssistantX.Authentication;

/// <summary>
/// Recovers an access token that Home Assistant has rejected even though its locally known lifetime has not elapsed.
/// </summary>
/// <remarks>
/// Implement this interface together with <see cref="IHomeAssistantAccessTokenProvider"/> when the provider can
/// refresh or otherwise replace rejected credentials. HomeAssistantX retries the failed operation at most once.
/// </remarks>
public interface IHomeAssistantAccessTokenRecovery
{
    /// <summary>
    /// Replaces the rejected access token, or observes a replacement completed by another caller.
    /// </summary>
    /// <param name="rejectedAccessToken">The token used by the rejected request. Implementations must not log it.</param>
    /// <param name="cancellationToken">Cancels waiting for or performing recovery.</param>
    Task RecoverAccessTokenAsync(
        string rejectedAccessToken,
        CancellationToken cancellationToken = default);
}
