using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Dashboards;

/// <summary>Reads frontend panels and manages Lovelace dashboards, configurations, and resources.</summary>
public sealed class HomeAssistantDashboardClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantDashboardClient(HomeAssistantWebSocketClient webSocket) => _webSocket = webSocket;

    public async Task<IReadOnlyList<HomeAssistantPanel>> GetPanelsAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("get_panels", null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object) throw new HomeAssistantProtocolException("The frontend panel response was not an object.");
        RequireNoDuplicateProperties(
            value,
            "The frontend panel response contained duplicate JSON properties.",
            cancellationToken);
        var result = new List<HomeAssistantPanel>();
        var routes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var route = RequireResponseSelector(property.Name, "A frontend panel contained an invalid route.");
            if (!routes.Add(route))
                throw new HomeAssistantProtocolException("The frontend panel response contained a duplicate route.");
            RequirePanelBooleans(property.Value);
            var panel = HomeAssistantJson.DeserializeResponse<HomeAssistantPanel>(
                property.Value,
                "A frontend panel could not be decoded.",
                cancellationToken: cancellationToken);
            if (!property.Value.TryGetProperty("url_path", out var embeddedRoute))
            {
                panel.UrlPath = route;
            }
            else
            {
                if (embeddedRoute.ValueKind != JsonValueKind.String)
                    throw new HomeAssistantProtocolException("A frontend panel contained an invalid route.");
                panel.UrlPath = RequireResponseSelector(
                    embeddedRoute.GetString(),
                    "A frontend panel contained an invalid route.");
                if (!string.Equals(panel.UrlPath, route, StringComparison.Ordinal))
                    throw new HomeAssistantProtocolException("A frontend panel route did not match its registered key.");
            }
            if (string.IsNullOrWhiteSpace(panel.UrlPath))
                throw new HomeAssistantProtocolException("A frontend panel did not contain its required fields.");
            panel.ComponentName = RequireResponseSelector(
                panel.ComponentName,
                "A frontend panel contained a noncanonical component name.");
            if (panel.Icon is not null
                && (!HomeAssistantDashboardIdentifier.TryNormalizeIcon(panel.Icon, out var normalizedIcon)
                    || !string.Equals(panel.Icon, normalizedIcon, StringComparison.Ordinal)))
                throw new HomeAssistantProtocolException("A frontend panel contained a noncanonical icon.");
            result.Add(panel);
        }
        return SortPanels(result, cancellationToken);
    }

    internal static IReadOnlyList<HomeAssistantPanel> SortPanels(
        List<HomeAssistantPanel> panels,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var comparer = new CancellationAwareStringComparer(StringComparer.OrdinalIgnoreCase, cancellationToken);
        CancellationAwareSort.Sort(panels, (left, right) => comparer.Compare(left.UrlPath, right.UrlPath));
        cancellationToken.ThrowIfCancellationRequested();
        return panels;
    }

    public async Task<HomeAssistantLovelaceInfo> GetInfoAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("lovelace/info", null, cancellationToken).ConfigureAwait(false);
        RequireNoDuplicateProperties(
            value,
            "The Lovelace information contained duplicate JSON properties.",
            cancellationToken);
        var info = HomeAssistantJson.DeserializeResponse<HomeAssistantLovelaceInfo>(
            value,
            "The Lovelace information could not be decoded.",
            cancellationToken: cancellationToken);
        if (info.ResourceMode != "storage" && info.ResourceMode != "yaml")
            throw new HomeAssistantProtocolException("The Lovelace information did not contain its resource mode.");
        return info;
    }

    public async Task<IReadOnlyList<HomeAssistantDashboard>> GetDashboardsAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("lovelace/dashboards/list", null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException("The dashboard list had an unexpected shape.");
        RequireNoDuplicateProperties(
            value,
            "The dashboard list contained duplicate JSON properties.",
            cancellationToken);
        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireDashboardVisibility(item, "A dashboard did not contain its required visibility fields.");
        }
        var dashboards = HomeAssistantJson.DeserializeResponse<HomeAssistantDashboard[]>(
            value,
            "The dashboard list could not be decoded.",
            cancellationToken: cancellationToken);
        var urlPaths = new HashSet<string>(StringComparer.Ordinal);
        var storageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var dashboard in dashboards)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateListedDashboard(dashboard, cancellationToken);
            if (!urlPaths.Add(dashboard.UrlPath)
                || (!string.IsNullOrEmpty(dashboard.Id) && !storageIds.Add(dashboard.Id)))
            {
                throw new HomeAssistantProtocolException("The dashboard list contained a duplicate route or storage identifier.");
            }
        }
        return dashboards;
    }

    public async Task<HomeAssistantDashboard> CreateDashboardAsync(HomeAssistantDashboardCreate create, CancellationToken cancellationToken = default)
    {
        if (create is null) throw new ArgumentNullException(nameof(create));
        if (!HomeAssistantDashboardIdentifier.TryNormalizeUrlPath(create.UrlPath, create.AllowSingleWord, out var urlPath, cancellationToken))
            throw new ArgumentException("Dashboard URL paths must be canonical lowercase slugs containing only letters, numbers, and single hyphens; a hyphen is required unless AllowSingleWord is enabled.", nameof(create));
        var title = Require(create.Title, nameof(create.Title), cancellationToken);
        var payload = new Dictionary<string, object?>
        {
            ["url_path"] = urlPath,
            ["title"] = title,
            ["show_in_sidebar"] = create.ShowInSidebar,
            ["require_admin"] = create.RequireAdmin
        };
        var icon = create.Icon is null ? null : RequireIcon(create.Icon, nameof(create.Icon));
        if (icon is not null) payload["icon"] = icon;
        if (create.AllowSingleWord) payload["allow_single_word"] = true;
        return await RequestDashboardAsync(
            "lovelace/dashboards/create",
            payload,
            cancellationToken,
            expectedUrlPath: urlPath,
            expectedTitle: title,
            validateIcon: icon is not null,
            expectedIcon: icon,
            expectedShowInSidebar: create.ShowInSidebar,
            expectedRequireAdmin: create.RequireAdmin).ConfigureAwait(false);
    }

    public Task<HomeAssistantDashboard> UpdateDashboardAsync(string dashboardId, HomeAssistantDashboardUpdate update, CancellationToken cancellationToken = default)
    {
        if (update is null) throw new ArgumentNullException(nameof(update));
        if (update.RemoveIcon && update.Icon is not null) throw new ArgumentException("Icon and RemoveIcon cannot be combined.", nameof(update));
        var normalizedDashboardId = Require(dashboardId, nameof(dashboardId), cancellationToken);
        var payload = new Dictionary<string, object?> { ["dashboard_id"] = normalizedDashboardId };
        var title = update.Title is null ? null : Require(update.Title, nameof(update.Title), cancellationToken);
        var icon = update.Icon is null ? null : RequireIcon(update.Icon, nameof(update.Icon));
        if (title is not null) payload["title"] = title;
        if (icon is not null) payload["icon"] = icon;
        if (update.RemoveIcon) payload["icon"] = null;
        if (update.ShowInSidebar.HasValue) payload["show_in_sidebar"] = update.ShowInSidebar.Value;
        if (update.RequireAdmin.HasValue) payload["require_admin"] = update.RequireAdmin.Value;
        if (payload.Count == 1) throw new ArgumentException("At least one dashboard update is required.", nameof(update));
        return RequestDashboardAsync(
            "lovelace/dashboards/update",
            payload,
            cancellationToken,
            expectedDashboardId: normalizedDashboardId,
            expectedTitle: title,
            validateIcon: update.RemoveIcon || icon is not null,
            expectedIcon: update.RemoveIcon ? null : icon,
            expectedShowInSidebar: update.ShowInSidebar,
            expectedRequireAdmin: update.RequireAdmin);
    }

    public Task<JsonElement> DeleteDashboardAsync(string dashboardId, CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            "lovelace/dashboards/delete",
            new Dictionary<string, object?> { ["dashboard_id"] = Require(dashboardId, nameof(dashboardId), cancellationToken) },
            "The dashboard deletion response contained duplicate JSON properties.",
            cancellationToken);

    public async Task<JsonElement> GetConfigurationAsync(string? urlPath = null, bool force = false, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>();
        if (force) payload["force"] = true;
        if (urlPath is not null) payload["url_path"] = RequireConfigurationUrlPath(urlPath, nameof(urlPath), cancellationToken);
        var value = await RequestJsonAsync(
            "lovelace/config",
            payload,
            "The Lovelace configuration contained duplicate JSON properties.",
            cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object)
            throw new HomeAssistantProtocolException("The Lovelace configuration was not an object.");
        return value;
    }

    public Task<JsonElement> SaveConfigurationAsync(JsonElement configuration, string? urlPath = null, CancellationToken cancellationToken = default)
    {
        HomeAssistantDashboardIdentifier.ValidateConfigurationForSave(
            configuration,
            nameof(configuration),
            cancellationToken);
        var payload = new Dictionary<string, object?>
        {
            ["config"] = HomeAssistantJson.FreezeValue(
                configuration,
                nameof(configuration),
                "Lovelace configuration",
                cancellationToken)
        };
        if (urlPath is not null) payload["url_path"] = RequireConfigurationUrlPath(urlPath, nameof(urlPath), cancellationToken);
        return RequestJsonAsync(
            "lovelace/config/save",
            payload,
            "The Lovelace configuration mutation response contained duplicate JSON properties.",
            cancellationToken);
    }

    public Task<JsonElement> DeleteConfigurationAsync(string? urlPath = null, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>();
        if (urlPath is not null) payload["url_path"] = RequireConfigurationUrlPath(urlPath, nameof(urlPath), cancellationToken);
        return RequestJsonAsync(
            "lovelace/config/delete",
            payload,
            "The Lovelace configuration deletion response contained duplicate JSON properties.",
            cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantDashboardResource>> GetResourcesAsync(CancellationToken cancellationToken = default)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var resourceMode = (await GetInfoAsync(cancellationToken).ConfigureAwait(false)).ResourceMode;
            var value = await _webSocket.RequestAsync("lovelace/resources/list", null, cancellationToken).ConfigureAwait(false);
            var confirmedMode = (await GetInfoAsync(cancellationToken).ConfigureAwait(false)).ResourceMode;
            if (!string.Equals(resourceMode, confirmedMode, StringComparison.Ordinal)) continue;

            return DecodeResources(value, resourceMode, cancellationToken);
        }

        throw new HomeAssistantConnectionException(
            "The Lovelace resource mode changed while resources were being read.",
            new InvalidOperationException("The resource mode did not remain stable across the resource-list request."));
    }

    private static IReadOnlyList<HomeAssistantDashboardResource> DecodeResources(
        JsonElement value,
        string resourceMode,
        CancellationToken cancellationToken)
    {
        RequireNoDuplicateProperties(
            value,
            "The Lovelace resource list contained duplicate JSON properties.",
            cancellationToken);
        var resources = HomeAssistantJson.DeserializeResponse<HomeAssistantDashboardResource[]>(
            value,
            "The Lovelace resource list could not be decoded.",
            cancellationToken: cancellationToken);
        var storageIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateListedResource(resource, resourceMode);
            if (resourceMode == "storage" && !storageIds.Add(resource.Id))
                throw new HomeAssistantProtocolException("The Lovelace resource list contained a duplicate storage identifier.");
        }
        return resources;
    }

    public Task<HomeAssistantDashboardResource> CreateResourceAsync(string url, HomeAssistantDashboardResourceType type, CancellationToken cancellationToken = default)
    {
        var normalizedUrl = Require(url, nameof(url), cancellationToken);
        var normalizedType = ResourceTypeName(type);
        return RequestResourceAsync(
            "lovelace/resources/create",
            new Dictionary<string, object?> { ["url"] = normalizedUrl, ["res_type"] = normalizedType },
            cancellationToken,
            expectedUrl: normalizedUrl,
            expectedType: normalizedType);
    }

    public Task<HomeAssistantDashboardResource> UpdateResourceAsync(string resourceId, string? url = null, HomeAssistantDashboardResourceType? type = null, CancellationToken cancellationToken = default)
    {
        var normalizedResourceId = Require(resourceId, nameof(resourceId), cancellationToken);
        var payload = new Dictionary<string, object?> { ["resource_id"] = normalizedResourceId };
        var normalizedUrl = url is null ? null : Require(url, nameof(url), cancellationToken);
        var normalizedType = type.HasValue ? ResourceTypeName(type.Value) : null;
        if (normalizedUrl is not null) payload["url"] = normalizedUrl;
        if (normalizedType is not null) payload["res_type"] = normalizedType;
        if (payload.Count == 1) throw new ArgumentException("At least one resource update is required.");
        return RequestResourceAsync(
            "lovelace/resources/update",
            payload,
            cancellationToken,
            normalizedResourceId,
            normalizedUrl,
            normalizedType);
    }

    public Task<JsonElement> DeleteResourceAsync(string resourceId, CancellationToken cancellationToken = default)
        => RequestJsonAsync(
            "lovelace/resources/delete",
            new Dictionary<string, object?> { ["resource_id"] = Require(resourceId, nameof(resourceId), cancellationToken) },
            "The Lovelace resource deletion response contained duplicate JSON properties.",
            cancellationToken);

    private async Task<HomeAssistantDashboard> RequestDashboardAsync(
        string command,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken,
        string? expectedDashboardId = null,
        string? expectedUrlPath = null,
        string? expectedTitle = null,
        bool validateIcon = false,
        string? expectedIcon = null,
        bool? expectedShowInSidebar = null,
        bool? expectedRequireAdmin = null)
    {
        var value = await _webSocket.RequestAsync(command, payload, cancellationToken).ConfigureAwait(false);
        RequireNoDuplicateProperties(
            value,
            "A dashboard mutation response contained duplicate JSON properties.",
            cancellationToken);
        var dashboard = HomeAssistantJson.DeserializeResponse<HomeAssistantDashboard>(
            value,
            "The dashboard response could not be decoded.",
            cancellationToken: cancellationToken);
        ValidateStorageDashboard(dashboard, cancellationToken);
        RequireDashboardVisibility(value, "A dashboard mutation response did not contain its required visibility fields.");
        if (expectedDashboardId is not null && !string.Equals(dashboard.Id, expectedDashboardId, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested identifier.");
        if (expectedUrlPath is not null && !string.Equals(dashboard.UrlPath, expectedUrlPath, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested URL path.");
        if (expectedTitle is not null && !string.Equals(dashboard.Title, expectedTitle, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested title.");
        if (validateIcon && !string.Equals(dashboard.Icon, expectedIcon, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested icon.");
        if (expectedShowInSidebar.HasValue
            && (!value.TryGetProperty("show_in_sidebar", out _)
                || dashboard.ShowInSidebar != expectedShowInSidebar.Value))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested sidebar visibility.");
        if (expectedRequireAdmin.HasValue
            && (!value.TryGetProperty("require_admin", out _)
                || dashboard.RequireAdmin != expectedRequireAdmin.Value))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested administrator requirement.");
        return dashboard;
    }

    private async Task<HomeAssistantDashboardResource> RequestResourceAsync(
        string command,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken,
        string? expectedResourceId = null,
        string? expectedUrl = null,
        string? expectedType = null)
    {
        var value = await _webSocket.RequestAsync(command, payload, cancellationToken).ConfigureAwait(false);
        RequireNoDuplicateProperties(
            value,
            "A Lovelace resource mutation response contained duplicate JSON properties.",
            cancellationToken);
        var resource = HomeAssistantJson.DeserializeResponse<HomeAssistantDashboardResource>(
            value,
            "The Lovelace resource response could not be decoded.",
            cancellationToken: cancellationToken);
        ValidateStorageResource(resource);
        if (expectedResourceId is not null && !string.Equals(resource.Id, expectedResourceId, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A Lovelace resource mutation response did not match the requested identifier.");
        if (expectedUrl is not null && !string.Equals(resource.Url, expectedUrl, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A Lovelace resource mutation response did not match the requested URL.");
        if (expectedType is not null && !string.Equals(resource.Type, expectedType, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A Lovelace resource mutation response did not match the requested type.");
        return resource;
    }

    private async Task<JsonElement> RequestJsonAsync(
        string command,
        IReadOnlyDictionary<string, object?> payload,
        string duplicateFailureMessage,
        CancellationToken cancellationToken)
    {
        var value = await _webSocket.RequestAsync(command, payload, cancellationToken).ConfigureAwait(false);
        RequireNoDuplicateProperties(value, duplicateFailureMessage, cancellationToken);
        return value;
    }

    private static void RequireNoDuplicateProperties(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (HomeAssistantJson.HasDuplicateProperties(value, cancellationToken))
        {
            throw new HomeAssistantProtocolException(failureMessage);
        }
    }

    private static void ValidateListedDashboard(HomeAssistantDashboard dashboard, CancellationToken cancellationToken)
    {
        dashboard.UrlPath = RequireResponseUrlPath(dashboard.UrlPath, "A dashboard did not contain a canonical URL path.", cancellationToken);
        if (string.IsNullOrWhiteSpace(dashboard.Title)
            || string.IsNullOrWhiteSpace(dashboard.Mode))
            throw new HomeAssistantProtocolException("A dashboard did not contain its required fields.");
        if (dashboard.Mode == "storage")
        {
            dashboard.Id = RequireResponseSelector(dashboard.Id, "A storage dashboard did not contain a canonical identifier.");
        }
        else if (dashboard.Mode == "yaml")
        {
            if (dashboard.FileName is not string fileName
                || string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(fileName, fileName.Trim(), StringComparison.Ordinal))
                throw new HomeAssistantProtocolException("A YAML dashboard did not contain a canonical filename.");
        }
        else
        {
            throw new HomeAssistantProtocolException("A dashboard contained an unsupported mode.");
        }
        if (dashboard.Icon is not null
            && (!HomeAssistantDashboardIdentifier.TryNormalizeIcon(dashboard.Icon, out var normalizedIcon)
                || !string.Equals(dashboard.Icon, normalizedIcon, StringComparison.Ordinal)))
        {
            throw new HomeAssistantProtocolException("A dashboard contained a noncanonical icon.");
        }
    }

    private static void RequireDashboardVisibility(JsonElement value, string failureMessage)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("show_in_sidebar", out var showInSidebar)
            || showInSidebar.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !value.TryGetProperty("require_admin", out var requireAdmin)
            || requireAdmin.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new HomeAssistantProtocolException(failureMessage);
        }
    }

    private static void RequirePanelBooleans(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object
            || !HasBooleanProperty(value, "default_visible")
            || !HasBooleanProperty(value, "require_admin")
            || !HasBooleanProperty(value, "show_in_sidebar"))
        {
            throw new HomeAssistantProtocolException("A frontend panel did not contain its required Boolean fields.");
        }
    }

    private static bool HasBooleanProperty(JsonElement value, string propertyName)
        => value.TryGetProperty(propertyName, out var property)
            && property.ValueKind is JsonValueKind.True or JsonValueKind.False;

    private static void ValidateStorageDashboard(HomeAssistantDashboard dashboard, CancellationToken cancellationToken)
    {
        ValidateListedDashboard(dashboard, cancellationToken);
        if (!string.Equals(dashboard.Mode, "storage", StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A dashboard mutation response was not a storage dashboard.");
    }

    private static void ValidateListedResource(HomeAssistantDashboardResource resource, string? resourceMode = null)
    {
        if (string.IsNullOrWhiteSpace(resource.Url)
            || !string.Equals(resource.Url, resource.Url.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(resource.Type)
            || !string.Equals(resource.Type, resource.Type.Trim(), StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A Lovelace resource did not contain its required fields.");
        if (resourceMode == "storage")
            resource.Id = RequireResponseSelector(resource.Id, "A storage Lovelace resource did not contain a canonical identifier.");
    }

    private static void ValidateStorageResource(HomeAssistantDashboardResource resource)
    {
        ValidateListedResource(resource);
        resource.Id = RequireResponseSelector(resource.Id, "A Lovelace resource mutation response did not contain a canonical identifier.");
    }

    private static string ResourceTypeName(HomeAssistantDashboardResourceType value) => value switch
    {
        HomeAssistantDashboardResourceType.JavaScript => "js",
        HomeAssistantDashboardResourceType.Css => "css",
        HomeAssistantDashboardResourceType.Module => "module",
        HomeAssistantDashboardResourceType.Html => "html",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string Require(
        string value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (value is null) throw new ArgumentNullException(parameterName);
        cancellationToken.ThrowIfCancellationRequested();
        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            if ((start & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            start++;
        }

        var end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            if (((value.Length - 1 - end) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            end--;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (end < start) throw new ArgumentException("A non-empty value is required.", parameterName);
        return start == 0 && end == value.Length - 1
            ? value
            : value.Substring(start, end - start + 1);
    }

    private static string RequireConfigurationUrlPath(string? value, string parameterName, CancellationToken cancellationToken)
    {
        if (!HomeAssistantDashboardIdentifier.TryNormalizeUrlPath(value, true, out var normalized, cancellationToken))
            throw new ArgumentException("Dashboard configuration URL paths must be canonical lowercase slugs containing only letters, numbers, and single hyphens.", parameterName);
        return normalized;
    }

    private static string RequireResponseUrlPath(string? value, string failureMessage, CancellationToken cancellationToken)
    {
        if (!HomeAssistantDashboardIdentifier.TryNormalizeUrlPath(value, true, out var normalized, cancellationToken)
            || !string.Equals(value, normalized, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException(failureMessage);
        return normalized;
    }

    private static string RequireResponseSelector(string? value, string failureMessage)
    {
        if (value is not string selector
            || string.IsNullOrWhiteSpace(selector)
            || !string.Equals(selector, selector.Trim(), StringComparison.Ordinal))
            throw new HomeAssistantProtocolException(failureMessage);
        return selector;
    }

    private static string RequireIcon(string value, string parameterName)
    {
        if (!HomeAssistantDashboardIdentifier.TryNormalizeIcon(value, out var normalized))
            throw new ArgumentException("A dashboard icon must contain a ':' separator.", parameterName);
        return normalized;
    }
}
