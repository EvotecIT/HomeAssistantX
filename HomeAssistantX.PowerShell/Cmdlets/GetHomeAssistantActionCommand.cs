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
        if (!string.IsNullOrWhiteSpace(Entity))
        {
            var entity = Client.Inventory.ResolveEntity(snapshot, Entity!, CancelToken);
            if (!string.IsNullOrWhiteSpace(domain) && !string.Equals(domain, entity.Domain, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The supplied domain does not match the selected entity.", nameof(Domain));
            domain = entity.Domain;
        }

        IEnumerable<HomeAssistantActionDefinition> actions = snapshot.Actions;
        if (!string.IsNullOrWhiteSpace(domain)) actions = actions.Where(x => { CancelToken.ThrowIfCancellationRequested(); return string.Equals(x.Domain, domain, StringComparison.OrdinalIgnoreCase); });
        if (!string.IsNullOrWhiteSpace(Action)) actions = actions.Where(x => { CancelToken.ThrowIfCancellationRequested(); return string.Equals(x.Action, Action, StringComparison.OrdinalIgnoreCase); });
        var result = actions.ToArray();
        CancelToken.ThrowIfCancellationRequested();
        WriteObject(result, true);
    }
}
