namespace HomeAssistantX.Models;

internal static class HomeAssistantEntityId
{
    internal static bool TryNormalizeForDomain(
        string? value,
        string domain,
        out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        var separator = normalized.IndexOf('.');
        if (separator <= 0
            || separator != normalized.LastIndexOf('.')
            || separator == normalized.Length - 1
            || !string.Equals(normalized.Substring(0, separator), domain, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return normalized.Substring(separator + 1).All(character =>
            (character >= 'a' && character <= 'z')
            || (character >= 'A' && character <= 'Z')
            || (character >= '0' && character <= '9')
            || character == '_');
    }
}
