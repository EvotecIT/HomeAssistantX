using System.Management.Automation;
using HomeAssistantX.Registries;

namespace HomeAssistantX.PowerShell;

/// <summary>Gets Home Assistant configuration entries by identifier, domain, or all integrations.</summary>
/// <example>
///   <summary>List MQTT configuration entries</summary>
///   <code>$ha | Get-HomeAssistantIntegration -Domain mqtt</code>
///   <para>Returns configuration entries belonging to one integration domain.</para>
/// </example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantIntegration", DefaultParameterSetName = AllParameterSet)]
[OutputType(typeof(HomeAssistantConfigEntry))]
public sealed class GetHomeAssistantIntegrationCommand : HomeAssistantCmdlet
{
    private const string AllParameterSet = "All";
    private const string IdParameterSet = "Id";
    private const string DomainParameterSet = "Domain";

    /// <summary>Returns all configuration entries. This is the default behavior.</summary>
    [Parameter(ParameterSetName = AllParameterSet)]
    public SwitchParameter All { get; set; }

    /// <summary>Exact configuration-entry identifier.</summary>
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = IdParameterSet)]
    [ValidateNotNullOrEmpty]
    public string EntryId { get; set; } = string.Empty;

    /// <summary>Integration domain, such as <c>hue</c> or <c>mqtt</c>.</summary>
    [Parameter(Mandatory = true, ParameterSetName = DomainParameterSet)]
    [ValidateNotNullOrEmpty]
    public string Domain { get; set; } = string.Empty;

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == IdParameterSet)
        {
            WriteObject(await Client.Operations.Integrations.GetAsync(EntryId, CancelToken).ConfigureAwait(false));
            return;
        }

        WriteObject(
            await Client.Operations.Integrations.GetAllAsync(
                ParameterSetName == DomainParameterSet ? Domain : null,
                CancelToken).ConfigureAwait(false),
            enumerateCollection: true);
    }
}
