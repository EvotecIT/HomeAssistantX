using HomeAssistantX.States;
using HomeAssistantX.Rest;
using HomeAssistantX.Operations;
using Xunit.Abstractions;

namespace HomeAssistantX.LiveTests;

public sealed class LiveHomeAssistantTests
{
    private readonly ITestOutputHelper _output;

    public LiveHomeAssistantTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [LiveFact]
    public async Task ReadOnlyRestAndWebSocketContractsWorkAgainstARealHomeAssistant()
    {
        var baseUri = new Uri(Environment.GetEnvironmentVariable("HOME_ASSISTANT_URL")!, UriKind.Absolute);
        var token = Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN")!;
        using var client = HomeAssistantClient.Create(baseUri, token);

        var api = await client.Rest.CheckApiAsync();
        var configuration = await client.Rest.GetConfigurationAsync();
        var restStates = await client.States.GetAllAsync();
        var components = await client.Rest.GetComponentsAsync();
        var eventTypes = await client.Rest.GetEventTypesAsync();
        var serviceCatalog = await client.Services.GetCatalogAsync();
        await client.WebSocket.ConnectAsync();
        var pong = await client.WebSocket.PingAsync();
        var webSocketStates = await client.States.GetAllWebSocketAsync();
        var webSocketConfiguration = await client.System.GetConfigurationAsync();
        var webSocketServices = await client.Services.GetCatalogWebSocketAsync();
        var panels = await client.System.GetPanelsAsync();
        var displayRegistry = await client.System.GetEntityRegistryForDisplayAsync();
        var signedPath = await client.System.SignPathAsync("/api/");
        var registries = await client.Registries.GetSnapshotAsync();
        var capabilities = await client.Operations.GetCapabilitiesAsync();
        var integrations = await client.Operations.Integrations.GetAllAsync();
        var updates = await client.Operations.Updates.GetAllAsync();
        using var subscription = await client.States.SubscribeAsync(
            HomeAssistantStateFilter.All,
            (_, _) => Task.CompletedTask);
        await subscription.StopAsync();

        if (components.Contains("recorder", StringComparer.OrdinalIgnoreCase))
        {
            var firstEntity = restStates[0].EntityId;
            _ = await client.Rest.GetHistoryAsync(new HomeAssistantHistoryQuery(firstEntity)
            {
                StartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
                MinimalResponse = true,
                NoAttributes = true
            });
        }

        if (components.Contains("calendar", StringComparer.OrdinalIgnoreCase))
        {
            _ = await client.Rest.GetCalendarsAsync();
        }

        IReadOnlyList<HomeAssistantSystemLogEntry> systemLog = Array.Empty<HomeAssistantSystemLogEntry>();
        if (components.Contains("system_log", StringComparer.OrdinalIgnoreCase))
        {
            systemLog = await client.Operations.Logs.GetSystemLogAsync();
        }

        IReadOnlyList<HomeAssistantRepairIssue> repairIssues = Array.Empty<HomeAssistantRepairIssue>();
        if (components.Contains("repairs", StringComparer.OrdinalIgnoreCase))
        {
            repairIssues = await client.Operations.Repairs.GetIssuesAsync(includeIgnored: true);
        }

        IReadOnlyList<HomeAssistantDiagnosticHandler> diagnosticHandlers = Array.Empty<HomeAssistantDiagnosticHandler>();
        if (components.Contains("diagnostics", StringComparer.OrdinalIgnoreCase))
        {
            diagnosticHandlers = await client.Operations.Diagnostics.GetHandlersAsync();
        }

        var supervisorApps = 0;
        var supervisorBackups = 0;
        var supervisorCapability = capabilities.Capabilities.Single(item => item.Name == "supervisor");
        if (supervisorCapability.Availability == HomeAssistantCapabilityAvailability.Available)
        {
            _ = await client.Supervisor.GetInfoAsync();
            _ = await client.Supervisor.GetCoreInfoAsync();
            _ = await client.Supervisor.GetAvailableUpdatesAsync();
            supervisorApps = (await client.Supervisor.GetAppsAsync()).Count;
            supervisorBackups = (await client.Supervisor.GetBackupsAsync()).Count;
            _ = await client.Supervisor.GetJobsAsync();
            _ = await client.Supervisor.GetResolutionAsync();
        }

        Assert.False(string.IsNullOrWhiteSpace(api.Message));
        Assert.False(string.IsNullOrWhiteSpace(configuration.Version));
        Assert.NotEmpty(restStates);
        Assert.NotEmpty(components);
        Assert.NotEmpty(eventTypes);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, serviceCatalog.ValueKind);
        Assert.NotEmpty(registries.Entities);
        Assert.NotEmpty(registries.Devices);
        Assert.Equal(configuration.Version, capabilities.CoreVersion);
        Assert.NotEmpty(integrations);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, pong.ValueKind);
        Assert.Equal(configuration.Version, webSocketConfiguration.Version);
        Assert.NotEmpty(webSocketStates);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, webSocketServices.ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, panels.ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, displayRegistry.ValueKind);
        Assert.StartsWith("/api/", signedPath);
        _output.WriteLine("Home Assistant {0}: REST states={1}, WebSocket states={2}, registry entities={3}, event types={4}, updates={5}, system log entries={6}, repairs={7}, diagnostic handlers={8}, Supervisor apps={9}, backups={10}",
            configuration.Version,
            restStates.Count,
            webSocketStates.Count,
            registries.Entities.Count,
            eventTypes.Count,
            updates.Count,
            systemLog.Count,
            repairIssues.Count,
            diagnosticHandlers.Count,
            supervisorApps,
            supervisorBackups);
    }
}

internal sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOME_ASSISTANT_URL"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN")))
        {
            Skip = "Set HOME_ASSISTANT_URL and HOME_ASSISTANT_TOKEN to run read-only live validation.";
        }
    }
}
