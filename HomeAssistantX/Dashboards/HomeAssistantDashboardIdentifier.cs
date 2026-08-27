namespace HomeAssistantX.Dashboards;

/// <summary>Validates native Home Assistant dashboard values.</summary>
public static class HomeAssistantDashboardIdentifier
{
    /// <summary>Trims and validates Home Assistant's prefix:name icon form.</summary>
    public static bool TryNormalizeIcon(string? value, out string normalized)
    {
        normalized = value?.Trim() ?? string.Empty;
        return normalized.Length > 0 && normalized.IndexOf(':') >= 0;
    }
}
