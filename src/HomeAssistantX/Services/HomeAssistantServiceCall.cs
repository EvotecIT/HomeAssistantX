using System.Text.Json;

namespace HomeAssistantX.Services;

/// <summary>A typed request model for a Home Assistant action/service call.</summary>
public sealed class HomeAssistantServiceCall
{
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);

    public HomeAssistantServiceCall(string domain, string service)
    {
        Domain = ValidateName(domain, nameof(domain));
        Service = ValidateName(service, nameof(service));
    }

    public string Domain { get; }

    public string Service { get; }

    public HomeAssistantTarget? Target { get; private set; }

    public IReadOnlyDictionary<string, object?> Data => _data;

    public bool ReturnResponse { get; private set; }

    public static HomeAssistantServiceCall Create(string domain, string service)
    {
        return new HomeAssistantServiceCall(domain, service);
    }

    public HomeAssistantServiceCall ForTarget(HomeAssistantTarget target)
    {
        Target = target ?? throw new ArgumentNullException(nameof(target));
        return this;
    }

    public HomeAssistantServiceCall ForEntity(params string[] entityIds)
    {
        Target ??= HomeAssistantTarget.Create();
        Target.WithEntities(entityIds);
        return this;
    }

    public HomeAssistantServiceCall ForDevice(params string[] deviceIds)
    {
        Target ??= HomeAssistantTarget.Create();
        Target.WithDevices(deviceIds);
        return this;
    }

    public HomeAssistantServiceCall ForArea(params string[] areaIds)
    {
        Target ??= HomeAssistantTarget.Create();
        Target.WithAreas(areaIds);
        return this;
    }

    public HomeAssistantServiceCall ForFloor(params string[] floorIds)
    {
        Target ??= HomeAssistantTarget.Create();
        Target.WithFloors(floorIds);
        return this;
    }

    public HomeAssistantServiceCall ForLabel(params string[] labelIds)
    {
        Target ??= HomeAssistantTarget.Create();
        Target.WithLabels(labelIds);
        return this;
    }

    public HomeAssistantServiceCall WithData(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("A service data name is required.", nameof(name));
        }

        _data[name] = value;
        return this;
    }

    public HomeAssistantServiceCall WithResponse(bool enabled = true)
    {
        ReturnResponse = enabled;
        return this;
    }

    internal Dictionary<string, object?> ToRestPayload()
    {
        var payload = new Dictionary<string, object?>(_data, StringComparer.Ordinal);
        if (Target?.EntityIds is { Count: > 0 })
        {
            payload["entity_id"] = Target.EntityIds.Count == 1 ? Target.EntityIds[0] : Target.EntityIds;
        }

        if (Target?.DeviceIds is { Count: > 0 })
        {
            payload["device_id"] = Target.DeviceIds.Count == 1 ? Target.DeviceIds[0] : Target.DeviceIds;
        }

        if (Target?.AreaIds is { Count: > 0 })
        {
            payload["area_id"] = Target.AreaIds.Count == 1 ? Target.AreaIds[0] : Target.AreaIds;
        }

        if (Target?.FloorIds is { Count: > 0 })
        {
            payload["floor_id"] = Target.FloorIds.Count == 1 ? Target.FloorIds[0] : Target.FloorIds;
        }

        if (Target?.LabelIds is { Count: > 0 })
        {
            payload["label_id"] = Target.LabelIds.Count == 1 ? Target.LabelIds[0] : Target.LabelIds;
        }

        return payload;
    }

    internal Dictionary<string, object?> ToWebSocketPayload()
    {
        return new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["domain"] = Domain,
            ["service"] = Service,
            ["service_data"] = _data.Count == 0 ? null : _data,
            ["target"] = Target,
            ["return_response"] = ReturnResponse ? true : null
        };
    }

    private static string ValidateName(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty name is required.", parameterName);
        }

        return value.Trim();
    }
}

/// <summary>Contains the state changes and optional response data produced by an action.</summary>
public sealed class HomeAssistantServiceCallResult
{
    public IReadOnlyList<Models.HomeAssistantState> ChangedStates { get; set; } = Array.Empty<Models.HomeAssistantState>();

    public JsonElement? Response { get; set; }

    public Models.HomeAssistantContext? Context { get; set; }
}
