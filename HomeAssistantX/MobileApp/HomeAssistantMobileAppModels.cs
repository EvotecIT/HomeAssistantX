using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.MobileApp;

/// <summary>Describes a Home Assistant mobile-app registration owned by the calling application.</summary>
public sealed class HomeAssistantMobileAppRegistrationRequest
{
    private readonly object _sync = new();
    private static readonly string[] KnownPropertyNames =
    {
        "app_id", "app_name", "app_version", "device_name", "manufacturer", "model",
        "device_id", "os_name", "os_version", "supports_encryption", "app_data"
    };

    private string _appId = string.Empty;
    private string _appName = string.Empty;
    private string _appVersion = string.Empty;
    private string _deviceName = string.Empty;
    private string _manufacturer = string.Empty;
    private string _model = string.Empty;
    private string? _deviceId;
    private string _operatingSystemName = string.Empty;
    private string? _operatingSystemVersion;
    private bool _supportsEncryption;
    private IReadOnlyDictionary<string, object?> _appData = new Dictionary<string, object?>();
    private Dictionary<string, object?> _additionalData = new(StringComparer.Ordinal);

    [JsonPropertyName("app_id")]
    public string AppId { get { lock (_sync) return _appId; } set { lock (_sync) _appId = value; } }

    [JsonPropertyName("app_name")]
    public string AppName { get { lock (_sync) return _appName; } set { lock (_sync) _appName = value; } }

    [JsonPropertyName("app_version")]
    public string AppVersion { get { lock (_sync) return _appVersion; } set { lock (_sync) _appVersion = value; } }

    [JsonPropertyName("device_name")]
    public string DeviceName { get { lock (_sync) return _deviceName; } set { lock (_sync) _deviceName = value; } }

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get { lock (_sync) return _manufacturer; } set { lock (_sync) _manufacturer = value; } }

    [JsonPropertyName("model")]
    public string Model { get { lock (_sync) return _model; } set { lock (_sync) _model = value; } }

    [JsonPropertyName("device_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceId { get { lock (_sync) return _deviceId; } set { lock (_sync) _deviceId = value; } }

    [JsonPropertyName("os_name")]
    public string OperatingSystemName { get { lock (_sync) return _operatingSystemName; } set { lock (_sync) _operatingSystemName = value; } }

    [JsonPropertyName("os_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatingSystemVersion { get { lock (_sync) return _operatingSystemVersion; } set { lock (_sync) _operatingSystemVersion = value; } }

    [JsonPropertyName("supports_encryption")]
    public bool SupportsEncryption { get { lock (_sync) return _supportsEncryption; } set { lock (_sync) _supportsEncryption = value; } }

    [JsonPropertyName("app_data")]
    public IReadOnlyDictionary<string, object?> AppData { get { lock (_sync) return _appData; } set { lock (_sync) _appData = value; } }

    /// <summary>Preserves provider-specific top-level registration fields not yet modeled by HomeAssistantX.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?> AdditionalData { get { lock (_sync) return _additionalData; } set { lock (_sync) _additionalData = value; } }

    internal HomeAssistantMobileAppRegistrationRequest Snapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new HomeAssistantMobileAppRegistrationRequest
            {
                AppId = _appId,
                AppName = _appName,
                AppVersion = _appVersion,
                DeviceName = _deviceName,
                Manufacturer = _manufacturer,
                Model = _model,
                DeviceId = _deviceId,
                OperatingSystemName = _operatingSystemName,
                OperatingSystemVersion = _operatingSystemVersion,
                SupportsEncryption = _supportsEncryption,
                AppData = _appData,
                AdditionalData = _additionalData
            };
        }
    }

    internal void Validate(CancellationToken cancellationToken)
    {
        Required(AppId, nameof(AppId), cancellationToken);
        Required(AppName, nameof(AppName), cancellationToken);
        Required(AppVersion, nameof(AppVersion), cancellationToken);
        Required(DeviceName, nameof(DeviceName), cancellationToken);
        Required(Manufacturer, nameof(Manufacturer), cancellationToken);
        Required(Model, nameof(Model), cancellationToken);
        Required(OperatingSystemName, nameof(OperatingSystemName), cancellationToken);
        if (AppData is null) throw new ArgumentNullException(nameof(AppData));
        if (AdditionalData is null) throw new ArgumentNullException(nameof(AdditionalData));
        foreach (var property in AdditionalData)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasNonWhitespace(property.Key, cancellationToken))
                throw new ArgumentException("Additional registration field names cannot be blank.", nameof(AdditionalData));
            ObserveString(property.Key, cancellationToken);
            if (KnownPropertyNames.Any(name =>
                    CancellationAwareString.EqualsOrdinalIgnoreCase(name, property.Key, cancellationToken)))
                throw new ArgumentException(
                    "Additional registration fields cannot replace a modeled registration field.",
                    nameof(AdditionalData));
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void Required(string value, string name, CancellationToken cancellationToken)
    {
        if (!HasNonWhitespace(value, cancellationToken))
            throw new ArgumentException("A non-empty value is required.", name);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool HasNonWhitespace(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return false;
        var hasNonWhitespace = false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!char.IsWhiteSpace(value[index])) hasNonWhitespace = true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return hasNonWhitespace;
    }

    private static void ObserveString(string value, CancellationToken cancellationToken)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }
}

/// <summary>Contains webhook connection data returned once by Home Assistant.</summary>
public sealed class HomeAssistantMobileAppRegistration
{
    private readonly object _connectionGate = new();
    private string _webhookId = string.Empty;
    private string? _secret;
    private Uri? _cloudhookUri;
    private Uri? _remoteUiUri;

    [JsonPropertyName("webhook_id")]
    public string WebhookId
    {
        get { lock (_connectionGate) return _webhookId; }
        set { lock (_connectionGate) _webhookId = value; }
    }

    [JsonPropertyName("secret")]
    public string? Secret
    {
        get { lock (_connectionGate) return _secret; }
        set { lock (_connectionGate) _secret = value; }
    }

    [JsonPropertyName("cloudhook_url")]
    public Uri? CloudhookUri
    {
        get { lock (_connectionGate) return _cloudhookUri; }
        set { lock (_connectionGate) _cloudhookUri = value; }
    }

    [JsonPropertyName("remote_ui_url")]
    public Uri? RemoteUiUri
    {
        get { lock (_connectionGate) return _remoteUiUri; }
        set { lock (_connectionGate) _remoteUiUri = value; }
    }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);

    internal HomeAssistantMobileAppWebhookConnection SnapshotWebhookConnection()
    {
        lock (_connectionGate)
        {
            return new HomeAssistantMobileAppWebhookConnection(
                _webhookId,
                _secret,
                _cloudhookUri);
        }
    }

    public override string ToString() => string.IsNullOrWhiteSpace(SnapshotWebhookConnection().WebhookId)
        ? "Home Assistant mobile-app registration (invalid)"
        : "Home Assistant mobile-app registration (credential redacted)";
}

internal sealed class HomeAssistantMobileAppWebhookConnection
{
    internal HomeAssistantMobileAppWebhookConnection(
        string webhookId,
        string? secret,
        Uri? cloudhookUri)
    {
        WebhookId = webhookId;
        Secret = secret;
        CloudhookUri = cloudhookUri;
    }

    internal string WebhookId { get; }
    internal string? Secret { get; }
    internal Uri? CloudhookUri { get; }
}

public sealed class HomeAssistantMobileAppRegistrationUpdate
{
    private readonly object _sync = new();
    private string? _operatingSystemVersion;
    private IReadOnlyDictionary<string, object?>? _appData;

    public HomeAssistantMobileAppRegistrationUpdate(
        string appVersion,
        string deviceName,
        string manufacturer,
        string model)
        : this(appVersion, deviceName, manufacturer, model, default)
    {
    }

    internal HomeAssistantMobileAppRegistrationUpdate(
        string appVersion,
        string deviceName,
        string manufacturer,
        string model,
        CancellationToken cancellationToken)
    {
        AppVersion = Required(appVersion, nameof(appVersion), cancellationToken);
        DeviceName = Required(deviceName, nameof(deviceName), cancellationToken);
        Manufacturer = Required(manufacturer, nameof(manufacturer), cancellationToken);
        Model = Required(model, nameof(model), cancellationToken);
    }

    [JsonPropertyName("app_version")]
    public string AppVersion { get; }

    [JsonPropertyName("device_name")]
    public string DeviceName { get; }

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; }

    [JsonPropertyName("model")]
    public string Model { get; }

    [JsonPropertyName("os_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatingSystemVersion
    {
        get { lock (_sync) return _operatingSystemVersion; }
        set { lock (_sync) _operatingSystemVersion = value; }
    }

    [JsonPropertyName("app_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? AppData
    {
        get { lock (_sync) return _appData; }
        set { lock (_sync) _appData = value; }
    }

    internal HomeAssistantMobileAppRegistrationUpdate Snapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Optional(_operatingSystemVersion, nameof(OperatingSystemVersion), cancellationToken);
            return new HomeAssistantMobileAppRegistrationUpdate(
                AppVersion,
                DeviceName,
                Manufacturer,
                Model,
                cancellationToken)
            {
                OperatingSystemVersion = _operatingSystemVersion,
                AppData = _appData
            };
        }
    }

    private static string Required(string value, string name, CancellationToken cancellationToken)
    {
        if (HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken))
            throw new ArgumentException("A required value cannot be empty.", name);
        return value;
    }

    private static void Optional(
        string? value,
        string name,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return;
        var found = false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            found |= !char.IsWhiteSpace(value[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!found) throw new ArgumentException("A supplied value cannot be empty.", name);
    }
}

public sealed class HomeAssistantMobileAppCameraStream
{
    [JsonPropertyName("mjpeg_path")]
    public string? MjpegPath { get; set; }

    [JsonPropertyName("hls_path")]
    public string? HlsPath { get; set; }

    [JsonPropertyName("success")]
    public bool? Success { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>Encrypts and decrypts the mobile-app webhook payload format. Implementations should use NaCl SecretBox as required by Home Assistant.</summary>
public interface IHomeAssistantMobileAppPayloadProtector
{
    Task<string> ProtectAsync(byte[] plaintextJson, string secret, CancellationToken cancellationToken = default);

    Task<byte[]> UnprotectAsync(string protectedPayload, string secret, CancellationToken cancellationToken = default);
}
