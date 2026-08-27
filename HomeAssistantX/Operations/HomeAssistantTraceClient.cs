using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Operations;

/// <summary>Reads automation and script execution traces.</summary>
public sealed class HomeAssistantTraceClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantTraceClient(HomeAssistantWebSocketClient webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task<IReadOnlyList<HomeAssistantTraceSummary>> GetAllAsync(
        string domain,
        string itemId,
        CancellationToken cancellationToken = default)
    {
        var normalizedDomain = NormalizeDomain(domain);
        var result = await _webSocket.RequestAsync(
            "trace/list",
            new Dictionary<string, object?>
            {
                ["domain"] = normalizedDomain,
                ["item_id"] = Required(itemId, nameof(itemId))
            },
            cancellationToken).ConfigureAwait(false);
        return HomeAssistantJson.DeserializeResponse<HomeAssistantTraceSummary[]>(result, "The Home Assistant traces could not be decoded.");
    }

    public Task<JsonElement> GetAsync(
        string domain,
        string itemId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        var normalizedDomain = NormalizeDomain(domain);
        return _webSocket.RequestAsync(
            "trace/get",
            new Dictionary<string, object?>
            {
                ["domain"] = normalizedDomain,
                ["item_id"] = Required(itemId, nameof(itemId)),
                ["run_id"] = Required(runId, nameof(runId))
            },
            cancellationToken);
    }

    public Task<JsonElement> GetContextsAsync(
        string? domain = null,
        string? itemId = null,
        CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?>();
        if (domain is not null)
        {
            payload["domain"] = NormalizeDomain(domain);
        }

        if (itemId is not null)
        {
            payload["item_id"] = Required(itemId, nameof(itemId)).Trim();
        }

        return _webSocket.RequestAsync("trace/contexts", payload, cancellationToken);
    }

    private static string NormalizeDomain(string domain)
    {
        var normalized = domain?.Trim().ToLowerInvariant();
        if (normalized is not ("automation" or "script"))
        {
            throw new ArgumentOutOfRangeException(nameof(domain), "Trace domain must be 'automation' or 'script'.");
        }

        return normalized;
    }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty identifier is required.", parameterName)
            : value;
    }
}
