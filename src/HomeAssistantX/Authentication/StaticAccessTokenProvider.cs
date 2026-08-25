namespace HomeAssistantX.Authentication;

/// <summary>Provides a caller-owned long-lived access token.</summary>
public sealed class StaticAccessTokenProvider : IHomeAssistantAccessTokenProvider
{
    private readonly string _accessToken;

    public StaticAccessTokenProvider(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("An access token is required.", nameof(accessToken));
        }

        _accessToken = accessToken;
    }

    public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_accessToken);
    }
}
