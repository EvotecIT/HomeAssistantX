using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Operations;

/// <summary>Reads bounded Core logs through the structured and legacy log APIs.</summary>
public sealed class HomeAssistantLogClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantLogClient(HomeAssistantRestClient rest, HomeAssistantWebSocketClient webSocket)
    {
        _rest = rest;
        _webSocket = webSocket;
    }

    /// <summary>Gets aggregated structured errors from the loaded <c>system_log</c> integration.</summary>
    public async Task<IReadOnlyList<HomeAssistantSystemLogEntry>> GetSystemLogAsync(
        CancellationToken cancellationToken = default)
    {
        var result = await _webSocket.RequestAsync("system_log/list", null, cancellationToken).ConfigureAwait(false);
        return HomeAssistantJson.DeserializeResponse<HomeAssistantSystemLogEntry[]>(result, "The Home Assistant system log could not be decoded.");
    }

    /// <summary>
    /// Gets the legacy Core error log. Some current installations return HTTP 404; callers should prefer
    /// <see cref="GetSystemLogAsync"/> or Supervisor Core logs when available.
    /// </summary>
    public Task<string> GetLegacyErrorLogAsync(CancellationToken cancellationToken = default)
    {
        return _rest.GetErrorLogAsync(cancellationToken);
    }
}
