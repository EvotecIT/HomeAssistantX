using System.Text.Json;

namespace HomeAssistantX.Services;

/// <summary>Describes one action exposed by Home Assistant or an installed integration.</summary>
public sealed class HomeAssistantActionDefinition
{
    public string Domain { get; internal set; } = string.Empty;

    public string Action { get; internal set; } = string.Empty;

    public string Name { get; internal set; } = string.Empty;

    public string? Description { get; internal set; }

    public IReadOnlyList<HomeAssistantActionFieldDefinition> Fields { get; internal set; } = Array.Empty<HomeAssistantActionFieldDefinition>();

    public JsonElement? Target { get; internal set; }

    public JsonElement? Response { get; internal set; }

    public JsonElement Raw { get; internal set; }

    public override string ToString()
    {
        return Domain + "." + Action;
    }
}

/// <summary>Describes one input accepted by a Home Assistant action.</summary>
public sealed class HomeAssistantActionFieldDefinition
{
    public string Field { get; internal set; } = string.Empty;

    public string Name { get; internal set; } = string.Empty;

    public string? Description { get; internal set; }

    public bool Required { get; internal set; }

    public bool Advanced { get; internal set; }

    public JsonElement? Default { get; internal set; }

    public JsonElement? Example { get; internal set; }

    public JsonElement? Selector { get; internal set; }

    public JsonElement Raw { get; internal set; }
}
