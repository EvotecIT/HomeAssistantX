namespace HomeAssistantX.Authentication;

/// <summary>Refreshes expiring Home Assistant OAuth tokens and delegates secure persistence to the host.</summary>
public sealed class RefreshingAccessTokenProvider : IHomeAssistantAccessTokenProvider, IDisposable
{
    private readonly HomeAssistantOAuthClient _oauth;
    private readonly Uri _clientId;
    private readonly Func<HomeAssistantOAuthTokens, CancellationToken, Task>? _persistTokens;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private HomeAssistantOAuthTokens _tokens;
    private int _disposed;

    public RefreshingAccessTokenProvider(
        HomeAssistantOAuthClient oauth,
        Uri clientId,
        HomeAssistantOAuthTokens tokens,
        Func<HomeAssistantOAuthTokens, CancellationToken, Task>? persistTokens = null)
    {
        _oauth = oauth ?? throw new ArgumentNullException(nameof(oauth));
        _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
        if (!_clientId.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute client identifier is required.", nameof(clientId));
        }

        _tokens = tokens ?? throw new ArgumentNullException(nameof(tokens));
        _persistTokens = persistTokens;
    }

    /// <summary>Gets a valid access token, refreshing it once when it is near expiry.</summary>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (HasUsableAccessToken(_tokens))
        {
            return _tokens.AccessToken;
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (HasUsableAccessToken(_tokens))
            {
                return _tokens.AccessToken;
            }

            if (string.IsNullOrWhiteSpace(_tokens.RefreshToken))
            {
                throw new Exceptions.HomeAssistantAuthenticationException(
                    "The OAuth access token expired and no refresh token is available.");
            }

            var refreshed = await _oauth.RefreshAsync(_clientId, _tokens.RefreshToken!, cancellationToken)
                .ConfigureAwait(false);
            if (_persistTokens is not null)
            {
                await _persistTokens(refreshed, cancellationToken).ConfigureAwait(false);
            }

            _tokens = refreshed;
            return refreshed.AccessToken;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private static bool HasUsableAccessToken(HomeAssistantOAuthTokens tokens)
    {
        return !string.IsNullOrWhiteSpace(tokens.AccessToken)
            && tokens.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1);
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            throw new ObjectDisposedException(nameof(RefreshingAccessTokenProvider));
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        _refreshGate.Dispose();
    }
}
