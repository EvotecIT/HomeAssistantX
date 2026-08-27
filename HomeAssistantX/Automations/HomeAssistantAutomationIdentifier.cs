using System.Text.Json;

namespace HomeAssistantX.Automations;

/// <summary>Normalizes administrator-managed Home Assistant automation configuration identifiers.</summary>
public static class HomeAssistantAutomationIdentifier
{
    /// <summary>Returns a trimmed non-empty automation configuration identifier.</summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="automationId"/> is empty or whitespace.</exception>
    public static string NormalizeConfigurationId(string automationId)
    {
        if (string.IsNullOrWhiteSpace(automationId))
            throw new ArgumentException("An automation configuration identifier is required.", nameof(automationId));
        return automationId.Trim();
    }

    internal static void ValidateDefinitionForSave(string automationId, JsonElement definition, string parameterName)
    {
        if (definition.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("An automation definition JSON object is required.", parameterName);
        if (HasDuplicateProperties(definition))
            throw new ArgumentException("An automation definition cannot contain duplicate JSON properties.", parameterName);

        var definitionIds = definition.EnumerateObject()
            .Where(property => property.NameEquals("id"))
            .Select(property => property.Value)
            .ToArray();
        if (definitionIds.Length > 1
            || (definitionIds.Length == 1
                && (definitionIds[0].ValueKind != JsonValueKind.String
                    || !string.Equals(definitionIds[0].GetString(), automationId, StringComparison.Ordinal))))
        {
            throw new ArgumentException("An automation definition identifier must match the requested automation identifier.", parameterName);
        }
    }

    internal static bool HasDuplicateProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().Any(HasDuplicateProperties);
        }
        if (value.ValueKind != JsonValueKind.Object) return false;

        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            if (!names.Add(property.Name) || HasDuplicateProperties(property.Value)) return true;
        }
        return false;
    }
}
