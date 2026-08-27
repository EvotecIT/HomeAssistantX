using System.Text;
using System.Text.Json;
using HomeAssistantX.Operations;
using HomeAssistantX.Supervisor;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class OperationsContractTests
{
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
        server.SetStates("[{\"entity_id\":\"update.home_assistant_core_update\",\"state\":\"on\",\"attributes\":{\"title\":\"Home Assistant Core\",\"installed_version\":\"2026.8.3\",\"latest_version\":\"2026.8.4\",\"in_progress\":false,\"update_percentage\":null}}]");
        using var client = TestClientFactory.Create(server);

        var updates = await client.Operations.Updates.GetAllAsync(availableOnly: true);
        var notes = await client.Operations.Updates.GetReleaseNotesAsync(" " + updates[0].EntityId + " ");
        await client.Operations.Updates.InstallAsync(updates[0].EntityId, backup: true);

        Assert.Single(updates);
        Assert.Equal("2026.8.4", updates[0].LatestVersion);
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

        Assert.Null(server.GetLastWebSocketCommand("update/release_notes"));
        Assert.Null(server.LastServiceCallBody);
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
        await client.Supervisor.InvokeAppAsync("test_app", HomeAssistantAppOperation.Restart);

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
        Assert.Equal("/addons/test_app/restart", command.RootElement.GetProperty("endpoint").GetString());
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

        Assert.Equal("2026.08.0", info.Version);
        Assert.Equal("2026.8.3", overview.CoreVersion);
        Assert.Single(updates);
        Assert.Single(apps);
        Assert.Single(backups);
        Assert.Contains("direct supervisor log line", log);
        Assert.Equal("Bearer " + TestHomeAssistantServer.AccessToken, server.LastAuthorization);
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
}
