using HomeAssistantX.Authentication;
using System.Net;
using System.Net.Http;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class OAuthContractTests
{
    [Fact]
    public void AuthorizationUriEscapesLongStateAcrossTargetFrameworks()
    {
        using var oauth = new HomeAssistantOAuthClient(new Uri("https://ha.example.net/"));
        var state = "state-" + new string('a', 40_000);

        var authorization = oauth.BuildAuthorizationUri(
            new Uri("https://client.example.net/"),
            new Uri("casaray://auth/callback"),
            state);

        Assert.Contains("state=state-", authorization.Query, StringComparison.Ordinal);
        Assert.True(authorization.Query.Length > state.Length);
    }

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

    [Theory]
    [InlineData(429)]
    [InlineData(503)]
    public async Task TransientTokenEndpointStatusDoesNotInvalidateCredentials(int statusCode)
    {
        using var httpClient = new HttpClient(new ResponseHandler(() => new HttpResponseMessage((HttpStatusCode)statusCode)
        {
            Content = new StringContent("{\"error\":\"temporarily_unavailable\"}")
        }));
        using var oauth = new HomeAssistantOAuthClient(new Uri("https://ha.example.net/"), httpClient);

        var exception = await Assert.ThrowsAsync<HomeAssistantConnectionException>(
            () => oauth.RefreshAsync(new Uri("https://app.example.net/"), "still-valid-refresh-token"));

        Assert.IsType<HttpRequestException>(exception.InnerException);
        Assert.Contains(statusCode.ToString(), exception.InnerException!.Message);
    }

    [Fact]
    public async Task CredentialRejectionRemainsAnAuthenticationFailure()
    {
        using var httpClient = new HttpClient(new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"invalid_grant\"}")
        }));
        using var oauth = new HomeAssistantOAuthClient(new Uri("https://ha.example.net/"), httpClient);

        await Assert.ThrowsAsync<HomeAssistantAuthenticationException>(
            () => oauth.RefreshAsync(new Uri("https://app.example.net/"), "rejected-refresh-token"));
    }

    [Fact]
    public async Task ResponseStreamIoFailureIsClassifiedAsConnectionFailure()
    {
        using var httpClient = new HttpClient(new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new ThrowingReadStream())
        }));
        using var oauth = new HomeAssistantOAuthClient(new Uri("https://ha.example.net/"), httpClient);

        var exception = await Assert.ThrowsAsync<HomeAssistantConnectionException>(
            () => oauth.ExchangeCodeAsync(
                new Uri("https://app.example.net/"),
                new Uri("homeassistantx://auth/callback"),
                "authorization-code"));

        Assert.IsType<IOException>(exception.InnerException);
    }

    [Fact]
    public async Task CancellationDisposesAStalledOAuthResponseStream()
    {
        using var stalledStream = new BlockingReadStream();
        using var httpClient = new HttpClient(new ResponseHandler(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stalledStream)
        }));
        using var oauth = new HomeAssistantOAuthClient(new Uri("https://ha.example.net/"), httpClient);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var exchange = oauth.ExchangeCodeAsync(
            new Uri("https://app.example.net/"),
            new Uri("homeassistantx://auth/callback"),
            "authorization-code",
            cancellation.Token);
        var completed = await Task.WhenAny(exchange, Task.Delay(TimeSpan.FromSeconds(5)));

        Assert.Same(exchange, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => exchange);
        Assert.True(stalledStream.WasDisposed);
    }

    private sealed class ResponseHandler : HttpMessageHandler
    {
        private readonly Func<HttpResponseMessage> _responseFactory;

        public ResponseHandler(Func<HttpResponseMessage> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_responseFactory());
        }
    }

    private sealed class ThrowingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new IOException("Connection reset.");

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) => Task.FromException<int>(new IOException("Connection reset."));

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class BlockingReadStream : Stream
    {
        private readonly TaskCompletionSource<int> _read = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool WasDisposed { get; private set; }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) => _read.Task;

        protected override void Dispose(bool disposing)
        {
            if (disposing && !WasDisposed)
            {
                WasDisposed = true;
                _read.TrySetException(new ObjectDisposedException(nameof(BlockingReadStream)));
            }

            base.Dispose(disposing);
        }

        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
