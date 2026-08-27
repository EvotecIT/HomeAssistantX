using System.Text.Json;
using HomeAssistantX.Models;

namespace HomeAssistantX.Automations;

public sealed class HomeAssistantAutomationStatus
{
    public string EntityId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool? IsEnabled { get; set; }
    public DateTimeOffset? LastTriggered { get; set; }
    public string? Mode { get; set; }
    public long? CurrentRuns { get; set; }
    public HomeAssistantState RawState { get; set; } = new();
}

/// <summary>An editable automation definition and its stable configuration identifier.</summary>
public sealed class HomeAssistantAutomationConfiguration
{
    public string AutomationId { get; set; } = string.Empty;
    public JsonElement Definition { get; set; }
}
