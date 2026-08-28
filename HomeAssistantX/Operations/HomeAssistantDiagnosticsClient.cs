using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Operations;

/// <summary>Discovers and downloads integration diagnostics generated and redacted by Home Assistant.</summary>
public sealed class HomeAssistantDiagnosticsClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantDiagnosticsClient(HomeAssistantRestClient rest, HomeAssistantWebSocketClient webSocket)
    {
        _rest = rest;
        _webSocket = webSocket;
    }

    public async Task<IReadOnlyList<HomeAssistantDiagnosticHandler>> GetHandlersAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.RequestAsync("diagnostics/list", null, cancellationToken).ConfigureAwait(false);
        return HomeAssistantJson.DeserializeResponse<HomeAssistantDiagnosticHandler[]>(
            result,
            "The Home Assistant diagnostics handlers could not be decoded.",
            cancellationToken: cancellationToken);
    }

    public Task<byte[]> GetConfigEntryAsync(
        string entryId,
        CancellationToken cancellationToken = default)
    {
        return _rest.GetBytesAsync(
            "api/diagnostics/config_entry/" + Escape(entryId, nameof(entryId)),
            cancellationToken);
    }

    public Task<byte[]> GetDeviceAsync(
        string entryId,
        string deviceId,
        CancellationToken cancellationToken = default)
    {
        return _rest.GetBytesAsync(
            "api/diagnostics/config_entry/" + Escape(entryId, nameof(entryId))
            + "/device/" + Escape(deviceId, nameof(deviceId)),
            cancellationToken);
    }

    private static string Escape(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty identifier is required.", parameterName);
        }

        return Uri.EscapeDataString(value);
    }
}

public sealed class HomeAssistantDiagnosticHandler
{
    [JsonPropertyName("domain")]
    public string Domain { get; set; } = string.Empty;

    [JsonPropertyName("handlers")]
    public HomeAssistantDiagnosticHandlerSupport Handlers { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class HomeAssistantDiagnosticHandlerSupport
{
    [JsonPropertyName("config_entry")]
    public bool ConfigEntry { get; set; }

    [JsonPropertyName("device")]
    public bool Device { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
