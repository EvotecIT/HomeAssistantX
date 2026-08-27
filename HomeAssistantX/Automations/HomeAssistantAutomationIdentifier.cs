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
}
