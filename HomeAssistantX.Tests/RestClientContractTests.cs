using System.Net.Http;
using System.Text.Json;
using HomeAssistantX.Authentication;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class RestClientContractTests
{
    [Fact]
    public async Task RestApiPreservesHomeAssistantStateAndExtensionData()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var status = await client.Rest.CheckApiAsync();
        var configuration = await client.Rest.GetConfigurationAsync();
        var states = await client.Rest.GetStatesAsync();
        var temperature = await client.Rest.GetStateAsync("sensor.kitchen_temperature");

        Assert.Equal("API running.", status.Message);
        Assert.True(status.AdditionalData["custom_api_field"].GetBoolean());
        Assert.Equal("Test Home", configuration.LocationName);
        Assert.Equal(42, configuration.AdditionalData["custom_field"].GetInt32());
        Assert.Equal(2, states.Count);
        Assert.Equal("sensor", temperature.Domain);
        Assert.True(temperature.TryGetAttribute<string>("unit_of_measurement", out var unit));
        Assert.Equal("°C", unit);
        Assert.Equal("good", temperature.Attributes["nested"].GetProperty("quality").GetString());
        Assert.Equal("test", temperature.AdditionalData["custom_state_field"].GetProperty("source").GetString());
        Assert.Equal("state-trace", temperature.Context!.AdditionalData["trace_hint"].GetString());
        Assert.Equal("Bearer " + TestHomeAssistantServer.AccessToken, server.LastAuthorization);
    }

    [Fact]
    public async Task FluentServiceCallProducesTheHomeAssistantRestContract()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var call = HomeAssistantServiceCall.Create("light", "turn_on")
            .ForEntity("light.kitchen")
            .ForFloor("ground")
            .ForLabel("evening")
            .WithData("brightness_pct", 45);

        var result = await client.Services.CallRestAsync(call);

        Assert.Empty(result.ChangedStates);
        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.Equal("light.kitchen", body.RootElement.GetProperty("entity_id").GetString());
        Assert.Equal("ground", body.RootElement.GetProperty("floor_id").GetString());
        Assert.Equal("evening", body.RootElement.GetProperty("label_id").GetString());
        Assert.Equal(45, body.RootElement.GetProperty("brightness_pct").GetInt32());
    }

    [Fact]
    public async Task RestFailuresAreClassifiedWithoutLeakingCredentials()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var commandError = await Assert.ThrowsAsync<HomeAssistantCommandException>(
            () => client.Rest.SendAsync<JsonElement>(HttpMethod.Post, "api/services/test/fail"));
        Assert.Equal("http_400", commandError.Code);
        Assert.Equal("Validation failed", commandError.Message);

        using var unauthorizedClient = TestClientFactory.Create(server, "private-bad-token");
        var authError = await Assert.ThrowsAsync<HomeAssistantAuthenticationException>(
            () => unauthorizedClient.Rest.CheckApiAsync());
        Assert.DoesNotContain("private-bad-token", authError.ToString());
        Assert.Equal(0, server.OAuthTokenRequestCount);
    }

    [Fact]
    public async Task RejectedOAuthTokenIsRefreshedOnceAndConcurrentRequestsShareRecovery()
    {
        using var server = new TestHomeAssistantServer
        {
            RequiredAccessToken = "refreshed-access-token"
        };
        using var oauth = new HomeAssistantOAuthClient(server.BaseUri);
        using var provider = CreateUnexpiredRefreshingProvider(oauth);
        using var client = TestClientFactory.Create(server, accessTokenProvider: provider);

        var statuses = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => client.Rest.CheckApiAsync()));

        Assert.All(statuses, status => Assert.Equal("API running.", status.Message));
        Assert.Equal(1, server.OAuthTokenRequestCount);
        Assert.True(server.UnauthorizedRequestCount >= 1);
        Assert.Equal(12, server.AuthenticatedRequestCount);
    }

    [Fact]
    public async Task RecoveredOAuthTokenIsNotRetriedMoreThanOnce()
    {
        using var server = new TestHomeAssistantServer
        {
            RequiredAccessToken = "a-token-the-provider-cannot-issue"
        };
        using var oauth = new HomeAssistantOAuthClient(server.BaseUri);
        using var provider = CreateUnexpiredRefreshingProvider(oauth);
        using var client = TestClientFactory.Create(server, accessTokenProvider: provider);

        await Assert.ThrowsAsync<HomeAssistantAuthenticationException>(
            () => client.Rest.CheckApiAsync());

        Assert.Equal(1, server.OAuthTokenRequestCount);
        Assert.Equal(2, server.UnauthorizedRequestCount);
    }

    [Fact]
    public async Task CallerCancellationStopsRejectedTokenRecoveryWithoutRetrying()
    {
        using var server = new TestHomeAssistantServer();
        var provider = new BlockingRecoveryProvider();
        using var client = TestClientFactory.Create(
            server,
            requestTimeout: TimeSpan.FromSeconds(5),
            accessTokenProvider: provider);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.Rest.CheckApiAsync(cancellation.Token));

        Assert.Equal(1, server.UnauthorizedRequestCount);
        Assert.Equal(1, provider.RecoveryCount);
    }

    [Fact]
    public async Task RawAuthenticatedRequestRejectsAnotherOrigin()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var exception = await Assert.ThrowsAsync<ArgumentException>(
            () => client.Rest.SendAsync<JsonElement>(HttpMethod.Get, "https://example.com/api/"));

        Assert.Contains("different origin", exception.Message);
    }

    [Fact]
    public async Task BinaryBodyHonorsRequestTimeoutAfterResponseHeaders()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(
            server,
            requestTimeout: TimeSpan.FromMilliseconds(100));

        var exception = await Assert.ThrowsAsync<HomeAssistantConnectionException>(
            () => client.Rest.GetBytesAsync("api/test/stall"));

        Assert.IsType<TimeoutException>(exception.InnerException);
    }

    [Fact]
    public async Task BinaryBodyRejectsDeclaredResponseAboveConfiguredLimit()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, maximumRestResponseBytes: 1024);

        var exception = await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Rest.GetBytesAsync("api/test/oversize"));

        Assert.Contains("size limit", exception.Message);
    }

    [Fact]
    public async Task InvalidJsonIsClassifiedAsAProtocolFailure()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var exception = await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Rest.SendAsync<JsonElement>(HttpMethod.Get, "api/test/invalid-json"));

        Assert.IsType<JsonException>(exception.InnerException);
    }

    private static RefreshingAccessTokenProvider CreateUnexpiredRefreshingProvider(
        HomeAssistantOAuthClient oauth)
    {
        return new RefreshingAccessTokenProvider(
            oauth,
            new Uri("https://app.example.net/"),
            new HomeAssistantOAuthTokens
            {
                AccessToken = "locally-unexpired-but-rejected",
                RefreshToken = "oauth-refresh-token",
                ExpiresInSeconds = 1800,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20)
            });
    }

    private sealed class BlockingRecoveryProvider :
        IHomeAssistantAccessTokenProvider,
        IHomeAssistantAccessTokenRecovery
    {
        private int _recoveryCount;

        public int RecoveryCount => Volatile.Read(ref _recoveryCount);

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult("rejected-static-token");
        }

        public async Task RecoverAccessTokenAsync(
            string rejectedAccessToken,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _recoveryCount);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }
    }
}
