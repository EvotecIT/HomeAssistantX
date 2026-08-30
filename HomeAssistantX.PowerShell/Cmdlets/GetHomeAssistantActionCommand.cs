using System.Management.Automation;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Lists Home Assistant actions and their runtime-provided field descriptions.</summary>
/// <example>
///   <summary>Inspect actions for a discovered entity</summary>
///   <code>Get-HomeAssistantAction -Entity 'Kitchen light'</code>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantAction")]
[OutputType(typeof(HomeAssistantActionDefinition))]
public sealed class GetHomeAssistantActionCommand : HomeAssistantCmdlet
{
    /// <summary>Action domain, such as <c>light</c> or <c>climate</c>.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Domain { get; set; }

    /// <summary>Action name within the selected domain. <c>Service</c> is an alias.</summary>
    [Parameter]
    [Alias("Service")]
    [ValidateNotNullOrEmpty]
    public string? Action { get; set; }

    /// <summary>Entity friendly name or native ID whose domain should be inspected.</summary>
    [Parameter]
    [Alias("EntityId")]
    [ValidateNotNullOrEmpty]
    public string? Entity { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var snapshot = await Client.Inventory.GetSnapshotAsync(CancelToken).ConfigureAwait(false);
        var domain = Domain;
        if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(Entity, CancelToken))
        {
            var entity = Client.Inventory.ResolveEntity(snapshot, Entity!, CancelToken);
            if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(domain, CancelToken)
                && !HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(domain, entity.Domain, CancelToken)) throw new ArgumentException("The supplied domain does not match the selected entity.", nameof(Domain));
            domain = entity.Domain;
        }

        IEnumerable<HomeAssistantActionDefinition> actions = snapshot.Actions;
        if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(domain, CancelToken)) actions = actions.Where(x => { CancelToken.ThrowIfCancellationRequested(); return HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(x.Domain, domain, CancelToken); });
        if (!HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(Action, CancelToken)) actions = actions.Where(x => { CancelToken.ThrowIfCancellationRequested(); return HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(x.Action, Action, CancelToken); });
        var result = actions.ToArray();
        CancelToken.ThrowIfCancellationRequested();
        WriteObject(result, true);
    }
}
