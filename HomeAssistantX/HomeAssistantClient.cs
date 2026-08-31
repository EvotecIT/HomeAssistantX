using System.Net.Http;
using HomeAssistantX.Authentication;
using HomeAssistantX.Configuration;
using HomeAssistantX.Controls;
using HomeAssistantX.Events;
using HomeAssistantX.Operations;
using HomeAssistantX.Inventory;
using HomeAssistantX.Registries;
using HomeAssistantX.Rest;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.Systems;
using HomeAssistantX.Supervisor;
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
        Services = new HomeAssistantServiceClient(Rest, WebSocket, options);
        Events = new HomeAssistantEventClient(WebSocket);
        Registries = new HomeAssistantRegistryClient(WebSocket);
        Inventory = new HomeAssistantInventoryClient(Registries, States, Services);
        Controls = new HomeAssistantControlsClient(Services, States, options);
        System = new HomeAssistantSystemClient(WebSocket);
        Operations = new HomeAssistantOperationsClient(Rest, WebSocket, States, Services);
        Supervisor = HomeAssistantSupervisorClient.CreateViaCore(Rest, WebSocket);
    }

    public HomeAssistantClientOptions Options { get; }

    public HomeAssistantRestClient Rest { get; }

    public HomeAssistantWebSocketClient WebSocket { get; }

    public HomeAssistantStateClient States { get; }

    public HomeAssistantServiceClient Services { get; }

    public HomeAssistantEventClient Events { get; }

    public HomeAssistantRegistryClient Registries { get; }

    /// <summary>Provides a joined, queryable view of the house and its available actions.</summary>
    public HomeAssistantInventoryClient Inventory { get; }

    /// <summary>Provides typed controls for common Home Assistant domains.</summary>
    public HomeAssistantControlsClient Controls { get; }

    /// <summary>Provides documented Home Assistant system, validation, target, and authentication commands.</summary>
    public HomeAssistantSystemClient System { get; }

    /// <summary>Provides health, log, repairs, integration, trace, update, and backup operations.</summary>
    public HomeAssistantOperationsClient Operations { get; }

    /// <summary>
    /// Provides Supervisor operations through Home Assistant Core's authenticated WebSocket proxy.
    /// The current Home Assistant user must be an administrator and the installation must include Supervisor.
    /// </summary>
    public HomeAssistantSupervisorClient Supervisor { get; }

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
        Supervisor.Dispose();
        WebSocket.Dispose();
        Rest.Dispose();
    }
}
