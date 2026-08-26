namespace HomeAssistantX.Inventory;

/// <summary>Filters the joined entity inventory without losing registry or live-state context.</summary>
public sealed class HomeAssistantEntityQuery
{
    public IReadOnlyList<string>? Entity { get; set; }

    public string? Name { get; set; }

    public string? Domain { get; set; }

    public string? Device { get; set; }

    public string? Area { get; set; }

    public string? Floor { get; set; }

    public bool AvailableOnly { get; set; }

    public bool IncludeDisabled { get; set; }

    public bool IncludeHidden { get; set; }
}
