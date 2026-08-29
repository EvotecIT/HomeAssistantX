using System.Text.Json;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Dashboards;

/// <summary>Validates native Home Assistant dashboard values.</summary>
public static class HomeAssistantDashboardIdentifier
{
    private const int MaximumSelectorLength = 255;

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

        var length = end - start + 1;
        if (length <= 0)
        {
            normalized = string.Empty;
            return false;
        }
        normalized = CancellationAwareString.Slice(value, start, length, cancellationToken);
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
        => TryNormalizeIcon(value, out normalized, default);

    internal static bool TryNormalizeIcon(
        string? value,
        out string normalized,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeSelector(value, out normalized, cancellationToken)) return false;
        for (var index = 0; index < normalized.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (normalized[index] == ':') return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    internal static bool TryNormalizeSelector(
        string? value,
        out string normalized,
        CancellationToken cancellationToken)
        => TryNormalizeBounded(value, MaximumSelectorLength, out normalized, cancellationToken);

    internal static string RequireSelector(
        string? value,
        string parameterName,
        CancellationToken cancellationToken)
        => RequireBounded(value, MaximumSelectorLength, parameterName, cancellationToken);

    internal static string RequireTitle(
        string? value,
        string parameterName,
        CancellationToken cancellationToken)
        => RequireText(value, parameterName, preserveWhitespace: true, cancellationToken);

    internal static string RequireResourceUrl(
        string? value,
        string parameterName,
        CancellationToken cancellationToken)
        => RequireText(value, parameterName, preserveWhitespace: true, cancellationToken);

    private static string RequireText(
        string? value,
        string parameterName,
        bool preserveWhitespace,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) throw new ArgumentException("A non-empty value is required.", parameterName);
        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            if ((start & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            start++;
        }
        var end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            if (((value.Length - 1 - end) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            end--;
        }
        var length = end - start + 1;
        cancellationToken.ThrowIfCancellationRequested();
        if (length <= 0) throw new ArgumentException("A non-empty value is required.", parameterName);
        for (var index = start; index <= end; index++)
        {
            if (((index - start) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return preserveWhitespace || start == 0 && length == value.Length
            ? value
            : value.Substring(start, length);
    }

    private static string RequireBounded(
        string? value,
        int maximumLength,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (!TryNormalizeBounded(value, maximumLength, out var normalized, cancellationToken))
            throw new ArgumentException(
                "A non-empty value no longer than " + maximumLength + " characters is required.",
                parameterName);
        return normalized;
    }

    private static bool TryNormalizeBounded(
        string? value,
        int maximumLength,
        out string normalized,
        CancellationToken cancellationToken)
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
            if ((start & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            start++;
        }
        var end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            if (((value.Length - 1 - end) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            end--;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var length = end - start + 1;
        if (length <= 0 || length > maximumLength)
        {
            normalized = string.Empty;
            return false;
        }
        normalized = start == 0 && length == value.Length ? value : value.Substring(start, length);
        cancellationToken.ThrowIfCancellationRequested();
        return true;
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
