using System.Text.Json;
using HomeAssistantX.Configuration;
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
    private readonly HomeAssistantClientOptions _options;

    internal HomeAssistantServiceClient(
        HomeAssistantRestClient rest,
        HomeAssistantWebSocketClient webSocket,
        HomeAssistantClientOptions options)
    {
        _rest = rest;
        _webSocket = webSocket;
        _options = options ?? throw new ArgumentNullException(nameof(options));
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

    /// <summary>Gets a flattened, typed action catalog while preserving every raw definition.</summary>
    public async Task<IReadOnlyList<HomeAssistantActionDefinition>> GetActionsAsync(
        CancellationToken cancellationToken = default)
    {
        var catalog = await GetCatalogWebSocketAsync(cancellationToken).ConfigureAwait(false);
        if (catalog.ValueKind != JsonValueKind.Object)
        {
            throw new Exceptions.HomeAssistantProtocolException("The Home Assistant action catalog was not an object.");
        }

        var actions = new List<HomeAssistantActionDefinition>();
        foreach (var domain in catalog.EnumerateObject())
        {
            if (domain.Value.ValueKind != JsonValueKind.Object)
            {
                throw new Exceptions.HomeAssistantProtocolException(
                    "The Home Assistant action catalog contained a non-object domain definition.");
            }

            foreach (var action in domain.Value.EnumerateObject())
            {
                if (action.Value.ValueKind != JsonValueKind.Object)
                {
                    throw new Exceptions.HomeAssistantProtocolException(
                        "The Home Assistant action catalog contained a non-object action definition.");
                }

                actions.Add(ParseAction(domain.Name, action.Name, action.Value));
            }
        }

        return actions
            .OrderBy(x => x.Domain, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Action, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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
            response.Context = HomeAssistantJson.DeserializeResponse<HomeAssistantContext>(
                context,
                "The Home Assistant service-call context could not be decoded.",
                cancellationToken: cancellationToken);
        }

        if (result.TryGetProperty("response", out var serviceResponse))
        {
            response.Response = await HomeAssistantJson.SnapshotResponseAsync(
                serviceResponse,
                "The Home Assistant service response could not be snapshotted.",
                cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return response;
    }

    public Task<HomeAssistantServiceCallResult> CallRestAsync(
        HomeAssistantServiceCall call,
        CancellationToken cancellationToken = default)
    {
        return _rest.CallServiceAsync(call, cancellationToken);
    }

    internal Task<HomeAssistantServiceCallResult> CallControlAsync(
        HomeAssistantServiceCall call,
        CancellationToken cancellationToken)
    {
        return CallControlAsync(call, CaptureControlTransport(), cancellationToken);
    }

    internal HomeAssistantServiceCallTransport CaptureControlTransport()
    {
        var transport = _options.ControlServiceCallTransport;
        return transport switch
        {
            HomeAssistantServiceCallTransport.WebSocket => transport,
            HomeAssistantServiceCallTransport.Rest => transport,
            _ => throw new InvalidOperationException("The configured typed-control transport is not supported.")
        };
    }

    internal Task<HomeAssistantServiceCallResult> CallControlAsync(
        HomeAssistantServiceCall call,
        HomeAssistantServiceCallTransport transport,
        CancellationToken cancellationToken)
    {
        return transport switch
        {
            HomeAssistantServiceCallTransport.WebSocket => CallAsync(call, cancellationToken),
            HomeAssistantServiceCallTransport.Rest => CallRestAsync(call, cancellationToken),
            _ => throw new InvalidOperationException("The configured typed-control transport is not supported.")
        };
    }

    private static HomeAssistantActionDefinition ParseAction(string domain, string action, JsonElement value)
    {
        var fields = new List<HomeAssistantActionFieldDefinition>();
        if (value.TryGetProperty("fields", out var rawFields) && rawFields.ValueKind == JsonValueKind.Object)
        {
            foreach (var field in rawFields.EnumerateObject())
            {
                fields.Add(ParseField(field.Name, field.Value));
            }
        }

        return new HomeAssistantActionDefinition
        {
            Domain = domain,
            Action = action,
            Name = GetString(value, "name") ?? action,
            Description = GetString(value, "description"),
            Fields = fields,
            Target = CloneProperty(value, "target"),
            Response = CloneProperty(value, "response"),
            Raw = value.Clone()
        };
    }

    private static HomeAssistantActionFieldDefinition ParseField(string field, JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return new HomeAssistantActionFieldDefinition
            {
                Field = field,
                Name = field,
                Raw = value.Clone()
            };
        }

        return new HomeAssistantActionFieldDefinition
        {
            Field = field,
            Name = GetString(value, "name") ?? field,
            Description = GetString(value, "description"),
            Required = GetBoolean(value, "required"),
            Advanced = GetBoolean(value, "advanced"),
            Default = CloneProperty(value, "default"),
            Example = CloneProperty(value, "example"),
            Selector = CloneProperty(value, "selector"),
            Raw = value.Clone()
        };
    }

    private static string? GetString(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static bool GetBoolean(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out var property)
            && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            && property.GetBoolean();
    }

    private static JsonElement? CloneProperty(JsonElement value, string name)
    {
        return value.TryGetProperty(name, out var property) ? property.Clone() : null;
    }
}
