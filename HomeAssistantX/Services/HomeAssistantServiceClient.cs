using System.Text.Json;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Services;

/// <summary>Discovers and invokes Home Assistant actions/services.</summary>
public sealed class HomeAssistantServiceClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantServiceClient(HomeAssistantRestClient rest, HomeAssistantWebSocketClient webSocket)
    {
        _rest = rest;
        _webSocket = webSocket;
    }

    public Task<JsonElement> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        return _rest.GetServicesAsync(cancellationToken);
    }

    /// <summary>Gets the service/action catalog through the WebSocket API.</summary>
    public Task<JsonElement> GetCatalogWebSocketAsync(CancellationToken cancellationToken = default)
    {
        return _webSocket.RequestAsync("get_services", null, cancellationToken);
    }

    public async Task<HomeAssistantServiceCallResult> CallAsync(
        HomeAssistantServiceCall call,
        CancellationToken cancellationToken = default)
    {
        if (call is null)
        {
            throw new ArgumentNullException(nameof(call));
        }

        var result = await _webSocket.RequestAsync("call_service", call.ToWebSocketPayload(), cancellationToken)
            .ConfigureAwait(false);
        var response = new HomeAssistantServiceCallResult();
        if (result.ValueKind != JsonValueKind.Object)
        {
            return response;
        }

        if (result.TryGetProperty("context", out var context))
        {
            response.Context = context.Deserialize<HomeAssistantContext>(HomeAssistantJson.SerializerOptions);
        }

        if (result.TryGetProperty("response", out var serviceResponse))
        {
            response.Response = serviceResponse.Clone();
        }

        return response;
    }

    public Task<HomeAssistantServiceCallResult> CallRestAsync(
        HomeAssistantServiceCall call,
        CancellationToken cancellationToken = default)
    {
        return _rest.CallServiceAsync(call, cancellationToken);
    }
}
