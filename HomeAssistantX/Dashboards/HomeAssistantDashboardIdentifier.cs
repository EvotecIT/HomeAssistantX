using System.Text.Json;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Dashboards;

/// <summary>Validates native Home Assistant dashboard values.</summary>
public static class HomeAssistantDashboardIdentifier
{
    internal static bool TryNormalizeUrlPath(string? value, bool allowSingleWord, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || (!allowSingleWord && normalized.IndexOf('-') < 0)) return false;
        if (normalized[0] == '-' || normalized[normalized.Length - 1] == '-' || normalized.Contains("--")) return false;
        return normalized.All(character =>
            (character >= 'a' && character <= 'z')
            || (character >= '0' && character <= '9')
            || character == '-');
    }

    /// <summary>Trims and validates Home Assistant's colon-delimited icon selector form.</summary>
    public static bool TryNormalizeIcon(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0 && normalized.IndexOf(':') >= 0;
    }

    internal static void ValidateConfigurationForSave(
        JsonElement configuration,
        string parameterName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (configuration.ValueKind != JsonValueKind.Object)
            throw new ArgumentException("A Lovelace configuration JSON object is required.", parameterName);
        if (HomeAssistantJson.HasDuplicateProperties(configuration, cancellationToken))
            throw new ArgumentException("A Lovelace configuration cannot contain duplicate JSON properties.", parameterName);
    }
}
