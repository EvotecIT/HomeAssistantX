using System.Text.Json;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Dashboards;

/// <summary>Validates native Home Assistant dashboard values.</summary>
public static class HomeAssistantDashboardIdentifier
{
    internal static bool TryNormalizeUrlPath(
        string? value,
        bool allowSingleWord,
        out string normalized,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null)
        {
            normalized = string.Empty;
            return false;
        }

        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            start++;
        }

        var end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            cancellationToken.ThrowIfCancellationRequested();
            end--;
        }

        normalized = end < start ? string.Empty : value.Substring(start, end - start + 1);
        if (normalized.Length == 0 || normalized[0] == '-' || normalized[normalized.Length - 1] == '-') return false;

        var hasHyphen = false;
        var previousWasHyphen = false;
        foreach (var character in normalized)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isHyphen = character == '-';
            if (!isHyphen
                && !(character >= 'a' && character <= 'z')
                && !(character >= '0' && character <= '9'))
            {
                return false;
            }

            if (isHyphen && previousWasHyphen) return false;
            hasHyphen |= isHyphen;
            previousWasHyphen = isHyphen;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return allowSingleWord || hasHyphen;
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
