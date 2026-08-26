using System.Management.Automation;
using HomeAssistantX.Inventory;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets joined entities by name, identifier, domain, device, area, or floor.</summary>
/// <example>
///   <summary>List the lights in the Kitchen</summary>
///   <code>Get-HomeAssistantEntity -Area Kitchen -Domain light</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantEntity")]
[OutputType(typeof(HomeAssistantEntityInfo))]
public sealed class GetHomeAssistantEntityCommand : HomeAssistantCmdlet
{
    /// <summary>One or more exact entity friendly names or native IDs.</summary>
    [Parameter(Position = 0)]
    [Alias("EntityId")]
    [ValidateNotNullOrEmpty]
    public string[]? Entity { get; set; }

    /// <summary>Text contained in the entity friendly name.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Name { get; set; }

    /// <summary>Entity domain, such as <c>light</c> or <c>sensor</c>.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Domain { get; set; }

    /// <summary>Device friendly name or native ID.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Device { get; set; }

    /// <summary>Area name, alias, or native ID. <c>Room</c> is an alias.</summary>
    [Parameter]
    [Alias("Room")]
    [ValidateNotNullOrEmpty]
    public string? Area { get; set; }

    /// <summary>Floor name, alias, or native ID.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Floor { get; set; }

    /// <summary>Returns only entities that currently have a state other than <c>unavailable</c>.</summary>
    [Parameter]
    public SwitchParameter AvailableOnly { get; set; }

    /// <summary>Includes registry entries disabled by Home Assistant, an integration, or a user.</summary>
    [Parameter]
    public SwitchParameter IncludeDisabled { get; set; }

    /// <summary>Includes registry entries hidden by Home Assistant, an integration, or a user.</summary>
    [Parameter]
    public SwitchParameter IncludeHidden { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var connection = ActiveConnection;
        var entities = await connection.Client.Inventory.GetEntitiesAsync(new HomeAssistantEntityQuery
        {
            Entity = Entity,
            Name = Name,
            Domain = Domain,
            Device = Device,
            Area = Area,
            Floor = Floor,
            AvailableOnly = AvailableOnly,
            IncludeDisabled = IncludeDisabled,
            IncludeHidden = IncludeHidden
        }, CancelToken).ConfigureAwait(false);

        foreach (var entity in entities)
        {
            HomeAssistantEntityProvenance.Set(entity, connection);
        }

        WriteObject(entities, true);
    }
}
