using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using System.Net.Http;
using HomeAssistantX.Configuration;
using HomeAssistantX.Diagnostics;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Operations;
using HomeAssistantX.Supervisor;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class OperationsContractTests
{
    [Fact]
    public void UpdateProjectionDoesNotCoerceNonStringIdentityAttributes()
    {
        using var title = JsonDocument.Parse("42");
        using var installed = JsonDocument.Parse("true");
        using var latest = JsonDocument.Parse("false");
        var state = new HomeAssistantState
        {
            EntityId = "update.platform",
            State = "on",
            Attributes = new Dictionary<string, JsonElement>
            {
                ["title"] = title.RootElement.Clone(),
                ["installed_version"] = installed.RootElement.Clone(),
                ["latest_version"] = latest.RootElement.Clone()
            }
        };

        var update = HomeAssistantUpdateClient.ToUpdate(state, CancellationToken.None);

        Assert.Null(update.Title);
        Assert.Null(update.InstalledVersion);
        Assert.Null(update.LatestVersion);
    }

    [Fact]
    public void UpdateProjectionPreservesCancellationAcrossProviderAttributes()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var title = JsonDocument.Parse("\"" + new string('x', 1_000_000) + "\"");
        var state = new HomeAssistantState
        {
            EntityId = "update.platform",
            State = "on",
            Attributes = new Dictionary<string, JsonElement>
            {
                ["title"] = title.RootElement.Clone()
            }
        };

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantUpdateClient.ToUpdate(state, cancellation.Token));
    }

    [Fact]
    public void SharedUriEscapingSupportsLongValuesAndCancellation()
    {
        var value = new string('a', 40000) + " /";
        var escaped = HomeAssistantUri.EscapeDataString(value, CancellationToken.None);

        Assert.StartsWith(new string('a', 40000), escaped);
        Assert.EndsWith("%20%2F", escaped);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() =>
            HomeAssistantUri.EscapeDataString(value, cancellation.Token));
    }

    [Fact]
    public async Task DiagnosticAndIntegrationPathValidationHonorsCancellationBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var identifier = new string(' ', 40000);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Operations.Diagnostics.GetConfigEntryAsync(identifier, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Operations.Integrations.ReloadAsync(identifier, cancellation.Token));

        Assert.Null(server.LastRequestPath);
    }

    [Fact]
    public async Task TypedDomainListingsRejectDuplicateEntityIdentities()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("["
            + "{\"entity_id\":\"update.home_assistant_core_update\",\"state\":\"on\",\"attributes\":{}},"
            + "{\"entity_id\":\"update.home_assistant_core_update\",\"state\":\"off\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Operations.Updates.GetAllAsync());
    }

    [Fact]
    public async Task OptionalOperationalSelectorsRejectExplicitBlanksBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Operations.Integrations.GetAllAsync(" "));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => client.Operations.Traces.GetContextsAsync(" "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Operations.Traces.GetContextsAsync(itemId: " "));

        Assert.Null(server.GetLastWebSocketCommand("config_entries/get"));
        Assert.Null(server.GetLastWebSocketCommand("trace/contexts"));
    }

#if !NET472
    [Fact]
    public async Task TraceDomainsAreCanonicalizedBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Operations.Traces.GetAllAsync(" AUTOMATION ", "night");
        using (var list = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("trace/list"))))
        {
            Assert.Equal("automation", list.RootElement.GetProperty("domain").GetString());
        }

        await client.Operations.Traces.GetAsync("SCRIPT", "night", "run-1");
        using (var get = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("trace/get"))))
        {
            Assert.Equal("script", get.RootElement.GetProperty("domain").GetString());
        }

        await client.Operations.Traces.GetContextsAsync("AUTOMATION", "night");
        using var contexts = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("trace/contexts")));
        Assert.Equal("automation", contexts.RootElement.GetProperty("domain").GetString());
    }
#endif
#if !NET472
    [Fact]
    public async Task OperationalReadsUseTheActualWebSocketAndRestContracts()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var logs = await client.Operations.Logs.GetSystemLogAsync();
        var issues = await client.Operations.Repairs.GetIssuesAsync();
        var allIssues = await client.Operations.Repairs.GetIssuesAsync(includeIgnored: true);
        var health = await client.Operations.Health.GetAsync();
        var integrations = await client.Operations.Integrations.GetAllAsync("test");
        var integration = await client.Operations.Integrations.GetAsync("entry-1");
        var reload = await client.Operations.Integrations.ReloadAsync("entry-1");
        var reconfiguration = await client.Operations.Integrations.StartReconfigurationAsync("test", "entry-1");
        var reconfigurationRequestBody = server.LastRequestBody;
        var handlers = await client.Operations.Diagnostics.GetHandlersAsync();
        var diagnostic = await client.Operations.Diagnostics.GetConfigEntryAsync("entry-1");
        var traces = await client.Operations.Traces.GetAllAsync("automation", "night");
        var trace = await client.Operations.Traces.GetAsync("automation", "night", "run-1");

        Assert.Single(logs);
        Assert.Equal("WARNING", logs[0].Level);
        Assert.Equal(2, logs[0].Count);
        Assert.Single(issues);
        Assert.Equal(2, allIssues.Count);
        Assert.Equal("warning-1", issues[0].IssueId);
        Assert.Equal("3.14.1", health.Domains["homeassistant"].GetProperty("info").GetProperty("python_version").GetString());
        Assert.True(health.Domains["test"].GetProperty("info").GetProperty("api").GetProperty("error").GetBoolean());
        Assert.Equal("Unavailable", health.Domains["test"].GetProperty("info").GetProperty("api").GetProperty("value").GetString());
        Assert.Single(integrations);
        Assert.Equal("entry-1", integration.EntryId);
        Assert.True(integration.SupportsUnload);
        Assert.False(reload.RequiresRestart);
        Assert.Equal("reconfigure", reconfiguration.GetProperty("step_id").GetString());
        using (var request = JsonDocument.Parse(Assert.IsType<string>(reconfigurationRequestBody)))
        {
            Assert.Equal("test", request.RootElement.GetProperty("handler").GetString());
            Assert.Equal("entry-1", request.RootElement.GetProperty("entry_id").GetString());
            Assert.False(request.RootElement.TryGetProperty("context", out _));
        }
        Assert.Single(handlers);
        Assert.True(handlers[0].Handlers.ConfigEntry);
        Assert.Contains("REDACTED", Encoding.UTF8.GetString(diagnostic));
        Assert.Single(traces);
        Assert.Equal("error", traces[0].ScriptExecution);
        Assert.Equal("run-1", trace.GetProperty("run_id").GetString());
        Assert.True(server.UnsubscribeCommandCount >= 1);
    }

    [Fact]
    public async Task SystemHealthDoesNotHangWhenTheSubscriptionEndsBeforeFinish()
    {
        using var server = new TestHomeAssistantServer { OmitSystemHealthFinish = true };
        var client = TestClientFactory.Create(server);
        var healthTask = client.Operations.Health.GetAsync();

        await server.WaitForSystemHealthEventsAsync();
        client.Dispose();

        var completed = await Task.WhenAny(healthTask, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(healthTask, completed);
        await Assert.ThrowsAnyAsync<Exception>(() => healthTask);
    }

    [Fact]
    public async Task SystemHealthClassifiesMalformedProjectionAsUpstreamFailure()
    {
        using var server = new TestHomeAssistantServer { SystemHealthInitialEventJson = "{\"type\":\"initial\"}" };
        var diagnostics = new RecordingDiagnosticsSink();
        using var client = TestClientFactory.Create(server, diagnostics: diagnostics);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Operations.Health.GetAsync());

        Assert.Contains(diagnostics.Events, item => item.Name == "subscription.upstream_failed");
        Assert.DoesNotContain(diagnostics.Events, item => item.Name == "subscription.handler_failed");
    }

    [Fact]
    public async Task CapabilityReportExplainsTheInstallationWithoutMutation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var report = await client.Operations.GetCapabilitiesAsync();

        Assert.Equal("2026.8.3", report.CoreVersion);
        Assert.Equal("Home Assistant OS", report.InstallationType);
        Assert.True(report.IsSupervisorManaged);
        Assert.Equal(
            HomeAssistantCapabilityAvailability.Available,
            Assert.Single(report.Capabilities, item => item.Name == "supervisor").Availability);
        Assert.Equal(
            HomeAssistantCapabilityAvailability.NotInstalled,
            Assert.Single(report.Capabilities, item => item.Name == "system_log").Availability);
    }

    [Fact]
    public async Task UpdateOperationsUseUpdateEntitiesAndOneGenericActionContract()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":\"update.home_assistant_core_update\",\"state\":\"on\",\"attributes\":{\"Title\":\"Home Assistant Core\",\"installed_version\":\"2026.8.3\",\"LATEST_VERSION\":\"2026.8.4\",\"In_Progress\":false,\"update_percentage\":null}}]");
        using var client = TestClientFactory.Create(server);

        var updates = await client.Operations.Updates.GetAllAsync(availableOnly: true);
        var notes = await client.Operations.Updates.GetReleaseNotesAsync(" " + updates[0].EntityId + " ");
        await client.Operations.Updates.InstallAsync(updates[0].EntityId, backup: true);

        Assert.Single(updates);
        Assert.Equal("Home Assistant Core", updates[0].Title);
        Assert.Equal("2026.8.4", updates[0].LatestVersion);
        Assert.False(updates[0].IsInProgress);
        Assert.Equal("Test release notes", notes);
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.Equal("update", command.RootElement.GetProperty("domain").GetString());
        Assert.Equal("install", command.RootElement.GetProperty("service").GetString());
        Assert.Equal("update.home_assistant_core_update", command.RootElement.GetProperty("target").GetProperty("entity_id")[0].GetString());
        Assert.True(command.RootElement.GetProperty("service_data").GetProperty("backup").GetBoolean());
        using var notesCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("update/release_notes")));
        Assert.Equal("update.home_assistant_core_update", notesCommand.RootElement.GetProperty("entity_id").GetString());
    }

    [Fact]
    public async Task UpdateOperationsRejectMalformedOrWrongDomainEntityIdsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Operations.Updates.GetReleaseNotesAsync("light.kitchen"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Operations.Updates.InstallAsync("update."));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Operations.Updates.InstallAsync("update.core.extra"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Operations.Updates.InstallAsync("update.Kitchen"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Operations.Updates.InstallAsync("update.core", " "));

        Assert.Null(server.GetLastWebSocketCommand("update/release_notes"));
        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task UpdateBulkReadRejectsMalformedServerEntityIds()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":\"update.core.extra\",\"state\":\"on\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Operations.Updates.GetAllAsync());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\" update.core\"")]
    public async Task UpdateBulkReadValidatesBeforeInspectingTheDomain(string entityIdJson)
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":" + entityIdJson + ",\"state\":\"on\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Operations.Updates.GetAllAsync());
    }

    [Fact]
    public async Task UpdateReleaseNotesClassifyUnexpectedServerShapes()
    {
        using var server = new TestHomeAssistantServer { ReturnInvalidUpdateReleaseNotes = true };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(
            () => client.Operations.Updates.GetReleaseNotesAsync("update.home_assistant_core_update"));
    }

    [Fact]
    public async Task CoreSupervisorProxySupportsInventoryLogsAndBoundedMutations()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var info = await client.Supervisor.GetInfoAsync();
        using (var infoCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("supervisor/api"))))
        {
            Assert.Equal("/supervisor/info", infoCommand.RootElement.GetProperty("endpoint").GetString());
        }

        var overview = await client.Supervisor.GetOverviewAsync();
        var updates = await client.Supervisor.GetAvailableUpdatesAsync();
        var apps = await client.Supervisor.GetAppsAsync();
        var backups = await client.Supervisor.GetBackupsAsync();
        var jobs = await client.Supervisor.GetJobsAsync();
        var job = await client.Supervisor.GetJobAsync("job-1");
        var resolution = await client.Supervisor.GetResolutionAsync();
        var log = await client.Supervisor.GetLogAsync(HomeAssistantSupervisorLogTarget.Core, lines: 25);
        var backup = await client.Supervisor.CreateFullBackupAsync(new HomeAssistantBackupRequest
        {
            Name = "Before update",
            Background = true,
            ExcludeDatabase = true
        });
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Supervisor.InstallUpdateAsync(HomeAssistantSupervisorUpdateTarget.App, ".."));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Supervisor.InstallUpdateAsync(HomeAssistantSupervisorUpdateTarget.Core, version: " "));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Supervisor.InvokeAppAsync("test/app", HomeAssistantAppOperation.Restart));
        server.ClearLastWebSocketCommand("supervisor/api");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Supervisor.InvokeAppAsync("test_app", (HomeAssistantAppOperation)99));
        Assert.Null(server.GetLastWebSocketCommand("supervisor/api"));
        await client.Supervisor.InvokeAppAsync(" TEST_APP ", HomeAssistantAppOperation.Restart);

        Assert.Equal("2026.08.0", info.Version);
        Assert.True(info.Healthy);
        Assert.Equal("2026.8.3", overview.CoreVersion);
        Assert.Single(updates);
        Assert.Single(apps);
        Assert.True(apps[0].Installed);
        Assert.Single(backups);
        Assert.Single(jobs);
        Assert.Equal("job-1", job.Id);
        Assert.Single(resolution.GetProperty("issues").EnumerateArray());
        Assert.Contains("test log line", log);
        Assert.Equal("job-new", backup.GetProperty("job_id").GetString());
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("supervisor/api")));
        Assert.Equal("/addons/TEST_APP/restart", command.RootElement.GetProperty("endpoint").GetString());
        Assert.Equal("post", command.RootElement.GetProperty("method").GetString());
    }
#endif

    [Fact]
    public async Task DirectSupervisorClientUsesItsOwnBearerBoundaryAndUnwrapsResponses()
    {
        using var server = new TestHomeAssistantServer();
        using var supervisor = HomeAssistantSupervisorClient.Create(server.BaseUri, TestHomeAssistantServer.AccessToken);

        var info = await supervisor.GetInfoAsync();
        Assert.Equal("/supervisor/info", server.LastRequestPath);
        var overview = await supervisor.GetOverviewAsync();
        Assert.Equal("/info", server.LastRequestPath);
        var updates = await supervisor.GetAvailableUpdatesAsync();
        Assert.Equal("/available_updates", server.LastRequestPath);
        var apps = await supervisor.GetAppsAsync();
        var backups = await supervisor.GetBackupsAsync();
        var log = await supervisor.GetLogAsync(HomeAssistantSupervisorLogTarget.Core, 10);
        await supervisor.InvokeAppAsync(" TEST_APP ", HomeAssistantAppOperation.Restart);

        Assert.Equal("2026.08.0", info.Version);
        Assert.Equal("2026.8.3", overview.CoreVersion);
        Assert.Single(updates);
        Assert.Single(apps);
        Assert.Single(backups);
        Assert.Contains("direct supervisor log line", log);
        Assert.Equal("/addons/TEST_APP/restart", server.LastRequestPath);
        Assert.Equal("Bearer " + TestHomeAssistantServer.AccessToken, server.LastAuthorization);
    }

    [Fact]
    public async Task SupervisorRoutesPrioritizeAPreCanceledTokenBeforeSelectorsAndEscaping()
    {
        using var server = new TestHomeAssistantServer();
        using var supervisor = HomeAssistantSupervisorClient.Create(server.BaseUri, TestHomeAssistantServer.AccessToken);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var longValue = new string('a', 1_000_000);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.GetJobAsync(longValue, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.GetLogAsync((HomeAssistantSupervisorLogTarget)int.MaxValue, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.RestartAsync((HomeAssistantSupervisorRestartTarget)int.MaxValue, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.InstallUpdateAsync(
                (HomeAssistantSupervisorUpdateTarget)int.MaxValue,
                app: longValue,
                version: longValue,
                cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.InvokeAppAsync(longValue, (HomeAssistantAppOperation)int.MaxValue, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            supervisor.SendAsync(HttpMethod.Get, "/" + longValue, cancellationToken: cancellation.Token));

        Assert.Null(server.LastRequestPath);
    }

    [Fact]
    public async Task SupervisorRawRequestsRejectAbsoluteOrNetworkPathTargets()
    {
        using var server = new TestHomeAssistantServer();
        using var supervisor = HomeAssistantSupervisorClient.Create(server.BaseUri, TestHomeAssistantServer.AccessToken);

        await Assert.ThrowsAsync<ArgumentException>(
            () => supervisor.SendAsync(System.Net.Http.HttpMethod.Get, "https://example.com/info"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => supervisor.SendAsync(System.Net.Http.HttpMethod.Get, "//example.com/info"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => supervisor.SendAsync(System.Net.Http.HttpMethod.Get, "/core/../host/info"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => supervisor.SendAsync(System.Net.Http.HttpMethod.Get, "/core/%2e%2e/host/info"));
        await Assert.ThrowsAsync<ArgumentException>(
            () => supervisor.SendAsync(System.Net.Http.HttpMethod.Get, "/core\\..\\host\\info"));
    }

    private sealed class RecordingDiagnosticsSink : IHomeAssistantDiagnosticsSink
    {
        private readonly ConcurrentQueue<HomeAssistantDiagnosticEvent> _events = new();

        internal IReadOnlyList<HomeAssistantDiagnosticEvent> Events => _events.ToArray();

        public void Write(HomeAssistantDiagnosticEvent diagnosticEvent) => _events.Enqueue(diagnosticEvent);
    }
}
