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

    /// <summary>Parses and snapshots a JSON definition without making cancellation wait for synchronous JSON parsing.</summary>
    public static async Task<HomeAssistantAutomationDraft> ParseAsync(
        string definitionJson,
        CancellationToken cancellationToken = default)
    {
        if (definitionJson is null) throw new ArgumentNullException(nameof(definitionJson));
        cancellationToken.ThrowIfCancellationRequested();

        // The worker owns the document through both parsing and snapshotting. The caller may
        // stop waiting on cancellation without disposing a document the worker is still reading.
        var parseTask = Task.Run(() =>
        {
            using var document = JsonDocument.Parse(definitionJson);
            return ParseCore(document.RootElement, cancellationToken);
        }, CancellationToken.None);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        if (await Task.WhenAny(parseTask, canceled.Task).ConfigureAwait(false) != parseTask)
        {
            _ = parseTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var draft = await parseTask.ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        return draft;
    }

    /// <summary>Snapshots a caller-owned JSON definition on a worker; keep its document alive until the returned task completes.</summary>
    public static Task<HomeAssistantAutomationDraft> ParseAsync(
        JsonElement definition,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Awaiting this worker also keeps the borrowed source document alive through cancellation.
        return Task.Run(() => ParseCore(definition, cancellationToken), CancellationToken.None);
    }

    /// <summary>Snapshots a definition and rejects ambiguous fragment names before validation or save.</summary>
    public static HomeAssistantAutomationDraft Parse(JsonElement definition, CancellationToken cancellationToken = default)
        => HomeAssistantJson.RunCancellationIsolated(
            () => ParseCore(definition, cancellationToken),
            cancellationToken);

    private static HomeAssistantAutomationDraft ParseCore(JsonElement definition, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (definition.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("An automation definition must be a JSON object.", nameof(definition));
        if (HomeAssistantJson.HasDuplicatePropertiesInline(definition, cancellationToken))
            throw new ArgumentException("An automation definition cannot contain duplicate JSON properties.", nameof(definition));

        var frozen = HomeAssistantJson.FreezeValue(definition, nameof(definition), "Automation definition", cancellationToken);
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
