using System.Net.Http;
using HomeAssistantX.Authentication;
using HomeAssistantX.Configuration;
using HomeAssistantX.Events;
using HomeAssistantX.Registries;
using HomeAssistantX.Rest;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.Systems;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX;

/// <summary>The main entry point for typed and raw Home Assistant API access.</summary>
public sealed class HomeAssistantClient : IDisposable
{
    private int _disposed;

    public HomeAssistantClient(HomeAssistantClientOptions options, HttpClient? httpClient = null)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        Rest = new HomeAssistantRestClient(options, httpClient);
        WebSocket = new HomeAssistantWebSocketClient(options);
        States = new HomeAssistantStateClient(Rest, WebSocket, options);
        Services = new HomeAssistantServiceClient(Rest, WebSocket);
        Events = new HomeAssistantEventClient(WebSocket);
        Registries = new HomeAssistantRegistryClient(WebSocket);
        System = new HomeAssistantSystemClient(WebSocket);
    }

    public HomeAssistantClientOptions Options { get; }

    public HomeAssistantRestClient Rest { get; }

    public HomeAssistantWebSocketClient WebSocket { get; }

    public HomeAssistantStateClient States { get; }

    public HomeAssistantServiceClient Services { get; }

    public HomeAssistantEventClient Events { get; }

    public HomeAssistantRegistryClient Registries { get; }

    /// <summary>Provides documented Home Assistant system, validation, target, and authentication commands.</summary>
    public HomeAssistantSystemClient System { get; }

    public static HomeAssistantClient Create(Uri baseUri, string accessToken)
    {
        return new HomeAssistantClient(
            new HomeAssistantClientOptions(baseUri, new StaticAccessTokenProvider(accessToken)));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        States.Dispose();
        WebSocket.Dispose();
        Rest.Dispose();
    }
}
