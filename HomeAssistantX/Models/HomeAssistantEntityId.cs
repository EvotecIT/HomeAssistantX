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
    {
        if (!TryNormalize(value, out normalized) || !TryNormalizeDomain(domain, out var normalizedDomain))
        {
            return false;
        }

        var separator = normalized.IndexOf('.');
        return string.Equals(normalized.Substring(0, separator), normalizedDomain, StringComparison.Ordinal);
    }

    /// <summary>Trims and validates a lowercase Home Assistant domain.</summary>
    public static bool TryNormalizeDomain(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return IsValidSegment(normalized, disallowDoubleUnderscore: true);
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
    {
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
        return IsValidSegment(domain, disallowDoubleUnderscore: true)
            && IsValidSegment(objectId, disallowDoubleUnderscore: false);
    }

    private static bool IsValidSegment(string value, bool disallowDoubleUnderscore)
    {
        if (value.Length == 0
            || value[0] == '_'
            || value[value.Length - 1] == '_'
            || (disallowDoubleUnderscore && value.Contains("__")))
        {
            return false;
        }

        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];

            if (!((character >= 'a' && character <= 'z')
                  || (character >= '0' && character <= '9')
                  || character == '_'))
            {
                return false;
            }
        }

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
        string domain)
    {
        var entityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var state in states)
        {
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
    }
}
