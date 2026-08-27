using HomeAssistantX.States;
using HomeAssistantX.Rest;
using HomeAssistantX.Operations;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Recorder;
using HomeAssistantX.Weather;
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
        var automationCategories = await client.Registries.GetCategoriesAsync("automation");
        var inventory = await client.Inventory.GetSnapshotAsync();
        var mediaPlayers = await client.Controls.MediaPlayers.GetAllAsync();
        var remotes = await client.Controls.Remotes.GetAllAsync();
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

        var statisticCount = 0;
        if (components.Contains("recorder", StringComparer.OrdinalIgnoreCase))
        {
            var statistics = await client.Recorder.ListStatisticsAsync();
            statisticCount = statistics.Count;
            _ = await client.Recorder.ValidateStatisticsAsync();
            if (statistics.Count > 0)
            {
                _ = await client.Recorder.GetStatisticsAsync(new HomeAssistantStatisticsQuery(
                    DateTimeOffset.UtcNow.AddHours(-2),
                    HomeAssistantStatisticPeriod.Hour,
                    statistics[0].StatisticId));
            }
        }

        var energyConfigured = false;
        var energySolarForecastProviders = 0;
        if (components.Contains("energy", StringComparer.OrdinalIgnoreCase))
        {
            var energyInfo = await client.Energy.GetInfoAsync();
            energySolarForecastProviders = energyInfo.SolarForecastDomains.Count;
            _ = await client.Energy.ValidateAsync();
            _ = await client.Energy.GetSolarForecastAsync();
            try
            {
                _ = await client.Energy.GetPreferencesAsync();
                energyConfigured = true;
            }
            catch (HomeAssistantCommandException exception) when (exception.Code == "not_found")
            {
            }
        }

        var weather = await client.Weather.GetAsync();
        if (weather.Count > 0)
        {
            _ = await client.Weather.GetConvertibleUnitsAsync();
            var supportedForecast = new[]
            {
                HomeAssistantWeatherForecastType.Daily,
                HomeAssistantWeatherForecastType.Hourly,
                HomeAssistantWeatherForecastType.TwiceDaily
            }.FirstOrDefault(weather[0].Supports);
            if (weather[0].Supports(supportedForecast))
            {
                var forecastReceived = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var weatherSubscription = await client.Weather.SubscribeForecastAsync(
                    weather[0].EntityId,
                    supportedForecast,
                    (_, _) => { forecastReceived.TrySetResult(true); return Task.CompletedTask; });
                await forecastReceived.Task.WaitAsync(TimeSpan.FromSeconds(15));
                await weatherSubscription.StopAsync();
            }
        }

        var calendars = Array.Empty<HomeAssistantCalendar>();
        var calendarEventCount = 0;
        if (components.Contains("calendar", StringComparer.OrdinalIgnoreCase))
        {
            calendars = (await client.Calendars.GetAsync()).ToArray();
            if (calendars.Length > 0)
            {
                var rangeStart = DateTimeOffset.Now;
                var rangeEnd = rangeStart.AddDays(30);
                calendarEventCount = (await client.Calendars.GetEventsAsync(calendars[0].EntityId, rangeStart, rangeEnd)).Count;
                var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                using var calendarSubscription = await client.Calendars.SubscribeAsync(
                    calendars[0].EntityId,
                    rangeStart,
                    rangeEnd,
                    (_, _) => { received.TrySetResult(true); return Task.CompletedTask; });
                if (calendarEventCount > 0)
                {
                    await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
                }
                await calendarSubscription.StopAsync();
            }
        }

        var persistentNotificationCount = 0;
        if (components.Contains("persistent_notification", StringComparer.OrdinalIgnoreCase))
        {
            persistentNotificationCount = (await client.Notifications.GetPersistentAsync()).Count;
            var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var notificationSubscription = await client.Notifications.SubscribePersistentAsync(
                (_, _) => { received.TrySetResult(true); return Task.CompletedTask; });
            await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await notificationSubscription.StopAsync();
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
            var supervisorInfo = await client.Supervisor.GetInfoAsync();
            var supervisorOverview = await client.Supervisor.GetOverviewAsync();
            Assert.False(string.IsNullOrWhiteSpace(supervisorInfo.Version));
            Assert.False(string.IsNullOrWhiteSpace(supervisorOverview.CoreVersion));
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
        Assert.NotEmpty(inventory.Entities);
        Assert.NotEmpty(inventory.Actions);
        Assert.All(restStates, state => Assert.Contains(inventory.Entities, entity => entity.EntityId == state.EntityId));
        Assert.Equal(restStates.Count(state => state.Domain == "media_player"), mediaPlayers.Count);
        Assert.Equal(restStates.Count(state => state.Domain == "remote"), remotes.Count);
        Assert.All(mediaPlayers, status => Assert.Equal("media_player", status.RawState.Domain));
        Assert.All(remotes, status => Assert.Equal("remote", status.RawState.Domain));
        Assert.Equal(configuration.Version, capabilities.CoreVersion);
        Assert.NotEmpty(integrations);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, pong.ValueKind);
        Assert.Equal(configuration.Version, webSocketConfiguration.Version);
        Assert.NotEmpty(webSocketStates);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, webSocketServices.ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, panels.ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Object, displayRegistry.ValueKind);
        Assert.StartsWith("/api/", signedPath);
        _output.WriteLine("Home Assistant {0}: REST states={1}, WebSocket states={2}, joined entities={3}, actions={4}, event types={5}, updates={6}, system log entries={7}, repairs={8}, diagnostic handlers={9}, Supervisor apps={10}, backups={11}, media players={12}, remotes={13}, labels={14}, automation categories={15}, calendars={16}, sampled calendar events={17}, persistent notifications={18}, statistics={19}, Energy configured={20}, solar forecast providers={21}, weather entities={22}",
            configuration.Version,
            restStates.Count,
            webSocketStates.Count,
            inventory.Entities.Count,
            inventory.Actions.Count,
            eventTypes.Count,
            updates.Count,
            systemLog.Count,
            repairIssues.Count,
            diagnosticHandlers.Count,
            supervisorApps,
            supervisorBackups,
            mediaPlayers.Count,
            remotes.Count,
            registries.Labels.Count,
            automationCategories.Count,
            calendars.Length,
            calendarEventCount,
            persistentNotificationCount,
            statisticCount,
            energyConfigured,
            energySolarForecastProviders,
            weather.Count);
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
