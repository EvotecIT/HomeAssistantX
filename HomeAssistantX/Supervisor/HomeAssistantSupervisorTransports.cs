using System.Net.Http;
using System.Text.Json;
using HomeAssistantX.Configuration;
using HomeAssistantX.Rest;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Supervisor;

internal sealed class CoreSupervisorTransport : IHomeAssistantSupervisorTransport
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;

    public CoreSupervisorTransport(HomeAssistantRestClient rest, HomeAssistantWebSocketClient webSocket)
    {
        _rest = rest;
        _webSocket = webSocket;
    }

    public Task<JsonElement> SendAsync(
        HttpMethod method,
        string endpoint,
        object? data,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["endpoint"] = endpoint,
            ["method"] = method.Method.ToLowerInvariant()
        };
        if (data is not null)
        {
            payload["data"] = data;
        }

        return _webSocket.RequestAsync("supervisor/api", payload, cancellationToken);
    }

    public Task<string> SendTextAsync(
        HttpMethod method,
        string endpoint,
        object? data,
        CancellationToken cancellationToken)
    {
        if (method != HttpMethod.Get || data is not null)
        {
            throw new NotSupportedException("The Home Assistant Core Supervisor proxy supports text only for GET requests.");
        }

        return _rest.SendTextAsync(method, "api/hassio" + endpoint, null, cancellationToken);
    }
}

internal sealed class DirectSupervisorTransport : IHomeAssistantSupervisorTransport, IDisposable
{
    private readonly HomeAssistantRestClient _rest;

    public DirectSupervisorTransport(HomeAssistantSupervisorClientOptions options, HttpClient? httpClient)
    {
        if (options.RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options.RequestTimeout));
        }

        if (options.MaximumResponseBytes < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(options.MaximumResponseBytes));
        }

        _rest = new HomeAssistantRestClient(
            new HomeAssistantClientOptions(options.BaseUri, options.AccessTokenProvider)
            {
                RequestTimeout = options.RequestTimeout,
                MaximumRestResponseBytes = options.MaximumResponseBytes
            },
            httpClient);
    }

    public Task<JsonElement> SendAsync(
        HttpMethod method,
        string endpoint,
        object? data,
        CancellationToken cancellationToken)
    {
        return _rest.SendHomeAssistantAsync<JsonElement>(method, endpoint.TrimStart('/'), data, cancellationToken);
    }

    public Task<string> SendTextAsync(
        HttpMethod method,
        string endpoint,
        object? data,
        CancellationToken cancellationToken)
    {
        return _rest.SendTextAsync(method, endpoint.TrimStart('/'), data, cancellationToken);
    }

    public void Dispose()
    {
        _rest.Dispose();
    }
}
