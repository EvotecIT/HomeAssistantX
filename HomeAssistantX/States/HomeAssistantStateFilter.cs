using HomeAssistantX.Models;

namespace HomeAssistantX.States;

/// <summary>Restricts live state delivery without changing the state retained by the monitor.</summary>
public sealed class HomeAssistantStateFilter
{
    private readonly HashSet<string>? _entityIds;
    private readonly HashSet<string>? _domains;

    private HomeAssistantStateFilter(IEnumerable<string>? entityIds, IEnumerable<string>? domains)
    {
        _entityIds = entityIds is null
            ? null
            : new HashSet<string>(entityIds, StringComparer.OrdinalIgnoreCase);
        _domains = domains is null
            ? null
            : new HashSet<string>(domains, StringComparer.OrdinalIgnoreCase);
    }

    public static HomeAssistantStateFilter All { get; } = new(null, null);

    public static HomeAssistantStateFilter ForEntities(params string[] entityIds)
    {
        return new HomeAssistantStateFilter(Validate(entityIds, nameof(entityIds)), null);
    }

    public static HomeAssistantStateFilter ForDomains(params string[] domains)
    {
        return new HomeAssistantStateFilter(null, Validate(domains, nameof(domains)));
    }

    internal bool Matches(HomeAssistantStateChange change)
    {
        if (_entityIds is not null && !_entityIds.Contains(change.EntityId))
        {
            return false;
        }

        if (_domains is null)
        {
            return true;
        }

        var state = change.CurrentState ?? change.PreviousState;
        return state is not null && _domains.Contains(state.Domain);
    }

    private static IEnumerable<string> Validate(string[] values, string parameterName)
    {
        if (values is null || values.Length == 0 || values.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty value is required.", parameterName);
        }

        return values.Select(value => value.Trim()).ToArray();
    }
}
