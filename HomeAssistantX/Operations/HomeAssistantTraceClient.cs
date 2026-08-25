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
        ValidateDomain(domain);
        var result = await _webSocket.RequestAsync(
            "trace/list",
            new Dictionary<string, object?>
            {
                ["domain"] = domain,
                ["item_id"] = Required(itemId, nameof(itemId))
            },
            cancellationToken).ConfigureAwait(false);
        return result.Deserialize<HomeAssistantTraceSummary[]>(HomeAssistantJson.SerializerOptions)
            ?? throw new HomeAssistantProtocolException("The Home Assistant traces could not be decoded.");
    }

    public Task<JsonElement> GetAsync(
        string domain,
        string itemId,
        string runId,
        CancellationToken cancellationToken = default)
    {
        ValidateDomain(domain);
        return _webSocket.RequestAsync(
            "trace/get",
            new Dictionary<string, object?>
            {
                ["domain"] = domain,
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
        if (!string.IsNullOrWhiteSpace(domain))
        {
            ValidateDomain(domain!);
            payload["domain"] = domain;
        }

        if (!string.IsNullOrWhiteSpace(itemId))
        {
            payload["item_id"] = itemId;
        }

        return _webSocket.RequestAsync("trace/contexts", payload, cancellationToken);
    }

    private static void ValidateDomain(string domain)
    {
        if (!string.Equals(domain, "automation", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(domain, "script", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentOutOfRangeException(nameof(domain), "Trace domain must be 'automation' or 'script'.");
        }
    }

    private static string Required(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A non-empty identifier is required.", parameterName)
            : value;
    }
}
