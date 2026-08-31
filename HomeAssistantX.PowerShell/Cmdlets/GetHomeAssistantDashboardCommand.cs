using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Dashboards;

namespace HomeAssistantX.PowerShell;

/// <summary>Reads Home Assistant frontend panels, Lovelace dashboards, configurations, resources, or mode information.</summary>
/// <example><summary>List dashboards</summary><code>Get-HomeAssistantDashboard</code></example>
/// <example><summary>Read one dashboard configuration</summary><code>Get-HomeAssistantDashboard -Configuration -UrlPath 'house-main'</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantDashboard", DefaultParameterSetName = DashboardsSet)]
[OutputType(typeof(HomeAssistantDashboard))]
[OutputType(typeof(HomeAssistantPanel))]
[OutputType(typeof(HomeAssistantLovelaceInfo))]
[OutputType(typeof(HomeAssistantDashboardResource))]
[OutputType(typeof(JsonElement))]
public sealed class GetHomeAssistantDashboardCommand : HomeAssistantCmdlet
{
    private const string DashboardsSet = "Dashboards";
    private const string PanelsSet = "Panels";
    private const string InfoSet = "Info";
    private const string ConfigurationSet = "Configuration";
    private const string ResourcesSet = "Resources";
    [Parameter(Mandatory = true, ParameterSetName = PanelsSet)][ValidateSwitchPresent] public SwitchParameter Panels { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = InfoSet)][ValidateSwitchPresent] public SwitchParameter Info { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ConfigurationSet)][ValidateSwitchPresent] public SwitchParameter Configuration { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ResourcesSet)][ValidateSwitchPresent] public SwitchParameter Resources { get; set; }
    [Parameter(ParameterSetName = ConfigurationSet)][ValidateNotNullOrEmpty] public string? UrlPath { get; set; }
    [Parameter(ParameterSetName = ConfigurationSet)] public SwitchParameter ForceReload { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        switch (ParameterSetName)
        {
            case PanelsSet: WriteObject(await Client.Dashboards.GetPanelsAsync(CancelToken).ConfigureAwait(false), true); break;
            case InfoSet: WriteObject(await Client.Dashboards.GetInfoAsync(CancelToken).ConfigureAwait(false)); break;
            case ConfigurationSet: WriteObject(await Client.Dashboards.GetConfigurationAsync(UrlPath, ForceReload, CancelToken).ConfigureAwait(false)); break;
            case ResourcesSet: WriteObject(await Client.Dashboards.GetResourcesAsync(CancelToken).ConfigureAwait(false), true); break;
            default: WriteObject(await Client.Dashboards.GetDashboardsAsync(CancelToken).ConfigureAwait(false), true); break;
        }
    }
}
