using System.Management.Automation;
using HomeAssistantX.Dashboards;

namespace HomeAssistantX.PowerShell;

/// <summary>Removes a Lovelace dashboard, its configuration, or a storage-mode resource.</summary>
/// <example><summary>Preview removing a dashboard configuration</summary><code>Remove-HomeAssistantDashboard -Configuration -UrlPath house-main -WhatIf</code></example>
[Cmdlet(VerbsCommon.Remove, "HomeAssistantDashboard", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High, DefaultParameterSetName = DashboardSet)]
public sealed class RemoveHomeAssistantDashboardCommand : HomeAssistantCmdlet
{
    private const string DashboardSet = "Dashboard";
    private const string ConfigurationSet = "Configuration";
    private const string ResourceSet = "Resource";
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = DashboardSet)][ValidateNotNullOrEmpty] public string? DashboardId { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ConfigurationSet)][ValidateSwitchPresent] public SwitchParameter Configuration { get; set; }
    [Parameter(ParameterSetName = ConfigurationSet)][ValidateNotNullOrEmpty] public string? UrlPath { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ResourceSet)][ValidateNotNullOrEmpty] public string? ResourceId { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        switch (ParameterSetName)
        {
            case ConfigurationSet:
                string? urlPath = null;
                if (UrlPath is not null
                    && !HomeAssistantDashboardIdentifier.TryNormalizeUrlPath(UrlPath, true, out urlPath, CancelToken))
                    throw new ArgumentException("Dashboard configuration URL paths must be canonical lowercase slugs containing only letters, numbers, and single hyphens.", nameof(UrlPath));
                if (ShouldProcess(urlPath ?? "default", "Delete Home Assistant dashboard configuration")) await Client.Dashboards.DeleteConfigurationAsync(urlPath, CancelToken).ConfigureAwait(false);
                break;
            case ResourceSet:
                var resourceId = Require(ResourceId, nameof(ResourceId));
                if (ShouldProcess(resourceId, "Delete Home Assistant dashboard resource")) await Client.Dashboards.DeleteResourceAsync(resourceId, CancelToken).ConfigureAwait(false);
                break;
            default:
                var dashboardId = Require(DashboardId, nameof(DashboardId));
                if (ShouldProcess(dashboardId, "Delete Home Assistant dashboard")) await Client.Dashboards.DeleteDashboardAsync(dashboardId, CancelToken).ConfigureAwait(false);
                break;
        }
    }

    private static string Require(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
        return value!.Trim();
    }
}
