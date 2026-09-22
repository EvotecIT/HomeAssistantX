using System.ComponentModel;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Automations;
using ModelContextProtocol.Server;

namespace HomeAssistantX.Mcp;

/// <summary>Task-oriented Home Assistant tools backed by the shared HomeAssistantX client.</summary>
[McpServerToolType]
public sealed class HomeAssistantMcpTools
{
    [McpServerTool(Name = "get_home_overview", ReadOnly = true), Description("Read Home Assistant version, capability availability, and counts of active issues and available updates. Makes no changes.")]
    public static async Task<object> GetHomeOverview(HomeAssistantClient client, CancellationToken cancellationToken)
    {
        var capabilities = await client.Operations.GetCapabilitiesAsync(cancellationToken);
        var repairs = capabilities.Capabilities.Single(capability => capability.Name == "repairs");
        int? activeIssueCount = null;
        if (repairs.Availability == HomeAssistantX.Operations.HomeAssistantCapabilityAvailability.Available)
        {
            var issues = await client.Operations.Repairs.GetIssuesAsync(cancellationToken: cancellationToken);
            activeIssueCount = issues.Count(issue => issue.Active);
        }
        var updates = await client.Operations.Updates.GetAllAsync(availableOnly: true, cancellationToken);
        return new
        {
            capabilities.CoreVersion,
            capabilities.InstallationType,
            capabilities.IsSupervisorManaged,
            capabilities.Capabilities,
            RepairsAvailability = repairs.Availability.ToString(),
            ActiveIssueCount = activeIssueCount,
            AvailableUpdateCount = updates.Count
        };
    }

    [McpServerTool(Name = "find_home_entities", ReadOnly = true), Description("Find entities by room, domain, or name using the joined Home Assistant inventory. Returns at most 50 matches with live state and integration context.")]
    public static async Task<object> FindHomeEntities(
        HomeAssistantClient client,
        CancellationToken cancellationToken,
        [Description("Optional entity name or part of a name.")] string? name = null,
        [Description("Optional room or area name.")] string? area = null,
        [Description("Optional domain such as light, sensor, or media_player.")] string? domain = null)
    {
        var matches = await client.Inventory.GetEntitiesAsync(new()
        {
            Name = name,
            Area = area,
            Domain = domain
        }, cancellationToken);
        return new
        {
            Total = matches.Count,
            Entities = matches.Take(50).Select(entity => new
            {
                entity.EntityId,
                entity.Name,
                entity.Domain,
                entity.State,
                entity.IsAvailable,
                entity.AreaName,
                entity.DeviceName,
                entity.IntegrationTitle
            }).ToArray()
        };
    }

    [McpServerTool(Name = "get_home_issues", ReadOnly = true), Description("Read active Home Assistant Repairs issues and their severity. Makes no changes.")]
    public static async Task<IReadOnlyList<HomeAssistantX.Operations.HomeAssistantRepairIssue>> GetHomeIssues(
        HomeAssistantClient client, CancellationToken cancellationToken)
    {
        var issues = await client.Operations.Repairs.GetIssuesAsync(cancellationToken: cancellationToken);
        return issues.Where(issue => issue.Active).ToArray();
    }

    [McpServerTool(Name = "get_home_updates", ReadOnly = true), Description("Read currently available Home Assistant update entities. Makes no changes.")]
    public static Task<IReadOnlyList<HomeAssistantX.Operations.HomeAssistantUpdate>> GetHomeUpdates(
        HomeAssistantClient client, CancellationToken cancellationToken)
        => client.Operations.Updates.GetAllAsync(availableOnly: true, cancellationToken);

    [McpServerTool(Name = "get_home_traces", ReadOnly = true), Description("List recent execution traces for one automation or script. Returns at most 20 runs with outcome and error context. Makes no changes.")]
    public static async Task<object> GetHomeTraces(
        HomeAssistantClient client,
        [Description("'automation' or 'script'.")] string domain,
        [Description("Home Assistant automation or script item id.")] string itemId,
        CancellationToken cancellationToken)
    {
        var runs = await client.Operations.Traces.GetAllAsync(domain, itemId, cancellationToken);
        return new
        {
            Total = runs.Count,
            Runs = runs.Take(20).Select(run => new
            {
                run.Domain,
                run.ItemId,
                run.RunId,
                run.State,
                run.ScriptExecution,
                run.LastStep,
                run.Error,
                run.Timestamp
            }).ToArray()
        };
    }

    [McpServerTool(Name = "get_home_trace", ReadOnly = true), Description("Read one automation or script execution trace by run id. The trace can contain private home data. Makes no changes.")]
    public static Task<JsonElement> GetHomeTrace(
        HomeAssistantClient client,
        [Description("'automation' or 'script'.")] string domain,
        [Description("Home Assistant automation or script item id.")] string itemId,
        [Description("Run id from get_home_traces.")] string runId,
        CancellationToken cancellationToken)
        => client.Operations.Traces.GetAsync(domain, itemId, runId, cancellationToken);

    [McpServerTool(Name = "get_home_errors", ReadOnly = true), Description("Read up to 30 recent aggregated Home Assistant system log entries. Entries can contain private home data.")]
    public static async Task<object> GetHomeErrors(HomeAssistantClient client, CancellationToken cancellationToken)
    {
        var entries = await client.Operations.Logs.GetSystemLogAsync(cancellationToken);
        return new
        {
            Total = entries.Count,
            Entries = entries.OrderByDescending(entry => entry.Timestamp).Take(30).ToArray()
        };
    }

    [McpServerTool(Name = "get_home_actions", ReadOnly = true), Description("Discover service actions and required fields for an integration domain before designing an automation. Makes no changes.")]
    public static async Task<object> GetHomeActions(
        HomeAssistantClient client,
        [Description("Integration domain such as light, climate, or notify.")] string domain,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(domain)) throw new ArgumentException("A domain is required.", nameof(domain));
        var actions = await client.Services.GetActionsAsync(cancellationToken);
        return actions.Where(action => string.Equals(action.Domain, domain, StringComparison.OrdinalIgnoreCase))
            .Select(action => new
            {
                action.Domain,
                action.Action,
                action.Name,
                action.Description,
                Fields = action.Fields.Select(field => new { field.Field, field.Name, field.Required, field.Description, field.Selector }).ToArray()
            }).ToArray();
    }

    [McpServerTool(Name = "get_home_automations", ReadOnly = true), Description("List automation runtime state and configuration identifiers when Home Assistant exposes them. Makes no changes.")]
    public static async Task<object> GetHomeAutomations(
        HomeAssistantClient client, CancellationToken cancellationToken)
    {
        var automations = await client.Automations.GetAsync(cancellationToken);
        return automations.Select(automation => new
        {
            automation.EntityId,
            automation.ConfigurationId,
            automation.Name,
            automation.IsEnabled,
            automation.LastTriggered,
            automation.Mode,
            automation.CurrentRuns
        }).ToArray();
    }

    [McpServerTool(Name = "get_home_automation_definition", ReadOnly = true), Description("Read one editable automation definition and its revision fingerprint before planning an update. Requires Home Assistant administrator access.")]
    public static async Task<object> GetHomeAutomationDefinition(
        HomeAssistantClient client,
        [Description("Configuration id of the automation, distinct from its entity id.")] string automationId,
        CancellationToken cancellationToken)
    {
        var configuration = await client.Automations.GetConfigurationAsync(automationId, cancellationToken);
        return new
        {
            configuration.AutomationId,
            configuration.Definition,
            Revision = await HomeAssistantMcpRevision.CreateAsync(configuration.Definition, cancellationToken)
        };
    }

    [McpServerTool(Name = "validate_home_automation_draft", ReadOnly = true), Description("Ask Home Assistant to validate the trigger, condition, and action fragments of an automation JSON draft. Makes no changes.")]
    public static async Task<object> ValidateHomeAutomationDraft(
        HomeAssistantClient client,
        [Description("Complete automation definition as a JSON object.")] string definitionJson,
        CancellationToken cancellationToken)
    {
        var draft = await HomeAssistantAutomationDraft.ParseAsync(definitionJson, cancellationToken);
        var validation = await client.Automations.ValidateDraftAsync(draft.Definition, cancellationToken);
        return new { draft.Definition, Validation = validation };
    }

    [McpServerTool(Name = "save_home_automation_definition", Destructive = true), Description("Create or replace one automation definition after local write opt-in. Use revision 'new' to create, or the fingerprint from get_home_automation_definition to update.")]
    public static async Task<object> SaveHomeAutomationDefinition(
        HomeAssistantClient client,
        HomeAssistantMcpAccess access,
        [Description("Configuration id of the automation.")] string automationId,
        [Description("Complete automation definition as a JSON object.")] string definitionJson,
        [Description("'new' for creation, or the revision fingerprint returned by get_home_automation_definition for replacement.")] string expectedRevision,
        CancellationToken cancellationToken)
    {
        access.RequireChanges();
        if (string.IsNullOrWhiteSpace(expectedRevision))
            throw new ArgumentException("An expected revision is required.", nameof(expectedRevision));
        var draft = await HomeAssistantAutomationDraft.ParseAsync(definitionJson, cancellationToken);

        HomeAssistantX.Automations.HomeAssistantAutomationConfiguration? existing;
        try
        {
            existing = await client.Automations.GetConfigurationAsync(automationId, cancellationToken);
        }
        catch (HomeAssistantCommandException exception) when (exception.Code == "http_404")
        {
            existing = null;
        }

        if (existing is null && expectedRevision != "new")
            throw new InvalidOperationException("The automation no longer exists. Read its current state before saving.");
        if (existing is not null && !string.Equals(
                await HomeAssistantMcpRevision.CreateAsync(existing.Definition, cancellationToken),
                expectedRevision,
                StringComparison.Ordinal))
            throw new InvalidOperationException("The automation changed since it was read. Fetch its current definition before saving.");

        var response = await client.Automations.SaveConfigurationAsync(automationId, draft.Definition, cancellationToken);
        return new { AutomationId = automationId, Created = existing is null, Response = response };
    }
}
