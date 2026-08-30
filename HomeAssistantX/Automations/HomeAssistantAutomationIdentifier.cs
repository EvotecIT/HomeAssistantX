using System.Text.Json;
using HomeAssistantX.Configuration;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Automations;

/// <summary>Normalizes administrator-managed Home Assistant automation configuration identifiers.</summary>
public static class HomeAssistantAutomationIdentifier
{
    /// <summary>Returns a non-empty automation configuration identifier without changing provider-defined characters.</summary>
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
        var hasContent = false;
        for (var index = 0; index < automationId.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!char.IsWhiteSpace(automationId[index])) hasContent = true;
        }

        if (!hasContent)
            throw new ArgumentException("An automation configuration identifier is required.", nameof(automationId));
        cancellationToken.ThrowIfCancellationRequested();
        return automationId;
    }

    internal static string EscapeConfigurationId(
        string automationId,
        CancellationToken cancellationToken)
        => HomeAssistantUri.EscapeDataString(automationId, cancellationToken);

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
        if (definitionId.HasValue)
        {
            var definitionIdValue = definitionId.Value.ValueKind == JsonValueKind.String
                ? HomeAssistantJson.GetString(definitionId.Value, cancellationToken)
                : null;
            if (definitionIdValue is null
                || !CancellationAwareString.EqualsOrdinal(definitionIdValue, automationId, cancellationToken))
            {
                throw new ArgumentException("An automation definition identifier must match the requested automation identifier.", parameterName);
            }
        }
    }

    internal static bool HasDuplicateProperties(
        JsonElement value,
        CancellationToken cancellationToken = default)
        => HomeAssistantJson.HasDuplicateProperties(value, cancellationToken);
}
