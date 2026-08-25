using System.Net.Http;
using System.Text;
using System.Text.Json;
using HomeAssistantX.Configuration;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Authentication;

/// <summary>Implements the Home Assistant OAuth authorization, token, refresh, and revoke protocol.</summary>
public sealed class HomeAssistantOAuthClient : IDisposable
{
    private const int MaximumTokenResponseBytes = 1024 * 1024;
    private readonly Uri _baseUri;
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;

    public HomeAssistantOAuthClient(Uri baseUri, HttpClient? httpClient = null)
    {
        _baseUri = HomeAssistantUri.NormalizeBaseUri(baseUri);
        _ownsHttpClient = httpClient is null;
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>Builds the browser URI used to begin Home Assistant authorization.</summary>
    public Uri BuildAuthorizationUri(Uri clientId, Uri redirectUri, string state)
    {
        ValidateClient(clientId, redirectUri);
        if (string.IsNullOrWhiteSpace(state))
        {
            throw new ArgumentException("A non-empty OAuth state is required.", nameof(state));
        }

        var query = "client_id=" + Uri.EscapeDataString(clientId.AbsoluteUri)
            + "&redirect_uri=" + Uri.EscapeDataString(redirectUri.AbsoluteUri)
            + "&state=" + Uri.EscapeDataString(state);
        return new Uri(_baseUri, "auth/authorize?" + query);
    }

    /// <summary>Exchanges an authorization code for access and refresh tokens.</summary>
    public Task<HomeAssistantOAuthTokens> ExchangeCodeAsync(
        Uri clientId,
        Uri redirectUri,
        string code,
        CancellationToken cancellationToken = default)
    {
        ValidateClient(clientId, redirectUri);
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("An authorization code is required.", nameof(code));
        }

        return RequestTokensAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["client_id"] = clientId.AbsoluteUri
            },
            null,
            cancellationToken);
    }

    /// <summary>Refreshes an access token using a caller-owned refresh token.</summary>
    public Task<HomeAssistantOAuthTokens> RefreshAsync(
        Uri clientId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        if (clientId is null || !clientId.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute client identifier is required.", nameof(clientId));
        }

        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("A refresh token is required.", nameof(refreshToken));
        }

        return RequestTokensAsync(
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = clientId.AbsoluteUri
            },
            refreshToken,
            cancellationToken);
    }

    /// <summary>Revokes a refresh token and all access tokens issued from it.</summary>
    public async Task RevokeRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new ArgumentException("A refresh token is required.", nameof(refreshToken));
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "auth/revoke"))
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken })
        };
        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HomeAssistantAuthenticationException("Home Assistant rejected the token revocation request.");
            }
        }
        catch (HttpRequestException ex)
        {
            throw new HomeAssistantConnectionException("The Home Assistant token revocation request failed.", ex);
        }
    }

    private async Task<HomeAssistantOAuthTokens> RequestTokensAsync(
        IReadOnlyDictionary<string, string> fields,
        string? existingRefreshToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(_baseUri, "auth/token"))
        {
            Content = new FormUrlEncodedContent(fields)
        };
        try
        {
            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HomeAssistantAuthenticationException("Home Assistant rejected the OAuth token request.");
            }

            var bytes = await ReadBoundedAsync(response.Content, cancellationToken).ConfigureAwait(false);
            var tokens = JsonSerializer.Deserialize<HomeAssistantOAuthTokens>(bytes, HomeAssistantJson.SerializerOptions)
                ?? throw new HomeAssistantProtocolException("Home Assistant returned an empty OAuth token response.");
            if (string.IsNullOrWhiteSpace(tokens.AccessToken) || tokens.ExpiresInSeconds <= 0)
            {
                throw new HomeAssistantProtocolException("Home Assistant returned an incomplete OAuth token response.");
            }

            if (string.IsNullOrWhiteSpace(tokens.RefreshToken))
            {
                tokens.RefreshToken = existingRefreshToken;
            }

            tokens.ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(tokens.ExpiresInSeconds);
            return tokens;
        }
        catch (HttpRequestException ex)
        {
            throw new HomeAssistantConnectionException("The Home Assistant OAuth token request failed.", ex);
        }
        catch (JsonException ex)
        {
            throw new HomeAssistantProtocolException("Home Assistant returned invalid OAuth token JSON.", ex);
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength is long length && length > MaximumTokenResponseBytes)
        {
            throw new HomeAssistantProtocolException("The Home Assistant OAuth response exceeded the size limit.");
        }

#if NET10_0_OR_GREATER
        using var stream = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
#else
        using var stream = await content.ReadAsStreamAsync().ConfigureAwait(false);
#endif
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        while (true)
        {
            var read = await stream.ReadAsync(chunk, 0, chunk.Length, cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return buffer.ToArray();
            }

            if (buffer.Length + read > MaximumTokenResponseBytes)
            {
                throw new HomeAssistantProtocolException("The Home Assistant OAuth response exceeded the size limit.");
            }

            buffer.Write(chunk, 0, read);
        }
    }

    private static void ValidateClient(Uri clientId, Uri redirectUri)
    {
        if (clientId is null || !clientId.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute client identifier is required.", nameof(clientId));
        }

        if (redirectUri is null || !redirectUri.IsAbsoluteUri)
        {
            throw new ArgumentException("An absolute redirect URI is required.", nameof(redirectUri));
        }
    }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }
}
