using HomeAssistantX.Rest;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Operations;

/// <summary>Groups reusable operational APIs used by troubleshooting and administration clients.</summary>
public sealed class HomeAssistantOperationsClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantOperationsClient(
        HomeAssistantRestClient rest,
        HomeAssistantWebSocketClient webSocket,
        HomeAssistantStateClient states,
        HomeAssistantServiceClient services)
    {
        _rest = rest;
        _webSocket = webSocket;
        Logs = new HomeAssistantLogClient(rest, webSocket);
        Repairs = new HomeAssistantRepairClient(webSocket);
        Health = new HomeAssistantSystemHealthClient(webSocket);
        Integrations = new HomeAssistantIntegrationClient(rest, webSocket);
        Traces = new HomeAssistantTraceClient(webSocket);
        Updates = new HomeAssistantUpdateClient(states, services, webSocket);
        Diagnostics = new HomeAssistantDiagnosticsClient(rest, webSocket);
    }

    public HomeAssistantLogClient Logs { get; }

    public HomeAssistantRepairClient Repairs { get; }

    public HomeAssistantSystemHealthClient Health { get; }

    public HomeAssistantIntegrationClient Integrations { get; }

    public HomeAssistantTraceClient Traces { get; }

    public HomeAssistantUpdateClient Updates { get; }

    public HomeAssistantDiagnosticsClient Diagnostics { get; }

    /// <summary>Discovers install-type and component capabilities without changing the server.</summary>
    public async Task<HomeAssistantCapabilityReport> GetCapabilitiesAsync(
        CancellationToken cancellationToken = default)
    {
        var configuration = await _rest.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
        var components = await _rest.GetComponentsAsync(cancellationToken).ConfigureAwait(false);
        var componentSet = new HashSet<string>(components, StringComparer.OrdinalIgnoreCase);
        string? installationType = null;
        bool? supervisorManaged = componentSet.Contains("hassio") ? true : null;
        var supervisorAvailability = supervisorManaged == true
            ? HomeAssistantCapabilityAvailability.Unknown
            : HomeAssistantCapabilityAvailability.NotInstalled;
        string? supervisorDetail = supervisorManaged == true
            ? "Supervisor is present; checking access for the current connection."
            : "Supervisor is not present or was not reported by this installation.";

        try
        {
            var health = await Health.GetAsync(cancellationToken).ConfigureAwait(false);
            if (health.Domains.TryGetValue("homeassistant", out var homeAssistant)
                && homeAssistant.ValueKind == System.Text.Json.JsonValueKind.Object
                && homeAssistant.TryGetProperty("info", out var info)
                && info.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                installationType = TryGetString(info, "installation_type");
                if (info.TryGetProperty("hassio", out var hassio)
                    && (hassio.ValueKind == System.Text.Json.JsonValueKind.True
                        || hassio.ValueKind == System.Text.Json.JsonValueKind.False))
                {
                    supervisorManaged = hassio.GetBoolean();
                }
            }
        }
        catch (Exceptions.HomeAssistantCommandException)
        {
            // system_health is optional; component-based capability discovery remains useful.
        }

        if (supervisorManaged == true)
        {
            try
            {
                _ = await _webSocket.RequestAsync(
                    "supervisor/api",
                    new Dictionary<string, object?>
                    {
                        ["endpoint"] = "/info",
                        ["method"] = "get"
                    },
                    cancellationToken).ConfigureAwait(false);
                supervisorAvailability = HomeAssistantCapabilityAvailability.Available;
                supervisorDetail = null;
            }
            catch (Exceptions.HomeAssistantCommandException exception)
            {
                var unauthorized = exception.Code.IndexOf("author", StringComparison.OrdinalIgnoreCase) >= 0;
                supervisorAvailability = unauthorized
                    ? HomeAssistantCapabilityAvailability.NotAuthorized
                    : HomeAssistantCapabilityAvailability.Unavailable;
                supervisorDetail = unauthorized
                    ? "Supervisor is present, but the current connection is not authorized for administrative operations."
                    : "Supervisor is present, but its API is unavailable through the current connection.";
            }
        }

        var capabilities = new[]
        {
            Available("rest"),
            Available("websocket"),
            FromComponent("history", "recorder", componentSet),
            FromComponent("logbook", "logbook", componentSet),
            FromComponent("system_log", "system_log", componentSet),
            FromComponent("repairs", "repairs", componentSet),
            FromComponent("diagnostics", "diagnostics", componentSet),
            FromComponents("traces", new[] { "automation", "script" }, componentSet),
            FromComponent("updates", "update", componentSet),
            FromComponent("backups", "backup", componentSet),
            new HomeAssistantCapability
            {
                Name = "supervisor",
                Availability = supervisorAvailability,
                Detail = supervisorDetail
            }
        };

        return new HomeAssistantCapabilityReport
        {
            CoreVersion = configuration.Version,
            InstallationType = installationType,
            IsSupervisorManaged = supervisorManaged,
            LoadedComponents = components.OrderBy(value => value, StringComparer.OrdinalIgnoreCase).ToArray(),
            Capabilities = capabilities
        };
    }

    private static HomeAssistantCapability Available(string name)
    {
        return new HomeAssistantCapability
        {
            Name = name,
            Availability = HomeAssistantCapabilityAvailability.Available
        };
    }

    private static HomeAssistantCapability FromComponent(
        string name,
        string component,
        ISet<string> components)
    {
        return new HomeAssistantCapability
        {
            Name = name,
            Availability = components.Contains(component)
                ? HomeAssistantCapabilityAvailability.Available
                : HomeAssistantCapabilityAvailability.NotInstalled,
            Detail = components.Contains(component) ? null : "Component '" + component + "' is not loaded."
        };
    }

    private static HomeAssistantCapability FromComponents(
        string name,
        IReadOnlyList<string> candidates,
        ISet<string> components)
    {
        var available = candidates.Any(components.Contains);
        return new HomeAssistantCapability
        {
            Name = name,
            Availability = available
                ? HomeAssistantCapabilityAvailability.Available
                : HomeAssistantCapabilityAvailability.NotInstalled,
            Detail = available ? null : "None of the required components are loaded."
        };
    }

    private static string? TryGetString(System.Text.Json.JsonElement value, string name)
    {
        return value.TryGetProperty(name, out var property)
            && property.ValueKind == System.Text.Json.JsonValueKind.String
                ? property.GetString()
                : null;
    }
}
