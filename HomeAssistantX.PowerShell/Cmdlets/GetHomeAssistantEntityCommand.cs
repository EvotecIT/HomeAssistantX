using System.Management.Automation;
using HomeAssistantX.Models;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets current entity states by identifier, domain, or all entities.</summary>
/// <example>
///   <summary>Get all light states</summary>
///   <code>$ha | Get-HomeAssistantEntity -Domain light</code>
///   <para>Filters the current state snapshot to the light domain.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantEntity", DefaultParameterSetName = AllParameterSet)]
[OutputType(typeof(HomeAssistantState))]
public sealed class GetHomeAssistantEntityCommand : HomeAssistantCmdlet
{
    private const string AllParameterSet = "All";
    private const string EntityParameterSet = "Entity";
    private const string DomainParameterSet = "Domain";

    /// <summary>Returns all current entity states. This is the default behavior.</summary>
    [Parameter(ParameterSetName = AllParameterSet)]
    public SwitchParameter All { get; set; }

    /// <summary>One or more exact entity identifiers.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = EntityParameterSet)]
    [ValidateNotNullOrEmpty]
    public string[] EntityId { get; set; } = Array.Empty<string>();

    /// <summary>Entity domain to filter, such as <c>light</c> or <c>sensor</c>.</summary>
    [Parameter(Mandatory = true, ParameterSetName = DomainParameterSet)]
    [ValidateNotNullOrEmpty]
    public string Domain { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == EntityParameterSet)
        {
            foreach (var entityId in EntityId)
            {
                WriteObject(await Client.States.GetAsync(entityId, CancelToken).ConfigureAwait(false));
            }

            return;
        }

        var states = await Client.States.GetAllAsync(CancelToken).ConfigureAwait(false);
        if (ParameterSetName == DomainParameterSet)
        {
            states = states.Where(state => string.Equals(state.Domain, Domain, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        WriteObject(states, enumerateCollection: true);
    }
}
