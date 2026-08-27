using System.Net.Http;
using System.Text.Json;
using HomeAssistantX.Authentication;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Supervisor;

/// <summary>
/// Typed and raw access to the Home Assistant Supervisor API. It can use the administrator-only
/// Core WebSocket proxy or a direct Supervisor token; restore, wipe, and credential-recovery APIs
/// are intentionally not modeled as convenience methods.
/// </summary>
public sealed class HomeAssistantSupervisorClient : IDisposable
{
    private readonly IHomeAssistantSupervisorTransport _transport;
    private readonly IDisposable? _ownedTransport;

    public HomeAssistantSupervisorClient(
        HomeAssistantSupervisorClientOptions options,
        HttpClient? httpClient = null)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        var transport = new DirectSupervisorTransport(options, httpClient);
        _transport = transport;
        _ownedTransport = transport;
    }

    private HomeAssistantSupervisorClient(IHomeAssistantSupervisorTransport transport)
    {
        _transport = transport;
    }

    public static HomeAssistantSupervisorClient Create(
        Uri baseUri,
        string supervisorToken,
        HttpClient? httpClient = null)
    {
        return new HomeAssistantSupervisorClient(
            new HomeAssistantSupervisorClientOptions(
                baseUri,
                new StaticAccessTokenProvider(supervisorToken)),
            httpClient);
    }

    internal static HomeAssistantSupervisorClient CreateViaCore(
        HomeAssistantRestClient rest,
        HomeAssistantWebSocketClient webSocket)
    {
        return new HomeAssistantSupervisorClient(new CoreSupervisorTransport(rest, webSocket));
    }

    /// <summary>Gets Supervisor component version, health, and channel information.</summary>
    public async Task<HomeAssistantSupervisorInfo> GetInfoAsync(
        CancellationToken cancellationToken = default)
    {
        return Decode<HomeAssistantSupervisorInfo>(
            await SendAsync(HttpMethod.Get, "/supervisor/info", null, cancellationToken).ConfigureAwait(false),
            "Supervisor information");
    }

    /// <summary>Gets the combined Supervisor, Core, OS, and host installation overview.</summary>
    public async Task<HomeAssistantSupervisorOverview> GetOverviewAsync(
        CancellationToken cancellationToken = default)
    {
        return Decode<HomeAssistantSupervisorOverview>(
            await SendAsync(HttpMethod.Get, "/info", null, cancellationToken).ConfigureAwait(false),
            "Supervisor installation overview");
    }

    public Task<JsonElement> GetCoreInfoAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync(HttpMethod.Get, "/core/info", null, cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantSupervisorUpdate>> GetAvailableUpdatesAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(HttpMethod.Get, "/available_updates", null, cancellationToken).ConfigureAwait(false);
        return DecodeList<HomeAssistantSupervisorUpdate>(result, "available_updates", "Supervisor updates");
    }

    public async Task<IReadOnlyList<HomeAssistantApp>> GetAppsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(HttpMethod.Get, "/addons", null, cancellationToken).ConfigureAwait(false);
        var apps = DecodeList<HomeAssistantApp>(result, "addons", "Home Assistant apps");
        foreach (var app in apps)
        {
            // The /addons endpoint is the installed-app inventory and does not emit an installed flag.
            app.Installed = true;
        }

        return apps;
    }

    public async Task<IReadOnlyList<HomeAssistantBackup>> GetBackupsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(HttpMethod.Get, "/backups", null, cancellationToken).ConfigureAwait(false);
        return DecodeList<HomeAssistantBackup>(result, "backups", "Home Assistant backups");
    }

    public async Task<IReadOnlyList<HomeAssistantSupervisorJob>> GetJobsAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await SendAsync(HttpMethod.Get, "/jobs/info", null, cancellationToken).ConfigureAwait(false);
        return DecodeList<HomeAssistantSupervisorJob>(result, "jobs", "Supervisor jobs");
    }

    public async Task<HomeAssistantSupervisorJob> GetJobAsync(
        string jobId,
        CancellationToken cancellationToken = default)
    {
        return Decode<HomeAssistantSupervisorJob>(
            await SendAsync(
                HttpMethod.Get,
                "/jobs/" + Escape(jobId, nameof(jobId)),
                null,
                cancellationToken).ConfigureAwait(false),
            "Supervisor job");
    }

    public Task<JsonElement> GetResolutionAsync(CancellationToken cancellationToken = default)
    {
        return SendAsync(HttpMethod.Get, "/resolution/info", null, cancellationToken);
    }

    public Task<string> GetLogAsync(
        HomeAssistantSupervisorLogTarget target,
        int lines = 100,
        string? app = null,
        CancellationToken cancellationToken = default)
    {
        if (lines < 1 || lines > 10000)
        {
            throw new ArgumentOutOfRangeException(nameof(lines), "Log line count must be between 1 and 10000.");
        }

        var endpoint = target switch
        {
            HomeAssistantSupervisorLogTarget.Core => "/core/logs",
            HomeAssistantSupervisorLogTarget.Supervisor => "/supervisor/logs",
            HomeAssistantSupervisorLogTarget.Host => "/host/logs",
            HomeAssistantSupervisorLogTarget.App => "/addons/" + EscapeApp(app, nameof(app)) + "/logs",
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        return _transport.SendTextAsync(
            HttpMethod.Get,
            endpoint + "?lines=" + lines.ToString(System.Globalization.CultureInfo.InvariantCulture) + "&no_colors",
            null,
            cancellationToken);
    }

    public Task<JsonElement> CreateFullBackupAsync(
        HomeAssistantBackupRequest? request = null,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(HttpMethod.Post, "/backups/new/full", request ?? new HomeAssistantBackupRequest(), cancellationToken);
    }

    public Task<JsonElement> RestartAsync(
        HomeAssistantSupervisorRestartTarget target,
        CancellationToken cancellationToken = default)
    {
        var endpoint = target switch
        {
            HomeAssistantSupervisorRestartTarget.Core => "/core/restart",
            HomeAssistantSupervisorRestartTarget.Supervisor => "/supervisor/restart",
            HomeAssistantSupervisorRestartTarget.Host => "/host/reboot",
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        return SendAsync(HttpMethod.Post, endpoint, null, cancellationToken);
    }

    public Task<JsonElement> InstallUpdateAsync(
        HomeAssistantSupervisorUpdateTarget target,
        string? app = null,
        string? version = null,
        bool? backup = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = target switch
        {
            HomeAssistantSupervisorUpdateTarget.Core => "/core/update",
            HomeAssistantSupervisorUpdateTarget.Supervisor => "/supervisor/update",
            HomeAssistantSupervisorUpdateTarget.OperatingSystem => "/os/update",
            HomeAssistantSupervisorUpdateTarget.App => "/addons/" + EscapeApp(app, nameof(app)) + "/update",
            _ => throw new ArgumentOutOfRangeException(nameof(target))
        };
        var data = new Dictionary<string, object?>();
        if (!string.IsNullOrWhiteSpace(version))
        {
            data["version"] = version;
        }

        if (backup.HasValue)
        {
            data["backup"] = backup.Value;
        }

        return SendAsync(HttpMethod.Post, endpoint, data.Count == 0 ? null : data, cancellationToken);
    }

    public Task<JsonElement> InvokeAppAsync(
        string app,
        HomeAssistantAppOperation operation,
        CancellationToken cancellationToken = default)
    {
        var operationName = operation switch
        {
            HomeAssistantAppOperation.Install => "install",
            HomeAssistantAppOperation.Update => "update",
            HomeAssistantAppOperation.Start => "start",
            HomeAssistantAppOperation.Stop => "stop",
            HomeAssistantAppOperation.Restart => "restart",
            HomeAssistantAppOperation.Uninstall => "uninstall",
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "A supported app operation is required.")
        };
        return SendAsync(
            HttpMethod.Post,
            "/addons/" + EscapeApp(app, nameof(app)) + "/" + operationName,
            null,
            cancellationToken);
    }

    /// <summary>Sends a raw Supervisor request after enforcing a root-relative endpoint.</summary>
    public async Task<JsonElement> SendAsync(
        HttpMethod method,
        string endpoint,
        object? data = null,
        CancellationToken cancellationToken = default)
    {
        ValidateEndpoint(endpoint);
        var result = await _transport.SendAsync(method, endpoint, data, cancellationToken).ConfigureAwait(false);
        return Unwrap(result);
    }

    public void Dispose()
    {
        _ownedTransport?.Dispose();
    }

    private static JsonElement Unwrap(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("result", out var result)
            && result.ValueKind == JsonValueKind.String
            && value.TryGetProperty("data", out var data))
        {
            return data.Clone();
        }

        return value.Clone();
    }

    private static T Decode<T>(JsonElement value, string name)
    {
        return HomeAssistantJson.DeserializeResponse<T>(value, "The " + name + " response could not be decoded.");
    }

    private static IReadOnlyList<T> DecodeList<T>(JsonElement value, string propertyName, string name)
    {
        var list = value;
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty(propertyName, out var nested))
        {
            list = nested;
        }

        return HomeAssistantJson.DeserializeResponse<T[]>(list, "The " + name + " response could not be decoded.");
    }

    private static string Escape(string? value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty Supervisor identifier is required.", parameterName);
        }

        return Uri.EscapeDataString(value);
    }

    private static string EscapeApp(string? value, string parameterName)
    {
        if (!HomeAssistantSupervisorIdentifier.TryNormalizeAppSlug(value, out var normalized))
        {
            throw new ArgumentException("A valid Supervisor app/add-on slug is required.", parameterName);
        }

        return Uri.EscapeDataString(normalized);
    }

    private static void ValidateEndpoint(string endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint)
            || !endpoint.StartsWith("/", StringComparison.Ordinal)
            || endpoint.StartsWith("//", StringComparison.Ordinal)
            || endpoint.IndexOf('\\') >= 0
            || endpoint.IndexOf('#') >= 0
            || Uri.TryCreate(endpoint, UriKind.Absolute, out _))
        {
            throw new ArgumentException("A root-relative Supervisor endpoint is required.", nameof(endpoint));
        }

        string decodedPath;
        try
        {
            var queryIndex = endpoint.IndexOf('?');
            decodedPath = Uri.UnescapeDataString(queryIndex < 0 ? endpoint : endpoint.Substring(0, queryIndex));
        }
        catch (UriFormatException exception)
        {
            throw new ArgumentException("The Supervisor endpoint contains invalid escaping.", nameof(endpoint), exception);
        }

        if (decodedPath.Split('/').Any(segment => segment == "." || segment == ".."))
        {
            throw new ArgumentException("The Supervisor endpoint cannot contain path traversal segments.", nameof(endpoint));
        }
    }
}

internal interface IHomeAssistantSupervisorTransport
{
    Task<JsonElement> SendAsync(HttpMethod method, string endpoint, object? data, CancellationToken cancellationToken);

    Task<string> SendTextAsync(HttpMethod method, string endpoint, object? data, CancellationToken cancellationToken);
}
