using HomeAssistantX.Exceptions;

namespace HomeAssistantX.Models;

internal static class HomeAssistantEntityId
{
    internal static bool TryNormalizeForDomain(
        string? value,
        string domain,
        out string normalized)
    {
        if (!TryNormalize(value, out normalized) || string.IsNullOrWhiteSpace(domain))
        {
            return false;
        }

        var separator = normalized.IndexOf('.');
        return string.Equals(normalized.Substring(0, separator), domain, StringComparison.OrdinalIgnoreCase);
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

    private static bool TryNormalize(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        var separator = normalized.IndexOf('.');
        if (separator <= 0
            || separator != normalized.LastIndexOf('.')
            || separator == normalized.Length - 1)
        {
            return false;
        }

        for (var index = 0; index < normalized.Length; index++)
        {
            var character = normalized[index];
            if (index == separator)
            {
                continue;
            }

            if (!((character >= 'a' && character <= 'z')
                  || (character >= 'A' && character <= 'Z')
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
}
