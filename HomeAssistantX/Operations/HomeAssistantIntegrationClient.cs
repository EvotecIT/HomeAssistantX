using System.Net.Http;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Registries;
using HomeAssistantX.Rest;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Operations;

/// <summary>Reads and performs bounded lifecycle operations on Home Assistant configuration entries.</summary>
public sealed class HomeAssistantIntegrationClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantIntegrationClient(HomeAssistantRestClient rest, HomeAssistantWebSocketClient webSocket)
    {
        _rest = rest;
        _webSocket = webSocket;
    }

    public async Task<IReadOnlyList<HomeAssistantConfigEntry>> GetAllAsync(
        string? domain = null,
        CancellationToken cancellationToken = default)
    {
        var payload = domain is null
            ? null
            : new Dictionary<string, object?> { ["domain"] = Required(domain, nameof(domain)).Trim() };
        var result = await _webSocket.RequestAsync("config_entries/get", payload, cancellationToken).ConfigureAwait(false);
        return DecodeEntries(result, cancellationToken);
    }

    public async Task<HomeAssistantConfigEntry> GetAsync(
        string entryId,
        CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.RequestAsync(
            "config_entries/get_single",
            new Dictionary<string, object?> { ["entry_id"] = Required(entryId, nameof(entryId)) },
            cancellationToken).ConfigureAwait(false);
        if (result.ValueKind == JsonValueKind.Object
            && result.TryGetProperty("config_entry", out var entry))
        {
            return HomeAssistantJson.DeserializeResponse<HomeAssistantConfigEntry>(
                entry,
                "The Home Assistant configuration entry could not be decoded.",
                cancellationToken: cancellationToken);
        }

        throw new HomeAssistantProtocolException("The Home Assistant configuration-entry response had an unexpected shape.");
    }

    public Task<HomeAssistantIntegrationOperationResult> ReloadAsync(
        string entryId,
        CancellationToken cancellationToken = default)
    {
        return _rest.SendHomeAssistantAsync<HomeAssistantIntegrationOperationResult>(
            HttpMethod.Post,
            "api/config/config_entries/entry/" + Escape(entryId, nameof(entryId)) + "/reload",
            null,
            cancellationToken);
    }

    public async Task<HomeAssistantIntegrationOperationResult> SetEnabledAsync(
        string entryId,
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.RequestAsync(
            "config_entries/disable",
            new Dictionary<string, object?>
            {
                ["entry_id"] = Required(entryId, nameof(entryId)),
                ["disabled_by"] = enabled ? null : "user"
            },
            cancellationToken).ConfigureAwait(false);
        return HomeAssistantJson.DeserializeResponse<HomeAssistantIntegrationOperationResult>(
            result,
            "The Home Assistant configuration-entry operation could not be decoded.",
            cancellationToken: cancellationToken);
    }

    /// <summary>Starts Home Assistant's user-initiated reconfiguration flow for an existing entry.</summary>
    public Task<JsonElement> StartReconfigurationAsync(
        string domain,
        string entryId,
        CancellationToken cancellationToken = default)
    {
        return _rest.SendHomeAssistantAsync<JsonElement>(
            HttpMethod.Post,
            "api/config/config_entries/flow",
            new Dictionary<string, object?>
            {
                ["handler"] = Required(domain, nameof(domain)),
                ["entry_id"] = Required(entryId, nameof(entryId))
            },
            cancellationToken);
    }

    public Task<JsonElement> ContinueFlowAsync(
        string flowId,
        IReadOnlyDictionary<string, object?> input,
        CancellationToken cancellationToken = default)
    {
        if (input is null)
        {
            throw new ArgumentNullException(nameof(input));
        }

        return _rest.SendHomeAssistantAsync<JsonElement>(
            HttpMethod.Post,
            "api/config/config_entries/flow/" + Escape(flowId, nameof(flowId)),
            input,
            cancellationToken);
    }

    private static IReadOnlyList<HomeAssistantConfigEntry> DecodeEntries(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        var entries = value;
        if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("entries", out var nested))
        {
            entries = nested;
        }

        return HomeAssistantJson.DeserializeResponse<HomeAssistantConfigEntry[]>(
            entries,
            "The Home Assistant configuration entries could not be decoded.",
            cancellationToken: cancellationToken);
    }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty identifier is required.", parameterName)
            : value;
    }

    private static string Escape(string value, string parameterName)
    {
        return Uri.EscapeDataString(Required(value, parameterName));
    }
}
