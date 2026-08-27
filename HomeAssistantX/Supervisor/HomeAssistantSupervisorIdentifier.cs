namespace HomeAssistantX.Supervisor;

/// <summary>Validates native Home Assistant Supervisor identifiers.</summary>
public static class HomeAssistantSupervisorIdentifier
{
    /// <summary>Trims and validates a Home Assistant Supervisor app/add-on slug.</summary>
    public static bool TryNormalizeAppSlug(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || normalized == "." || normalized == "..")
        {
            return false;
        }

        return normalized.All(character =>
            (character >= 'a' && character <= 'z')
            || (character >= 'A' && character <= 'Z')
            || (character >= '0' && character <= '9')
            || character == '-'
            || character == '_'
            || character == '.');
    }
}
