using System.Text.Json;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Automations;

/// <summary>Identifies the native fragments of an automation definition for Home Assistant validation.</summary>
public sealed class HomeAssistantAutomationDraft
{
    private HomeAssistantAutomationDraft(JsonElement definition, JsonElement trigger, JsonElement? condition, JsonElement action)
    {
        Definition = definition;
        Trigger = trigger;
        Condition = condition;
        Action = action;
    }

    /// <summary>The complete definition, including unknown integration-specific fields.</summary>
    public JsonElement Definition { get; }

    /// <summary>The trigger or triggers fragment accepted by Home Assistant.</summary>
    public JsonElement Trigger { get; }

    /// <summary>The optional condition or conditions fragment.</summary>
    public JsonElement? Condition { get; }

    /// <summary>The action or actions fragment accepted by Home Assistant.</summary>
    public JsonElement Action { get; }

    /// <summary>Snapshots a definition and rejects ambiguous fragment names before validation or save.</summary>
    public static HomeAssistantAutomationDraft Parse(JsonElement definition, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (definition.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("An automation definition must be a JSON object.", nameof(definition));
        if (HomeAssistantAutomationIdentifier.HasDuplicateProperties(definition, cancellationToken))
            throw new ArgumentException("An automation definition cannot contain duplicate JSON properties.", nameof(definition));

        var frozen = HomeAssistantJson.RunCancellationIsolated(
            () => HomeAssistantJson.FreezeValue(definition, nameof(definition), "Automation definition", cancellationToken),
            cancellationToken);
        var hasTriggers = frozen.TryGetProperty("triggers", out var triggers);
        var hasTrigger = frozen.TryGetProperty("trigger", out var trigger);
        var hasActions = frozen.TryGetProperty("actions", out var actions);
        var hasAction = frozen.TryGetProperty("action", out var action);
        var hasConditions = frozen.TryGetProperty("conditions", out var conditions);
        var hasCondition = frozen.TryGetProperty("condition", out var condition);
        if (!hasTriggers && !hasTrigger || !hasActions && !hasAction)
            throw new ArgumentException("An automation definition needs triggers and actions.", nameof(definition));
        if (hasTriggers && hasTrigger || hasActions && hasAction || hasConditions && hasCondition)
            throw new ArgumentException("An automation definition cannot mix singular and plural names for the same fragment.", nameof(definition));

        cancellationToken.ThrowIfCancellationRequested();
        return new HomeAssistantAutomationDraft(
            frozen,
            hasTriggers ? triggers : trigger,
            hasConditions ? conditions : hasCondition ? condition : null,
            hasActions ? actions : action);
    }
}
