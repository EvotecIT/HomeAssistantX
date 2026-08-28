using System.Text.Json;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Automations;

/// <summary>Normalizes administrator-managed Home Assistant automation configuration identifiers.</summary>
public static class HomeAssistantAutomationIdentifier
{
    /// <summary>Returns a trimmed non-empty automation configuration identifier.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="automationId"/> is empty or whitespace.</exception>
    public static string NormalizeConfigurationId(string automationId)
        => NormalizeConfigurationId(automationId, default);

    internal static string NormalizeConfigurationId(
        string automationId,
        CancellationToken cancellationToken)
    {
        if (automationId is null)
            throw new ArgumentNullException(nameof(automationId));
        cancellationToken.ThrowIfCancellationRequested();
        var start = 0;
        while (start < automationId.Length && char.IsWhiteSpace(automationId[start]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            start++;
        }

        var end = automationId.Length - 1;
        while (end >= start && char.IsWhiteSpace(automationId[end]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            end--;
        }

        if (end < start)
            throw new ArgumentException("An automation configuration identifier is required.", nameof(automationId));
        for (var index = start; index <= end; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
        }

        cancellationToken.ThrowIfCancellationRequested();
        return automationId.Substring(start, end - start + 1);
    }

    internal static void ValidateDefinitionForSave(
        string automationId,
        JsonElement definition,
        string parameterName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (definition.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("An automation definition JSON object is required.", parameterName);
        if (HomeAssistantJson.HasDuplicateProperties(definition, cancellationToken))
            throw new ArgumentException("An automation definition cannot contain duplicate JSON properties.", parameterName);

        JsonElement? definitionId = null;
        foreach (var property in definition.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!property.NameEquals("id"))
            {
                continue;
            }

            if (definitionId.HasValue)
            {
                throw new ArgumentException("An automation definition cannot contain duplicate JSON properties.", parameterName);
            }

            definitionId = property.Value;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (definitionId.HasValue
            && (definitionId.Value.ValueKind != JsonValueKind.String
                || !string.Equals(definitionId.Value.GetString(), automationId, StringComparison.Ordinal)))
        {
            throw new ArgumentException("An automation definition identifier must match the requested automation identifier.", parameterName);
        }
    }

    internal static bool HasDuplicateProperties(
        JsonElement value,
        CancellationToken cancellationToken = default)
        => HomeAssistantJson.HasDuplicateProperties(value, cancellationToken);
}
