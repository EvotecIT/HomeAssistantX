using HomeAssistantX.Exceptions;

namespace HomeAssistantX.Models;

/// <summary>Validates and normalizes native Home Assistant entity identifiers.</summary>
public static class HomeAssistantEntityId
{
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

        var separator = normalized.IndexOf('.');
        return string.Equals(normalized.Substring(0, separator), normalizedDomain, StringComparison.Ordinal);
    }

    /// <summary>Trims and validates a lowercase Home Assistant domain.</summary>
    public static bool TryNormalizeDomain(string? value, out string normalized)
        => TryNormalizeDomain(value, default, out normalized);

    internal static bool TryNormalizeDomain(
        string? value,
        CancellationToken cancellationToken,
        out string normalized)
    {
        cancellationToken.ThrowIfCancellationRequested();
        normalized = value?.Trim() ?? string.Empty;
        return IsValidSegment(
            normalized,
            mustStartWithLetter: true,
            disallowBoundaryUnderscore: true,
            disallowDoubleUnderscore: true,
            cancellationToken);
    }

    internal static string RequireResponseEntityId(string? value)
    {
        if (!TryNormalize(value, out var normalized)
            || !string.Equals(value, normalized, StringComparison.Ordinal)
            || normalized.Any(character => character >= 'A' && character <= 'Z'))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a malformed entity identifier.");
        }

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
        cancellationToken.ThrowIfCancellationRequested();
        normalized = value?.Trim() ?? string.Empty;
        var separator = normalized.IndexOf('.');
        if (separator <= 0
            || separator != normalized.LastIndexOf('.')
            || separator == normalized.Length - 1)
        {
            return false;
        }

        var domain = normalized.Substring(0, separator);
        var objectId = normalized.Substring(separator + 1);
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
            || (disallowDoubleUnderscore && value.Contains("__")))
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

    internal static HomeAssistantState RequireResponseDomain(HomeAssistantState state, string domain)
    {
        if (state is null)
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an entity outside the requested " + domain + " domain.");
        }

        var entityId = RequireResponseEntityId(state.EntityId);
        var separator = entityId.IndexOf('.');
        if (!string.Equals(entityId.Substring(0, separator), domain, StringComparison.Ordinal))
        {
            throw new HomeAssistantProtocolException("Home Assistant returned an entity outside the requested " + domain + " domain.");
        }

        return state;
    }

    internal static HomeAssistantState RequireResponseEntity(HomeAssistantState state, string expectedEntityId)
    {
        if (state is null)
        {
            throw new HomeAssistantProtocolException("Home Assistant returned a different entity than requested.");
        }

        var actualEntityId = RequireResponseEntityId(state.EntityId);
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

            var entityId = RequireResponseEntityId(state.EntityId);
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
