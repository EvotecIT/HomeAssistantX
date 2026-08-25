using HomeAssistantX.Authentication;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class OAuthContractTests
{
    [Fact]
    public async Task AuthorizationExchangeRefreshAndRevokeFollowHomeAssistantOAuthContract()
    {
        using var server = new TestHomeAssistantServer();
        using var oauth = new HomeAssistantOAuthClient(server.BaseUri);
        var clientId = new Uri("https://app.example.net/");
        var redirectUri = new Uri("homeassistantx://auth/callback");

        var authorization = oauth.BuildAuthorizationUri(clientId, redirectUri, "anti-forgery-state");
        var tokens = await oauth.ExchangeCodeAsync(clientId, redirectUri, "authorization-code");
        var refreshed = await oauth.RefreshAsync(clientId, tokens.RefreshToken!);
        await oauth.RevokeRefreshTokenAsync(refreshed.RefreshToken!);

        Assert.Equal(server.BaseUri.Host, authorization.Host);
        Assert.Contains("client_id=https%3A%2F%2Fapp.example.net%2F", authorization.Query);
        Assert.Contains("state=anti-forgery-state", authorization.Query);
        Assert.Equal("oauth-access-token", tokens.AccessToken);
        Assert.Equal("oauth-refresh-token", tokens.RefreshToken);
        Assert.True(tokens.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Equal("refreshed-access-token", refreshed.AccessToken);
        Assert.Equal("oauth-refresh-token", refreshed.RefreshToken);
        Assert.Equal(2, server.OAuthTokenRequestCount);
        Assert.Equal("oauth-refresh-token", server.LastRevokedRefreshToken);
    }

    [Fact]
    public async Task RefreshingProviderCoalescesConcurrentRefreshAndPersistsTheReplacement()
    {
        using var server = new TestHomeAssistantServer();
        using var oauth = new HomeAssistantOAuthClient(server.BaseUri);
        HomeAssistantOAuthTokens? persisted = null;
        using var provider = new RefreshingAccessTokenProvider(
            oauth,
            new Uri("https://app.example.net/"),
            new HomeAssistantOAuthTokens
            {
                AccessToken = "expired-access-token",
                RefreshToken = "oauth-refresh-token",
                ExpiresInSeconds = 1800,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1)
            },
            (tokens, _) =>
            {
                persisted = tokens;
                return Task.CompletedTask;
            });

        var values = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => provider.GetAccessTokenAsync()));

        Assert.All(values, value => Assert.Equal("refreshed-access-token", value));
        Assert.Equal(1, server.OAuthTokenRequestCount);
        Assert.NotNull(persisted);
        Assert.Equal("oauth-refresh-token", persisted!.RefreshToken);
        Assert.True(persisted.ExpiresAt > DateTimeOffset.UtcNow);
    }
}
