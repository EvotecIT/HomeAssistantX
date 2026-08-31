using System.Net.Http;
using HomeAssistantX.Authentication;
using HomeAssistantX.Automations;
using HomeAssistantX.Configuration;
using HomeAssistantX.Controls;
using HomeAssistantX.Calendars;
using HomeAssistantX.Cameras;
using HomeAssistantX.Dashboards;
using HomeAssistantX.Events;
using HomeAssistantX.Energy;
using HomeAssistantX.Operations;
using HomeAssistantX.Inventory;
using HomeAssistantX.Media;
using HomeAssistantX.Registries;
using HomeAssistantX.Notifications;
using HomeAssistantX.Recorder;
using HomeAssistantX.Rest;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.Systems;
using HomeAssistantX.Supervisor;
using HomeAssistantX.WebSockets;
using HomeAssistantX.Weather;

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
        Calendars = new HomeAssistantCalendarClient(Rest, WebSocket);
        Notifications = new HomeAssistantNotificationClient(Services, WebSocket);
        Energy = new HomeAssistantEnergyClient(WebSocket, Rest);
        Recorder = new HomeAssistantRecorderClient(Rest, WebSocket, Services);
        Weather = new HomeAssistantWeatherClient(States, Services, WebSocket);
        Inventory = new HomeAssistantInventoryClient(Registries, States, Services);
        Controls = new HomeAssistantControlsClient(Services, States, options);
        System = new HomeAssistantSystemClient(WebSocket);
        Cameras = new HomeAssistantCameraClient(Rest, States, WebSocket, System);
        Media = new HomeAssistantMediaBrowserClient(WebSocket);
        Dashboards = new HomeAssistantDashboardClient(WebSocket);
        Automations = new HomeAssistantAutomationClient(States, Rest, Services);
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

    /// <summary>Provides calendar discovery, event management, and live event-list subscriptions.</summary>
    public HomeAssistantCalendarClient Calendars { get; }

    /// <summary>Provides persistent and targeted notifications, including live persistent-notification updates.</summary>
    public HomeAssistantNotificationClient Notifications { get; }

    /// <summary>Provides Energy dashboard preferences, validation, forecasts, and fossil-energy calculations.</summary>
    public HomeAssistantEnergyClient Energy { get; }

    /// <summary>Provides Recorder statistics and maintenance operations.</summary>
    public HomeAssistantRecorderClient Recorder { get; }

    /// <summary>Provides typed current weather observations and push-based forecasts.</summary>
    public HomeAssistantWeatherClient Weather { get; }

    /// <summary>Provides camera state, bounded snapshots, streams, signed paths, preferences, and push updates.</summary>
    public HomeAssistantCameraClient Cameras { get; }

    /// <summary>Provides Home Assistant-native media-source and media-player browsing, search, and resolution.</summary>
    public HomeAssistantMediaBrowserClient Media { get; }

    /// <summary>Provides typed frontend panel and Lovelace dashboard, configuration, and resource access.</summary>
    public HomeAssistantDashboardClient Dashboards { get; }

    /// <summary>Provides automation runtime state/execution and administrator-only editable definitions.</summary>
    public HomeAssistantAutomationClient Automations { get; }

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
