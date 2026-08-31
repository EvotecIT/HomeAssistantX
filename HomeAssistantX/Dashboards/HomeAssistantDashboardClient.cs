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
        var routes = new HashSet<string>(
            new CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var propertyName = await HomeAssistantJson.GetPropertyNameAsync(property, cancellationToken).ConfigureAwait(false);
            var route = RequireResponsePanelRoute(propertyName, "A frontend panel contained an invalid route.", cancellationToken);
            if (!routes.Add(route))
                throw new HomeAssistantProtocolException("The frontend panel response contained a duplicate route.");
            var embeddedRoute = RequirePanelBooleans(property.Value, cancellationToken);
            var panel = HomeAssistantJson.DeserializeResponse<HomeAssistantPanel>(
                property.Value,
                "A frontend panel could not be decoded.",
                cancellationToken: cancellationToken);
            if (embeddedRoute.ValueKind == JsonValueKind.Undefined)
            {
                panel.UrlPath = route;
            }
            else
            {
                if (embeddedRoute.ValueKind != JsonValueKind.String)
                    throw new HomeAssistantProtocolException("A frontend panel contained an invalid route.");
                panel.UrlPath = RequireResponsePanelRoute(
                    await HomeAssistantJson.GetStringAsync(embeddedRoute, cancellationToken).ConfigureAwait(false),
                    "A frontend panel contained an invalid route.",
                    cancellationToken);
                if (!CancellationAwareString.EqualsOrdinal(panel.UrlPath, route, cancellationToken))
                    throw new HomeAssistantProtocolException("A frontend panel route did not match its registered key.");
            }
            if (string.IsNullOrWhiteSpace(panel.UrlPath))
                throw new HomeAssistantProtocolException("A frontend panel did not contain its required fields.");
            panel.ComponentName = RequireResponsePanelComponentName(
                panel.ComponentName,
                "A frontend panel contained a noncanonical component name.",
                cancellationToken);
            if (panel.Icon is not null
                && (!HomeAssistantDashboardIdentifier.TryNormalizeIcon(panel.Icon, out var normalizedIcon, cancellationToken)
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
        var comparer = new CancellationAwareStringComparer(StringComparison.OrdinalIgnoreCase, cancellationToken);
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
            RequireDashboardVisibility(item, "A dashboard did not contain its required visibility fields.", cancellationToken);
        }
        var dashboards = HomeAssistantJson.DeserializeResponse<HomeAssistantDashboard[]>(
            value,
            "The dashboard list could not be decoded.",
            cancellationToken: cancellationToken);
        var comparer = new CancellationAwareOrdinalStringEqualityComparer(cancellationToken);
        var urlPaths = new HashSet<string>(comparer);
        var storageIds = new HashSet<string>(comparer);
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
        var title = HomeAssistantDashboardIdentifier.RequireTitle(create.Title, nameof(create.Title), cancellationToken);
        var payload = new Dictionary<string, object?>
        {
            ["url_path"] = urlPath,
            ["title"] = title,
            ["show_in_sidebar"] = create.ShowInSidebar,
            ["require_admin"] = create.RequireAdmin
        };
        var icon = create.Icon is null ? null : RequireIcon(create.Icon, nameof(create.Icon), cancellationToken);
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
        var normalizedDashboardId = HomeAssistantDashboardIdentifier.RequireSelector(dashboardId, nameof(dashboardId), cancellationToken);
        var payload = new Dictionary<string, object?> { ["dashboard_id"] = normalizedDashboardId };
        var title = update.Title is null ? null : HomeAssistantDashboardIdentifier.RequireTitle(update.Title, nameof(update.Title), cancellationToken);
        var icon = update.Icon is null ? null : RequireIcon(update.Icon, nameof(update.Icon), cancellationToken);
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
            new Dictionary<string, object?> { ["dashboard_id"] = HomeAssistantDashboardIdentifier.RequireSelector(dashboardId, nameof(dashboardId), cancellationToken) },
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
        var storageIds = new HashSet<string>(
            new CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
        foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateListedResource(resource, resourceMode, cancellationToken);
            if (resourceMode == "storage" && !storageIds.Add(resource.Id))
                throw new HomeAssistantProtocolException("The Lovelace resource list contained a duplicate storage identifier.");
        }
        return resources;
    }

    public Task<HomeAssistantDashboardResource> CreateResourceAsync(string url, HomeAssistantDashboardResourceType type, CancellationToken cancellationToken = default)
    {
        var normalizedUrl = HomeAssistantDashboardIdentifier.RequireResourceUrl(url, nameof(url), cancellationToken);
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
        var normalizedResourceId = HomeAssistantDashboardIdentifier.RequireSelector(resourceId, nameof(resourceId), cancellationToken);
        var payload = new Dictionary<string, object?> { ["resource_id"] = normalizedResourceId };
        var normalizedUrl = url is null ? null : HomeAssistantDashboardIdentifier.RequireResourceUrl(url, nameof(url), cancellationToken);
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
            new Dictionary<string, object?> { ["resource_id"] = HomeAssistantDashboardIdentifier.RequireSelector(resourceId, nameof(resourceId), cancellationToken) },
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
        RequireDashboardVisibility(value, "A dashboard mutation response did not contain its required visibility fields.", cancellationToken);
        if (expectedDashboardId is not null && !string.Equals(dashboard.Id, expectedDashboardId, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested identifier.");
        if (expectedUrlPath is not null && !CancellationAwareString.EqualsOrdinal(dashboard.UrlPath, expectedUrlPath, cancellationToken))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested URL path.");
        if (expectedTitle is not null && !CancellationAwareString.EqualsOrdinal(dashboard.Title, expectedTitle, cancellationToken))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested title.");
        if (validateIcon && !string.Equals(dashboard.Icon, expectedIcon, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested icon.");
        if (expectedShowInSidebar.HasValue
            && dashboard.ShowInSidebar != expectedShowInSidebar.Value)
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested sidebar visibility.");
        if (expectedRequireAdmin.HasValue
            && dashboard.RequireAdmin != expectedRequireAdmin.Value)
            throw new HomeAssistantProtocolException("A dashboard mutation response did not match the requested administrator requirement.");
        cancellationToken.ThrowIfCancellationRequested();
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
        ValidateStorageResource(resource, cancellationToken);
        if (expectedResourceId is not null && !string.Equals(resource.Id, expectedResourceId, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A Lovelace resource mutation response did not match the requested identifier.");
        if (expectedUrl is not null && !CancellationAwareString.EqualsOrdinal(resource.Url, expectedUrl, cancellationToken))
            throw new HomeAssistantProtocolException("A Lovelace resource mutation response did not match the requested URL.");
        if (expectedType is not null && !string.Equals(resource.Type, expectedType, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A Lovelace resource mutation response did not match the requested type.");
        cancellationToken.ThrowIfCancellationRequested();
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
        if (!HasNonWhitespace(dashboard.Title, cancellationToken)
            || !IsCanonicalTrimmed(dashboard.Mode, cancellationToken))
            throw new HomeAssistantProtocolException("A dashboard did not contain its required fields.");
        if (dashboard.Mode == "storage")
        {
            dashboard.Id = RequireResponseSelector(dashboard.Id, "A storage dashboard did not contain a canonical identifier.", cancellationToken);
        }
        else if (dashboard.Mode == "yaml")
        {
            if (!IsCanonicalTrimmed(dashboard.FileName, cancellationToken))
                throw new HomeAssistantProtocolException("A YAML dashboard did not contain a canonical filename.");
        }
        else
        {
            throw new HomeAssistantProtocolException("A dashboard contained an unsupported mode.");
        }
        if (dashboard.Icon is not null
            && (!HomeAssistantDashboardIdentifier.TryNormalizeIcon(dashboard.Icon, out var normalizedIcon, cancellationToken)
                || !string.Equals(dashboard.Icon, normalizedIcon, StringComparison.Ordinal)))
        {
            throw new HomeAssistantProtocolException("A dashboard contained a noncanonical icon.");
        }
    }

    private static bool HasNonWhitespace(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return false;
        var found = false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            found |= !char.IsWhiteSpace(value[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return found;
    }

    private static string RequireResponsePanelComponentName(
        string? value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (!IsCanonicalTrimmed(value, cancellationToken))
            throw new HomeAssistantProtocolException(failureMessage);
        return value!;
    }

    internal static void RequireDashboardVisibility(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException(failureMessage);
        }

        var hasShowInSidebar = false;
        var hasRequireAdmin = false;
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (property.NameEquals("show_in_sidebar"))
            {
                hasShowInSidebar = property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False;
            }
            else if (property.NameEquals("require_admin"))
            {
                hasRequireAdmin = property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False;
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!hasShowInSidebar || !hasRequireAdmin)
        {
            throw new HomeAssistantProtocolException(failureMessage);
        }
    }

    private static JsonElement RequirePanelBooleans(JsonElement value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("A frontend panel did not contain its required Boolean fields.");
        }

        var hasRequireAdmin = false;
        var embeddedRoute = default(JsonElement);
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var isBoolean = property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False;
            if (property.NameEquals("require_admin")) hasRequireAdmin = isBoolean;
            else if (property.NameEquals("url_path")) embeddedRoute = property.Value;
            else if ((property.NameEquals("default_visible") || property.NameEquals("show_in_sidebar"))
                && !isBoolean)
            {
                throw new HomeAssistantProtocolException("A frontend panel contained an invalid optional Boolean field.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!hasRequireAdmin)
        {
            throw new HomeAssistantProtocolException("A frontend panel did not contain its required require_admin field.");
        }
        return embeddedRoute;
    }

    private static void ValidateStorageDashboard(HomeAssistantDashboard dashboard, CancellationToken cancellationToken)
    {
        ValidateListedDashboard(dashboard, cancellationToken);
        if (!string.Equals(dashboard.Mode, "storage", StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A dashboard mutation response was not a storage dashboard.");
    }

    private static void ValidateListedResource(
        HomeAssistantDashboardResource resource,
        string? resourceMode,
        CancellationToken cancellationToken)
    {
        if (!IsNonBlankPreserved(resource.Url, cancellationToken)
            || !IsCanonicalTrimmed(resource.Type, cancellationToken))
            throw new HomeAssistantProtocolException("A Lovelace resource did not contain its required fields.");
        if (resourceMode == "storage")
            resource.Id = RequireResponseSelector(resource.Id, "A storage Lovelace resource did not contain a canonical identifier.", cancellationToken);
    }

    private static bool IsNonBlankPreserved(string? value, CancellationToken cancellationToken)
    {
        if (value is null || CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken)) return false;
        CancellationAwareString.Observe(value, cancellationToken);
        return true;
    }

    private static void ValidateStorageResource(HomeAssistantDashboardResource resource, CancellationToken cancellationToken)
    {
        ValidateListedResource(resource, null, cancellationToken);
        resource.Id = RequireResponseSelector(resource.Id, "A Lovelace resource mutation response did not contain a canonical identifier.", cancellationToken);
    }

    private static string ResourceTypeName(HomeAssistantDashboardResourceType value) => value switch
    {
        HomeAssistantDashboardResourceType.JavaScript => "js",
        HomeAssistantDashboardResourceType.Css => "css",
        HomeAssistantDashboardResourceType.Module => "module",
        HomeAssistantDashboardResourceType.Html => "html",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

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

    private static string RequireResponseSelector(
        string? value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (!HomeAssistantDashboardIdentifier.TryNormalizeSelector(value, out var selector, cancellationToken)
            || !string.Equals(value, selector, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException(failureMessage);
        return selector;
    }

    private static string RequireResponsePanelRoute(
        string? value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (!IsCanonicalTrimmed(value, cancellationToken))
            throw new HomeAssistantProtocolException(failureMessage);
        return value!;
    }

    private static string RequireIcon(string value, string parameterName, CancellationToken cancellationToken)
    {
        if (!HomeAssistantDashboardIdentifier.TryNormalizeIcon(value, out var normalized, cancellationToken))
            throw new ArgumentException("A dashboard icon must contain a ':' separator.", parameterName);
        return normalized;
    }

    private static bool IsCanonicalTrimmed(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null
            || value.Length == 0
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[value.Length - 1]))
            return false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }
}
