using System.Globalization;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;
using HomeAssistantX.States;
using HomeAssistantX.Subscriptions;
using HomeAssistantX.Systems;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Cameras;

/// <summary>Provides camera state, bounded snapshots, streams, signed paths, preferences, and push updates.</summary>
public sealed class HomeAssistantCameraClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantStateClient _states;
    private readonly HomeAssistantWebSocketClient _webSocket;
    private readonly HomeAssistantSystemClient _system;

    internal HomeAssistantCameraClient(HomeAssistantRestClient rest, HomeAssistantStateClient states, HomeAssistantWebSocketClient webSocket, HomeAssistantSystemClient system)
    {
        _rest = rest;
        _states = states;
        _webSocket = webSocket;
        _system = system;
    }

    public async Task<IReadOnlyList<HomeAssistantCameraStatus>> GetAsync(CancellationToken cancellationToken = default)
    {
        var states = HomeAssistantEntityId.RequireResponseDomainStates(
            await _states.GetAllAsync(cancellationToken).ConfigureAwait(false),
            "camera",
            cancellationToken);
        var result = new List<HomeAssistantCameraStatus>();
        foreach (var state in states)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(ToStatus(state));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var comparer = new CancellationAwareStringComparer(StringComparer.OrdinalIgnoreCase, cancellationToken);
        CancellationAwareSort.Sort(result, (left, right) => comparer.Compare(left.EntityId, right.EntityId));
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public async Task<HomeAssistantCameraStatus> GetAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        var normalizedEntityId = entityId.Trim();
        var state = await _states.GetAsync(normalizedEntityId, cancellationToken).ConfigureAwait(false);
        return ToStatus(HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId));
    }

    public async Task<byte[]> GetSnapshotAsync(string entityId, int? width = null, int? height = null, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        ValidateDimensions(width, height);
        var snapshot = width.HasValue
            ? await _rest.GetCameraImageAsync(entityId.Trim(), width.Value, height!.Value, cancellationToken).ConfigureAwait(false)
            : await _rest.GetCameraImageAsync(entityId.Trim(), cancellationToken).ConfigureAwait(false);
        if (snapshot.Length == 0)
            throw new HomeAssistantProtocolException("Home Assistant returned an empty camera snapshot.");
        return snapshot;
    }

    public async Task<HomeAssistantCameraCapabilities> GetCapabilitiesAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        var value = await _webSocket.RequestAsync("camera/capabilities", new Dictionary<string, object?> { ["entity_id"] = entityId.Trim() }, cancellationToken).ConfigureAwait(false);
        RequireNoDuplicateProperties(value, "The camera capabilities contained duplicate JSON properties.", cancellationToken);
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("frontend_stream_types", out var frontendStreamTypes)
            || frontendStreamTypes.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantProtocolException("The camera capabilities omitted their frontend stream-type collection.");
        }
        var result = HomeAssistantJson.DeserializeResponse<HomeAssistantCameraCapabilities>(
            value,
            "The camera capabilities could not be decoded.",
            cancellationToken: cancellationToken);
        if (result.FrontendStreamTypes is null)
            throw new HomeAssistantProtocolException("The camera capabilities contained a null stream-type collection.");
        HomeAssistantJson.RequireNoNullCollectionEntries(
            result.FrontendStreamTypes,
            "The camera capabilities contained a null stream type.",
            cancellationToken: cancellationToken);
        ValidateStreamTypes(result.FrontendStreamTypes, cancellationToken);
        return result;
    }

    public async Task<HomeAssistantCameraStream> GetStreamAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        var value = await _webSocket.RequestAsync("camera/stream", new Dictionary<string, object?> { ["entity_id"] = entityId.Trim(), ["format"] = "hls" }, cancellationToken).ConfigureAwait(false);
        RequireNoDuplicateProperties(value, "The camera stream response contained duplicate JSON properties.", cancellationToken);
        var stream = HomeAssistantJson.DeserializeResponse<HomeAssistantCameraStream>(
            value,
            "The camera stream response could not be decoded.",
            cancellationToken: cancellationToken);
        if (!HomeAssistantRootRelativePath.IsValid(stream.Path))
        {
            throw new HomeAssistantProtocolException("Home Assistant did not return a valid root-relative camera stream path.");
        }
        stream.EntityId = entityId.Trim();
        stream.Format = "hls";
        return stream;
    }

    public async Task<HomeAssistantCameraPreferences> GetPreferencesAsync(string entityId, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        var value = await _webSocket.RequestAsync("camera/get_prefs", new Dictionary<string, object?> { ["entity_id"] = entityId.Trim() }, cancellationToken).ConfigureAwait(false);
        return DecodePreferences(value, "The camera preferences could not be decoded.", cancellationToken);
    }

    public async Task<HomeAssistantCameraPreferences> SavePreferencesAsync(string entityId, HomeAssistantCameraPreferencesUpdate update, CancellationToken cancellationToken = default)
    {
        ValidateEntityId(entityId);
        if (update is null) throw new ArgumentNullException(nameof(update));
        var expectedPreloadStream = update.PreloadStream;
        var expectedOrientation = update.Orientation;
        var payload = update.ToPayload(entityId.Trim());
        var value = await _webSocket.RequestAsync("camera/update_prefs", payload, cancellationToken).ConfigureAwait(false);
        var preferences = DecodePreferences(value, "The updated camera preferences could not be decoded.", cancellationToken);
        if (expectedPreloadStream.HasValue
            && (!value.TryGetProperty("preload_stream", out _)
                || preferences.PreloadStream != expectedPreloadStream.Value))
            throw new HomeAssistantProtocolException("The updated camera preferences did not match the requested preload-stream value.");
        if (expectedOrientation.HasValue
            && (!value.TryGetProperty("orientation", out _)
                || preferences.Orientation != expectedOrientation.Value))
            throw new HomeAssistantProtocolException("The updated camera preferences did not match the requested orientation.");
        return preferences;
    }

    private static HomeAssistantCameraPreferences DecodePreferences(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        RequireNoDuplicateProperties(value, failureMessage, cancellationToken);
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("preload_stream", out var preloadStream)
            || preloadStream.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !value.TryGetProperty("orientation", out var orientation)
            || orientation.ValueKind != JsonValueKind.Number
            || !orientation.TryGetInt32(out _))
        {
            throw new HomeAssistantProtocolException(failureMessage);
        }

        var preferences = HomeAssistantJson.DeserializeResponse<HomeAssistantCameraPreferences>(
            value,
            failureMessage,
            cancellationToken: cancellationToken);
        if (!Enum.IsDefined(typeof(HomeAssistantCameraOrientation), preferences.Orientation))
            throw new HomeAssistantProtocolException(failureMessage);
        return preferences;
    }

    private static void RequireNoDuplicateProperties(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (HomeAssistantJson.HasDuplicateProperties(value, cancellationToken))
            throw new HomeAssistantProtocolException(failureMessage);
    }

    internal static void ValidateStreamTypes(
        IEnumerable<string> values,
        CancellationToken cancellationToken)
    {
        var streamTypes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var streamType in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(streamType)
                || !string.Equals(streamType, streamType.Trim(), StringComparison.Ordinal)
                || !string.Equals(streamType, streamType.ToLowerInvariant(), StringComparison.Ordinal)
                || !streamTypes.Add(streamType))
            {
                throw new HomeAssistantProtocolException("The camera capabilities contained an invalid stream type.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    public async Task<string> GetSignedImagePathAsync(string entityId, TimeSpan? expiration = null, int? width = null, int? height = null, CancellationToken cancellationToken = default)
    {
        ValidateDimensions(width, height);
        var path = "/api/camera_proxy/" + Uri.EscapeDataString(ValidateEntityId(entityId));
        var signedPath = await _system.SignPathAsync(path, expiration, cancellationToken).ConfigureAwait(false);
        if (!width.HasValue) return signedPath;

        var separator = signedPath.IndexOf('?') >= 0 ? "&" : "?";
        return signedPath + separator
            + "width=" + width.Value.ToString(CultureInfo.InvariantCulture)
            + "&height=" + height!.Value.ToString(CultureInfo.InvariantCulture);
    }

    public async Task<string> GetSignedMjpegStreamPathAsync(string entityId, TimeSpan? expiration = null, double? intervalSeconds = null, CancellationToken cancellationToken = default)
    {
        var validated = ValidateEntityId(entityId);
        if (intervalSeconds.HasValue && (double.IsNaN(intervalSeconds.Value) || double.IsInfinity(intervalSeconds.Value) || intervalSeconds.Value < 0.5))
            throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Camera stream interval must be at least 0.5 seconds.");
        var path = "/api/camera_proxy_stream/" + Uri.EscapeDataString(validated);
        var signedPath = await _system.SignPathAsync(path, expiration, cancellationToken).ConfigureAwait(false);
        if (!intervalSeconds.HasValue) return signedPath;

        var separator = signedPath.IndexOf('?') >= 0 ? "&" : "?";
        return signedPath + separator + "interval=" + intervalSeconds.Value.ToString("R", CultureInfo.InvariantCulture);
    }

    public Task<IHomeAssistantSubscription> SubscribeAsync(Func<HomeAssistantCameraStateChange, CancellationToken, Task> handler, CancellationToken cancellationToken = default)
    {
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        return _states.SubscribeAsync(HomeAssistantStateFilter.ForDomains("camera"), (change, token) => handler(new HomeAssistantCameraStateChange(change.EntityId, ToOptionalStatus(change.PreviousState), ToOptionalStatus(change.CurrentState)), token), cancellationToken);
    }

    private static HomeAssistantCameraStatus? ToOptionalStatus(HomeAssistantState? state)
    {
        if (state is null) return null;
        return ToStatus(HomeAssistantEntityId.RequireResponseDomain(state, "camera"));
    }

    private static HomeAssistantCameraStatus ToStatus(HomeAssistantState state)
    {
        if (!string.Equals(state.Domain, "camera", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("The entity is not a camera.", nameof(state));
        if (string.IsNullOrWhiteSpace(state.State)) throw new HomeAssistantProtocolException("The Home Assistant camera state omitted its required state value.");
        return new HomeAssistantCameraStatus
        {
            EntityId = state.EntityId,
            Name = HomeAssistantAttributeReader.GetString(state.Attributes, "friendly_name"),
            State = state.State,
            Brand = HomeAssistantAttributeReader.GetString(state.Attributes, "brand"),
            Model = HomeAssistantAttributeReader.GetString(state.Attributes, "model_name"),
            MotionDetectionEnabled = HomeAssistantAttributeReader.GetBoolean(state.Attributes, "motion_detection"),
            EntityPicture = HomeAssistantAttributeReader.GetString(state.Attributes, "entity_picture"),
            SupportedFeatures = (HomeAssistantCameraFeature)(HomeAssistantAttributeReader.GetNonNegativeInt32(state.Attributes, "supported_features") ?? 0),
            RawState = state
        };
    }

    private static string ValidateEntityId(string entityId)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "camera", out var normalized))
            throw new ArgumentException("A camera entity identifier is required.", nameof(entityId));
        return normalized;
    }

    private static void ValidateDimensions(int? width, int? height)
    {
        if (width.HasValue != height.HasValue) throw new ArgumentException("Camera image width and height must be supplied together.");
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width), "Camera image dimensions must be positive.");
    }
}
