using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Dashboards;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.PowerShell;

/// <summary>Creates or updates Lovelace dashboards, configurations, and storage-mode resources.</summary>
/// <example><summary>Replace the default dashboard configuration</summary><code>Set-HomeAssistantDashboard -ConfigurationJson '{"views":[]}' -WhatIf</code></example>
/// <example><summary>Create a dashboard</summary><code>Set-HomeAssistantDashboard -New -UrlPath 'house-main' -Title 'House' -WhatIf</code></example>
/// <example><summary>Add a dashboard resource</summary><code>Set-HomeAssistantDashboard -NewResource -ResourceUrl '/local/card.js' -ResourceType Module -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantDashboard", SupportsShouldProcess = true, DefaultParameterSetName = ConfigurationSet)]
[OutputType(typeof(HomeAssistantDashboard))]
[OutputType(typeof(HomeAssistantDashboardResource))]
[OutputType(typeof(JsonElement))]
public sealed class SetHomeAssistantDashboardCommand : HomeAssistantCmdlet
{
    private const string ConfigurationSet = "Configuration";
    private const string CreateSet = "Create";
    private const string UpdateSet = "Update";
    private const string ResourceCreateSet = "ResourceCreate";
    private const string ResourceUpdateSet = "ResourceUpdate";

    [Parameter(Mandatory = true, ParameterSetName = ConfigurationSet)][ValidateNotNullOrEmpty] public string? ConfigurationJson { get; set; }
    /// <summary>Dashboard URL path. Required when creating a dashboard; optional when targeting a stored configuration.</summary>
    [Parameter(ParameterSetName = ConfigurationSet)]
    [Parameter(Mandatory = true, ParameterSetName = CreateSet)]
    [ValidateNotNullOrEmpty] public string? UrlPath { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = CreateSet)][ValidateSwitchPresent] public SwitchParameter New { get; set; }
    /// <summary>Dashboard title. Required when creating a dashboard; optional when updating one.</summary>
    [Parameter(Mandatory = true, ParameterSetName = CreateSet)]
    [Parameter(ParameterSetName = UpdateSet)]
    [ValidateNotNullOrEmpty] public string? Title { get; set; }
    [Parameter(ParameterSetName = CreateSet)]
    [Parameter(ParameterSetName = UpdateSet)] public string? Icon { get; set; }
    [Parameter(ParameterSetName = CreateSet)] public SwitchParameter HideFromSidebar { get; set; }
    [Parameter(ParameterSetName = CreateSet)] public SwitchParameter RequireAdmin { get; set; }
    [Parameter(ParameterSetName = CreateSet)] public SwitchParameter AllowSingleWord { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = UpdateSet)][ValidateNotNullOrEmpty] public string? DashboardId { get; set; }
    [Parameter(ParameterSetName = UpdateSet)] public SwitchParameter RemoveIcon { get; set; }
    [Parameter(ParameterSetName = UpdateSet)] public bool? ShowInSidebar { get; set; }
    [Parameter(ParameterSetName = UpdateSet)] public bool? DashboardRequireAdmin { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ResourceCreateSet)][ValidateSwitchPresent] public SwitchParameter NewResource { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ResourceUpdateSet)][ValidateNotNullOrEmpty] public string? ResourceId { get; set; }
    /// <summary>Dashboard resource URL. Required when creating a resource; optional when updating one.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ResourceCreateSet)]
    [Parameter(ParameterSetName = ResourceUpdateSet)]
    [ValidateNotNullOrEmpty] public string? ResourceUrl { get; set; }
    /// <summary>Dashboard resource type. Required when creating a resource; optional when updating one.</summary>
    [Parameter(Mandatory = true, ParameterSetName = ResourceCreateSet)]
    [Parameter(ParameterSetName = ResourceUpdateSet)] public HomeAssistantDashboardResourceType? ResourceType { get; set; }
    [Parameter] public SwitchParameter PassThru { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        object? result;
        string target;
        string action;
        switch (ParameterSetName)
        {
            case CreateSet:
                if (!HomeAssistantDashboardIdentifier.TryNormalizeUrlPath(UrlPath, AllowSingleWord, out var createUrlPath, CancelToken))
                    throw new ArgumentException("Dashboard URL paths must be canonical lowercase slugs containing only letters, numbers, and single hyphens; a hyphen is required unless AllowSingleWord is enabled.", nameof(UrlPath));
                var createTitle = Require(Title, nameof(Title));
                var createIcon = Icon is null ? null : RequireIcon(Icon, nameof(Icon));
                target = createUrlPath; action = "Create Home Assistant dashboard";
                if (!ShouldProcess(target, action)) return;
                result = await Client.Dashboards.CreateDashboardAsync(new HomeAssistantDashboardCreate { UrlPath = createUrlPath, Title = createTitle, Icon = createIcon, ShowInSidebar = !HideFromSidebar, RequireAdmin = RequireAdmin, AllowSingleWord = AllowSingleWord }, CancelToken).ConfigureAwait(false);
                break;
            case UpdateSet:
                if (RemoveIcon && Icon is not null) throw new ArgumentException("Icon and RemoveIcon cannot be combined.");
                var dashboardId = Require(DashboardId, nameof(DashboardId));
                var updateTitle = Title is null ? null : Require(Title, nameof(Title));
                var updateIcon = Icon is null ? null : RequireIcon(Icon, nameof(Icon));
                var update = new HomeAssistantDashboardUpdate { Title = updateTitle, Icon = updateIcon, RemoveIcon = RemoveIcon, ShowInSidebar = ShowInSidebar, RequireAdmin = DashboardRequireAdmin };
                if (Title is null && Icon is null && !RemoveIcon && !ShowInSidebar.HasValue && !DashboardRequireAdmin.HasValue) throw new ArgumentException("Specify at least one dashboard update.");
                target = dashboardId; action = "Update Home Assistant dashboard";
                if (!ShouldProcess(target, action)) return;
                result = await Client.Dashboards.UpdateDashboardAsync(dashboardId, update, CancelToken).ConfigureAwait(false);
                break;
            case ResourceCreateSet:
                var createResourceUrl = Require(ResourceUrl, nameof(ResourceUrl));
                ValidateResourceType(ResourceType, required: true);
                target = createResourceUrl; action = "Create Home Assistant dashboard resource";
                if (!ShouldProcess(target, action)) return;
                result = await Client.Dashboards.CreateResourceAsync(createResourceUrl, ResourceType!.Value, CancelToken).ConfigureAwait(false);
                break;
            case ResourceUpdateSet:
                if (ResourceUrl is null && !ResourceType.HasValue) throw new ArgumentException("Specify ResourceUrl or ResourceType.");
                var resourceId = Require(ResourceId, nameof(ResourceId));
                var updateResourceUrl = ResourceUrl is null ? null : Require(ResourceUrl, nameof(ResourceUrl));
                ValidateResourceType(ResourceType, required: false);
                target = resourceId; action = "Update Home Assistant dashboard resource";
                if (!ShouldProcess(target, action)) return;
                result = await Client.Dashboards.UpdateResourceAsync(resourceId, updateResourceUrl, ResourceType, CancelToken).ConfigureAwait(false);
                break;
            default:
                CancelToken.ThrowIfCancellationRequested();
                using (var document = await HomeAssistantJson.ParseDocumentAsync(ConfigurationJson!, CancelToken).ConfigureAwait(false))
                {
                    var configuration = document.RootElement;
                    HomeAssistantDashboardIdentifier.ValidateConfigurationForSave(
                        configuration,
                        nameof(ConfigurationJson),
                        CancelToken);
                    string? configurationUrlPath = null;
                    if (UrlPath is not null
                        && !HomeAssistantDashboardIdentifier.TryNormalizeUrlPath(UrlPath, true, out configurationUrlPath, CancelToken))
                        throw new ArgumentException("Dashboard configuration URL paths must be canonical lowercase slugs containing only letters, numbers, and single hyphens.", nameof(UrlPath));
                    target = configurationUrlPath ?? "default"; action = "Replace Home Assistant dashboard configuration";
                    if (!ShouldProcess(target, action)) return;
                    result = await Client.Dashboards.SaveConfigurationAsync(configuration, configurationUrlPath, CancelToken).ConfigureAwait(false);
                }
                break;
        }
        if (PassThru) WriteObject(result);
    }

    private static void ValidateResourceType(HomeAssistantDashboardResourceType? value, bool required)
    {
        if (required && !value.HasValue) throw new ArgumentException("A dashboard resource type is required.", nameof(ResourceType));
        if (value.HasValue && !Enum.IsDefined(typeof(HomeAssistantDashboardResourceType), value.Value)) throw new ArgumentOutOfRangeException(nameof(ResourceType));
    }

    private static string Require(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
        return value!.Trim();
    }

    private static string RequireIcon(string value, string parameterName)
    {
        if (!HomeAssistantDashboardIdentifier.TryNormalizeIcon(value, out var normalized))
            throw new ArgumentException("A dashboard icon must contain a ':' separator.", parameterName);
        return normalized;
    }
}
