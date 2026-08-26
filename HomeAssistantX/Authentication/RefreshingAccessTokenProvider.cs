namespace HomeAssistantX.Authentication;

/// <summary>Refreshes expiring Home Assistant OAuth tokens and delegates secure persistence to the host.</summary>
public sealed class RefreshingAccessTokenProvider :
    IHomeAssistantAccessTokenProvider,
    IHomeAssistantAccessTokenRecovery,
    IDisposable
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
        var tokens = Volatile.Read(ref _tokens);
        if (HasUsableAccessToken(tokens))
        {
            return tokens.AccessToken;
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            tokens = Volatile.Read(ref _tokens);
            if (HasUsableAccessToken(tokens))
            {
                return tokens.AccessToken;
            }

            return await RefreshUnderGateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>Refreshes a server-rejected token once while coalescing concurrent recovery attempts.</summary>
    public async Task RecoverAccessTokenAsync(
        string rejectedAccessToken,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        if (string.IsNullOrWhiteSpace(rejectedAccessToken))
        {
            throw new ArgumentException("A rejected access token is required.", nameof(rejectedAccessToken));
        }

        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (!string.Equals(
                Volatile.Read(ref _tokens).AccessToken,
                rejectedAccessToken,
                StringComparison.Ordinal))
            {
                return;
            }

            await RefreshUnderGateAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<string> RefreshUnderGateAsync(CancellationToken cancellationToken)
    {
        var tokens = Volatile.Read(ref _tokens);
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
        {
            throw new Exceptions.HomeAssistantAuthenticationException(
                "The OAuth access token cannot be refreshed because no refresh token is available.");
        }

        var refreshed = await _oauth.RefreshAsync(_clientId, tokens.RefreshToken!, cancellationToken)
            .ConfigureAwait(false);
        if (_persistTokens is not null)
        {
            await _persistTokens(refreshed, cancellationToken).ConfigureAwait(false);
        }

        Volatile.Write(ref _tokens, refreshed);
        return refreshed.AccessToken;
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
