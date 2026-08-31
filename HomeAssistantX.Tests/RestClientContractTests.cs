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
    public async Task TypedEntityRestPathsRejectNoncanonicalIdentifiersBeforeIo()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.GetStateAsync("sensor.Kitchen"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.SetStateAsync("sensor.Kitchen", new HomeAssistantX.Rest.HomeAssistantStateUpdate("on")));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.DeleteStateAsync("sensor.Kitchen"));
        Assert.Throws<ArgumentException>(() => new HomeAssistantX.Rest.HomeAssistantHistoryQuery("sensor.Kitchen"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.GetLogbookAsync(new HomeAssistantX.Rest.HomeAssistantLogbookQuery { EntityId = "sensor.Kitchen" }));
    }

    [Fact]
    public async Task TypedStateReadRejectsAValidButDifferentResponseEntity()
    {
        using var server = new TestHomeAssistantServer
        {
            ExactStateResponseJson = "{\"entity_id\":\"sensor.other\",\"state\":\"on\",\"attributes\":{}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Rest.GetStateAsync("sensor.requested"));
    }

    [Fact]
    public async Task TypedStateMutationRejectsAValidButDifferentResponseEntity()
    {
        using var server = new TestHomeAssistantServer
        {
            StateMutationResponseJson = "{\"entity_id\":\"sensor.other\",\"state\":\"ready\",\"attributes\":{}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Rest.SetStateAsync("sensor.virtual", new HomeAssistantX.Rest.HomeAssistantStateUpdate("ready")));
    }

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
        Assert.Equal("Case-distinct extension", configuration.AdditionalData["Location_Name"].GetString());
        Assert.Equal(42, configuration.AdditionalData["custom_field"].GetInt32());
        Assert.Equal(2, states.Count);
        Assert.Equal("sensor", temperature.Domain);
        Assert.True(temperature.TryGetAttribute<string>("unit_of_measurement", out var unit));
        Assert.Equal("°C", unit);
        Assert.Equal("good", temperature.Attributes["nested"].GetProperty("quality").GetString());
        Assert.True(temperature.TryGetAttribute<ConsumerAttribute>("nested", out var nested));
        Assert.Equal("good", nested!.Quality);
        Assert.Equal("test", temperature.AdditionalData["custom_state_field"].GetProperty("source").GetString());
        Assert.Equal("state-trace", temperature.Context!.AdditionalData["trace_hint"].GetString());
        Assert.Equal("Bearer " + TestHomeAssistantServer.AccessToken, server.LastAuthorization);
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("[[null]]")]
    public async Task RestHistoryRejectsNullSeriesAndRows(string responseJson)
    {
        using var server = new TestHomeAssistantServer { HistoryResponseJson = responseJson };
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantX.Rest.HomeAssistantHistoryQuery("sensor.kitchen_temperature")
        {
            StartTime = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero)
        };

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Rest.GetHistoryAsync(query));
    }

    [Fact]
    public async Task ConfigurationRejectsNullEntriesInRequiredComponentCollection()
    {
        using var server = new TestHomeAssistantServer
        {
            ConfigurationResponseJson = "{\"components\":[\"api\",null]}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Rest.GetConfigurationAsync());
    }

    [Fact]
    public async Task RawGenericResponsesRemainCaseInsensitiveForConsumerDtos()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var response = await client.Rest.SendAsync<ConsumerResponse>(
            HttpMethod.Get,
            "api/test/raw-dto");

        Assert.Equal(1, response.Value);
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
    public async Task AuthenticationRecoveryRetriesTheExactFrozenRequestBody()
    {
        var body = new Dictionary<string, object?> { ["value"] = "original" };
        var provider = new SwitchingRecoveryProvider();
        var handler = new BodyCapturingRecoveryHandler(body);
        using var httpClient = new HttpClient(handler);
        var options = new HomeAssistantX.Configuration.HomeAssistantClientOptions(
            new Uri("https://home.example.net/"),
            provider);
        using var client = new HomeAssistantX.Rest.HomeAssistantRestClient(options, httpClient);

        var response = await client.SendAsync<JsonElement>(HttpMethod.Post, "api/test", body);

        Assert.True(response.GetProperty("ok").GetBoolean());
        Assert.Equal(2, handler.Bodies.Count);
        Assert.All(handler.Bodies, value => Assert.Equal("original", value));
        Assert.Equal("mutated", body["value"]);
        Assert.Equal(1, provider.RecoveryCount);
    }

    [Fact]
    public async Task PreCancelledRequestsDoNotTraverseTheCallerBody()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Rest.SendAsync<JsonElement>(
            HttpMethod.Post,
            "api/test",
            new ThrowingSerializationBody(),
            cancellation.Token));

        Assert.Equal(0, server.AuthenticatedRequestCount);
        Assert.Equal(0, server.UnauthorizedRequestCount);
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
    public async Task BuiltInResponseValidationHonorsRequestTimeout()
    {
        using var httpClient = new HttpClient(new ImmediateJsonHandler("{\"values\":[]}"));
        var options = new HomeAssistantX.Configuration.HomeAssistantClientOptions(
            new Uri("https://home.example.net/"),
            new StaticAccessTokenProvider("test-token"))
        {
            RequestTimeout = TimeSpan.FromMilliseconds(75)
        };
        using var client = new HomeAssistantX.Rest.HomeAssistantRestClient(options, httpClient);

        var exception = await Assert.ThrowsAsync<HomeAssistantConnectionException>(() =>
            client.SendHomeAssistantAsync<SlowValidationResponse>(HttpMethod.Get, "api/test"));

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

    private sealed class SwitchingRecoveryProvider :
        IHomeAssistantAccessTokenProvider,
        IHomeAssistantAccessTokenRecovery
    {
        private string _token = "rejected-token";
        private int _recoveryCount;

        public int RecoveryCount => Volatile.Read(ref _recoveryCount);

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(Volatile.Read(ref _token));
        }

        public Task RecoverAccessTokenAsync(string rejectedAccessToken, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("rejected-token", rejectedAccessToken);
            Interlocked.Increment(ref _recoveryCount);
            Volatile.Write(ref _token, "accepted-token");
            return Task.CompletedTask;
        }
    }

    private sealed class BodyCapturingRecoveryHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, object?> _callerBody;
        private int _attempt;

        public BodyCapturingRecoveryHandler(Dictionary<string, object?> callerBody)
        {
            _callerBody = callerBody;
        }

        public List<string?> Bodies { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
            Bodies.Add(document.RootElement.GetProperty("value").GetString());
            if (Interlocked.Increment(ref _attempt) == 1)
            {
                _callerBody["value"] = "mutated";
                return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized)
                {
                    Content = new StringContent("{\"message\":\"Unauthorized\"}")
                };
            }

            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("accepted-token", request.Headers.Authorization?.Parameter);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            };
        }
    }

    private sealed class ImmediateJsonHandler : HttpMessageHandler
    {
        private readonly string _json;

        public ImmediateJsonHandler(string json)
        {
            _json = json;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent(_json)
            });
        }
    }

    private sealed class SlowValidationResponse
    {
        private IReadOnlyList<string>? _values;

        public IReadOnlyList<string>? Values
        {
            get
            {
                Thread.Sleep(150);
                return _values;
            }
            set => _values = value;
        }
    }

    private sealed class ConsumerResponse
    {
        public int Value { get; set; }
    }

    private sealed class ThrowingSerializationBody
    {
        public string Value => throw new InvalidOperationException("The cancelled request body was serialized.");
    }

    private sealed class ConsumerAttribute
    {
        public string? Quality { get; set; }
    }
}
