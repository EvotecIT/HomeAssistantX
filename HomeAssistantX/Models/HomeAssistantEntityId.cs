using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Models;

/// <summary>Validates and normalizes native Home Assistant entity identifiers.</summary>
public static class HomeAssistantEntityId
{
    private const int MaximumEntityIdLength = 255;
    private const int MaximumDomainLength = 64;

    /// <summary>Trims and validates a lowercase Home Assistant entity identifier in the requested domain.</summary>
    public static bool TryNormalizeForDomain(
        string? value,
        string domain,
        out string normalized)
        => TryNormalizeForDomain(value, domain, default, out normalized);

    internal static bool TryNormalizeForDomain(
        string? value,
        string domain,
        CancellationToken cancellationToken,
        out string normalized)
    {
        if (!TryNormalize(value, cancellationToken, out normalized)
            || !TryNormalizeDomain(domain, cancellationToken, out var normalizedDomain))
        {
            return false;
        }

        var separator = FindSeparator(normalized, cancellationToken, out _);
        return separator > 0
            && CancellationAwareString.EqualsOrdinal(
                CancellationAwareString.Slice(normalized, 0, separator, cancellationToken),
                normalizedDomain,
                cancellationToken);
    }

    /// <summary>Trims and validates a lowercase Home Assistant domain.</summary>
    public static bool TryNormalizeDomain(string? value, out string normalized)
        => TryNormalizeDomain(value, default, out normalized);

    internal static bool TryNormalizeDomain(
        string? value,
        CancellationToken cancellationToken,
        out string normalized)
    {
        if (!TryNormalizeBounded(value, MaximumDomainLength, cancellationToken, out normalized))
            return false;
        return IsValidSegment(
            normalized,
            mustStartWithLetter: true,
            disallowBoundaryUnderscore: true,
            disallowDoubleUnderscore: true,
            cancellationToken);
    }

    internal static string RequireResponseEntityId(string? value)
        => RequireResponseEntityId(value, default);

    internal static string RequireResponseEntityId(
        string? value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryNormalize(value, cancellationToken, out var normalized)
            || !string.Equals(value, normalized, StringComparison.Ordinal)
            || ContainsUppercase(normalized, cancellationToken))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a malformed entity identifier.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return normalized;
    }

    /// <summary>Trims and validates a lowercase Home Assistant entity identifier.</summary>
    public static bool TryNormalize(string? value, out string normalized)
        => TryNormalize(value, default, out normalized);

    internal static bool TryNormalize(
        string? value,
        CancellationToken cancellationToken,
        out string normalized)
    {
        if (!TryNormalizeBounded(value, MaximumEntityIdLength, cancellationToken, out normalized))
            return false;
        var separator = FindSeparator(normalized, cancellationToken, out var lastSeparator);
        if (separator <= 0
            || separator != lastSeparator
            || separator == normalized.Length - 1)
        {
            return false;
        }

        var domain = CancellationAwareString.Slice(normalized, 0, separator, cancellationToken);
        var objectId = CancellationAwareString.Slice(normalized, separator + 1, normalized.Length - separator - 1, cancellationToken);
        return IsValidSegment(
                domain,
                mustStartWithLetter: true,
                disallowBoundaryUnderscore: true,
                disallowDoubleUnderscore: true,
                cancellationToken)
            && IsValidSegment(
                objectId,
                mustStartWithLetter: false,
                disallowBoundaryUnderscore: true,
                disallowDoubleUnderscore: false,
                cancellationToken);
    }

    private static bool TryNormalizeBounded(
        string? value,
        int maximumLength,
        CancellationToken cancellationToken,
        out string normalized)
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

        normalized = CancellationAwareString.Slice(value, start, length, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    private static bool IsValidSegment(
        string value,
        bool mustStartWithLetter,
        bool disallowBoundaryUnderscore,
        bool disallowDoubleUnderscore,
        CancellationToken cancellationToken)
    {
        if (value.Length == 0
            || (mustStartWithLetter && (value[0] < 'a' || value[0] > 'z'))
            || (disallowBoundaryUnderscore
                && (value[0] == '_' || value[value.Length - 1] == '_'))
            || (disallowDoubleUnderscore && ContainsDoubleUnderscore(value, cancellationToken)))
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            var character = value[index];

            if (!((character >= 'a' && character <= 'z')
                  || (character >= '0' && character <= '9')
                  || character == '_'))
            {
                return false;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    private static int FindSeparator(string value, CancellationToken cancellationToken, out int lastSeparator)
    {
        var first = -1;
        lastSeparator = -1;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (value[index] != '.') continue;
            if (first < 0) first = index;
            lastSeparator = index;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return first;
    }

    private static bool ContainsUppercase(string value, CancellationToken cancellationToken)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (value[index] is >= 'A' and <= 'Z') return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private static bool ContainsDoubleUnderscore(string value, CancellationToken cancellationToken)
    {
        for (var index = 1; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (value[index - 1] == '_' && value[index] == '_') return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    internal static HomeAssistantState RequireResponseDomain(
        HomeAssistantState state,
        string domain,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state is null)
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an entity outside the requested " + domain + " domain.");
        }

        var entityId = RequireResponseEntityId(state.EntityId, cancellationToken);
        var separator = FindSeparator(entityId, cancellationToken, out _);
        if (!CancellationAwareString.EqualsOrdinal(
                CancellationAwareString.Slice(entityId, 0, separator, cancellationToken),
                domain,
                cancellationToken))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an entity outside the requested " + domain + " domain.");
        }

        return state;
    }

    internal static HomeAssistantState RequireResponseEntity(
        HomeAssistantState state,
        string expectedEntityId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state is null)
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a different entity than requested.");
        }

        var actualEntityId = RequireResponseEntityId(state.EntityId, cancellationToken);
        if (!string.Equals(actualEntityId, expectedEntityId, StringComparison.Ordinal))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a different entity than requested.");
        }

        return state;
    }

    internal static IEnumerable<HomeAssistantState> RequireResponseDomainStates(
        IEnumerable<HomeAssistantState> states,
        string domain,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var entityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (state is null)
            {
                throw new HomeAssistantProtocolException(
                    "Home Assistant returned a null entity state.");
            }

            var entityId = RequireResponseEntityId(state.EntityId, cancellationToken);
            var separator = entityId.IndexOf('.');
            if (string.Equals(entityId.Substring(0, separator), domain, StringComparison.Ordinal))
            {
                if (!entityIds.Add(entityId))
                {
                    throw new HomeAssistantProtocolException(
                        "Home Assistant returned duplicate " + domain + " entity states.");
                }
                yield return state;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }
}
