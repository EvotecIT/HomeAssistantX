using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.MobileApp;

/// <summary>Describes a Home Assistant mobile-app registration owned by the calling application.</summary>
public sealed class HomeAssistantMobileAppRegistrationRequest
{
    private static readonly HashSet<string> KnownPropertyNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "app_id", "app_name", "app_version", "device_name", "manufacturer", "model",
        "device_id", "os_name", "os_version", "supports_encryption", "app_data"
    };

    [JsonPropertyName("app_id")]
    public string AppId { get; set; } = string.Empty;

    [JsonPropertyName("app_name")]
    public string AppName { get; set; } = string.Empty;

    [JsonPropertyName("app_version")]
    public string AppVersion { get; set; } = string.Empty;

    [JsonPropertyName("device_name")]
    public string DeviceName { get; set; } = string.Empty;

    [JsonPropertyName("manufacturer")]
    public string Manufacturer { get; set; } = string.Empty;

    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("device_id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? DeviceId { get; set; }

    [JsonPropertyName("os_name")]
    public string OperatingSystemName { get; set; } = string.Empty;

    [JsonPropertyName("os_version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? OperatingSystemVersion { get; set; }

    [JsonPropertyName("supports_encryption")]
    public bool SupportsEncryption { get; set; }

    [JsonPropertyName("app_data")]
    public IReadOnlyDictionary<string, object?> AppData { get; set; } = new Dictionary<string, object?>();

    /// <summary>Preserves provider-specific top-level registration fields not yet modeled by HomeAssistantX.</summary>
    [JsonExtensionData]
    public Dictionary<string, object?> AdditionalData { get; set; } = new(StringComparer.Ordinal);

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
            if (KnownPropertyNames.Contains(property.Key))
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
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!char.IsWhiteSpace(value[index])) return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
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
    [JsonPropertyName("webhook_id")]
    public string WebhookId { get; set; } = string.Empty;

    [JsonPropertyName("secret")]
    public string? Secret { get; set; }

    [JsonPropertyName("cloudhook_url")]
    public Uri? CloudhookUri { get; set; }

    [JsonPropertyName("remote_ui_url")]
    public Uri? RemoteUiUri { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);

    public override string ToString() => string.IsNullOrWhiteSpace(WebhookId)
        ? "Home Assistant mobile-app registration (invalid)"
        : "Home Assistant mobile-app registration (credential redacted)";
}

public sealed class HomeAssistantMobileAppRegistrationUpdate
{
    public HomeAssistantMobileAppRegistrationUpdate(
        string appVersion,
        string deviceName,
        string manufacturer,
        string model)
    {
        AppVersion = Required(appVersion, nameof(appVersion));
        DeviceName = Required(deviceName, nameof(deviceName));
        Manufacturer = Required(manufacturer, nameof(manufacturer));
        Model = Required(model, nameof(model));
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
    public string? OperatingSystemVersion { get; set; }

    [JsonPropertyName("app_data")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, object?>? AppData { get; set; }

    internal string? CaptureValidatedOperatingSystemVersion(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var value = OperatingSystemVersion;
        Optional(value, nameof(OperatingSystemVersion), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A required value cannot be empty.", name);
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
