using HomeAssistantX.Mcp;
using HomeAssistantX.Tests.Infrastructure;
using ModelContextProtocol.Client;
using System.Text.Json;

namespace HomeAssistantX.Mcp.Tests;

public sealed class McpTransportTests
{
    [Fact]
    public async Task StdioHandshakeExposesFocusedToolsAndBlocksWritesByDefault()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var assemblyPath = typeof(HomeAssistantMcpAccess).Assembly.Location;
        var executablePath = Environment.GetEnvironmentVariable("HOMEASSISTANTX_MCP_TEST_EXECUTABLE")
            ?? Path.Combine(
                Path.GetDirectoryName(assemblyPath)!,
                "HomeAssistantX.Mcp" + (OperatingSystem.IsWindows() ? ".exe" : ""));
        var transport = new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "HomeAssistantX contract test",
            Command = executablePath,
            EnvironmentVariables = new Dictionary<string, string?>
            {
                ["HOMEASSISTANTX_URL"] = "http://127.0.0.1:1/",
                ["HOMEASSISTANTX_TOKEN"] = "test-token",
                ["HOMEASSISTANTX_MCP_ALLOW_CHANGES"] = "false"
            }
        });
        await using var client = await McpClient.CreateAsync(transport, cancellationToken: timeout.Token);
        var tools = await client.ListToolsAsync(cancellationToken: timeout.Token);

        var overview = Assert.Single(tools, tool => tool.Name == "get_home_overview");
        Assert.True(overview.ProtocolTool.Annotations?.ReadOnlyHint);
        Assert.Contains(tools, tool => tool.Name == "find_home_entities");
        Assert.Contains(tools, tool => tool.Name == "validate_home_automation_draft");
        var save = Assert.Single(tools, tool => tool.Name == "save_home_automation_definition");
        Assert.True(save.ProtocolTool.Annotations?.DestructiveHint);
        var schema = save.JsonSchema.GetRawText();
        Assert.Contains("expectedRevision", schema);
        Assert.DoesNotContain("HomeAssistantClient", schema);
        Assert.DoesNotContain("HomeAssistantMcpAccess", schema);

        var result = await client.CallToolAsync(
            save.Name,
            new Dictionary<string, object?>
            {
                ["automationId"] = "contract-test",
                ["definitionJson"] = "{\"alias\":\"Contract test\",\"triggers\":[],\"actions\":[]}",
                ["expectedRevision"] = "new"
            },
            cancellationToken: timeout.Token);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task AutomationWriteGateFailsBeforeContactingHomeAssistant()
    {
        using var client = HomeAssistantClient.Create(new Uri("http://127.0.0.1:1/"), "test-token");
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HomeAssistantMcpTools.SaveHomeAutomationDefinition(
                client,
                new HomeAssistantMcpAccess(false),
                "contract-test",
                "{\"alias\":\"Contract test\",\"triggers\":[],\"actions\":[]}",
                "new",
                CancellationToken.None));
        Assert.Contains("disabled", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task OperatorToolsReadLoopbackHomeAndValidateDraft()
    {
        using var server = new TestHomeAssistantServer
        {
            RepairIssuesResponseJson = "{\"issues\":[{\"domain\":\"test\",\"issue_id\":\"warning-1\",\"active\":true,\"ignored\":false},{\"domain\":\"test\",\"issue_id\":\"resolved-1\",\"active\":false,\"ignored\":false},{\"domain\":\"test\",\"issue_id\":\"ignored-1\",\"active\":true,\"ignored\":true}]}"
        };
        using var client = HomeAssistantClient.Create(server.BaseUri, TestHomeAssistantServer.AccessToken);

        var overview = JsonSerializer.SerializeToElement(
            await HomeAssistantMcpTools.GetHomeOverview(client, CancellationToken.None));
        Assert.Equal("NotInstalled", overview.GetProperty("RepairsAvailability").GetString());
        Assert.Equal(JsonValueKind.Null, overview.GetProperty("ActiveIssueCount").ValueKind);

        var issues = await HomeAssistantMcpTools.GetHomeIssues(client, CancellationToken.None);
        Assert.Single(issues);
        Assert.Equal("warning-1", issues[0].IssueId);

        var draft = JsonSerializer.SerializeToElement(
            await HomeAssistantMcpTools.ValidateHomeAutomationDraft(
                client,
                "{\"alias\":\"Test\",\"triggers\":[],\"actions\":[]}",
                CancellationToken.None));
        Assert.Equal(JsonValueKind.Object, draft.GetProperty("Validation").ValueKind);

        var current = JsonSerializer.SerializeToElement(
            await HomeAssistantMcpTools.GetHomeAutomationDefinition(
                client, "morning-routine", CancellationToken.None));
        var revision = current.GetProperty("Revision").GetString()!;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            HomeAssistantMcpTools.SaveHomeAutomationDefinition(
                client,
                new HomeAssistantMcpAccess(true),
                "morning-routine",
                "{\"alias\":\"Edited\",\"triggers\":[],\"actions\":[]}",
                "new",
                CancellationToken.None));

        await HomeAssistantMcpTools.SaveHomeAutomationDefinition(
            client,
            new HomeAssistantMcpAccess(true),
            "morning-routine",
            "{\"alias\":\"Edited\",\"triggers\":[],\"actions\":[]}",
            revision,
            CancellationToken.None);
        Assert.Contains("\"alias\":\"Edited\"", server.LastRequestBody);

        var created = JsonSerializer.SerializeToElement(
            await HomeAssistantMcpTools.SaveHomeAutomationDefinition(
                client,
                new HomeAssistantMcpAccess(true),
                "new-routine",
                "{\"id\":\"new-routine\",\"alias\":\"New\",\"triggers\":[],\"actions\":[]}",
                "new",
                CancellationToken.None));
        Assert.True(created.GetProperty("Created").GetBoolean());
        Assert.Contains("\"alias\":\"New\"", server.LastRequestBody);
    }
}
