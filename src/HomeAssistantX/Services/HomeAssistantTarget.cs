using System.Text.Json.Serialization;

namespace HomeAssistantX.Services;

/// <summary>Identifies Home Assistant entities, devices, areas, floors, or labels targeted by an action.</summary>
public sealed class HomeAssistantTarget
{
    [JsonPropertyName("entity_id")]
    public IReadOnlyList<string>? EntityIds { get; set; }

    [JsonPropertyName("device_id")]
    public IReadOnlyList<string>? DeviceIds { get; set; }

    [JsonPropertyName("area_id")]
    public IReadOnlyList<string>? AreaIds { get; set; }

    [JsonPropertyName("floor_id")]
    public IReadOnlyList<string>? FloorIds { get; set; }

    [JsonPropertyName("label_id")]
    public IReadOnlyList<string>? LabelIds { get; set; }

    public static HomeAssistantTarget ForEntity(params string[] entityIds)
    {
        return new HomeAssistantTarget().WithEntities(entityIds);
    }

    public static HomeAssistantTarget ForDevice(params string[] deviceIds)
    {
        return new HomeAssistantTarget().WithDevices(deviceIds);
    }

    public static HomeAssistantTarget ForArea(params string[] areaIds)
    {
        return new HomeAssistantTarget().WithAreas(areaIds);
    }

    public static HomeAssistantTarget Create()
    {
        return new HomeAssistantTarget();
    }

    public HomeAssistantTarget WithEntities(params string[] entityIds)
    {
        EntityIds = ValidateIds(entityIds, nameof(entityIds));
        return this;
    }

    public HomeAssistantTarget WithDevices(params string[] deviceIds)
    {
        DeviceIds = ValidateIds(deviceIds, nameof(deviceIds));
        return this;
    }

    public HomeAssistantTarget WithAreas(params string[] areaIds)
    {
        AreaIds = ValidateIds(areaIds, nameof(areaIds));
        return this;
    }

    public HomeAssistantTarget WithFloors(params string[] floorIds)
    {
        FloorIds = ValidateIds(floorIds, nameof(floorIds));
        return this;
    }

    public HomeAssistantTarget WithLabels(params string[] labelIds)
    {
        LabelIds = ValidateIds(labelIds, nameof(labelIds));
        return this;
    }

    private static IReadOnlyList<string> ValidateIds(string[] ids, string parameterName)
    {
        if (ids is null || ids.Length == 0 || ids.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty identifier is required.", parameterName);
        }

        return ids.ToArray();
    }
}
