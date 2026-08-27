using System.Text.Json.Serialization;

using HomeAssistantX.Models;

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
        EntityIds = NormalizeEntityIds(entityIds, nameof(entityIds));
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

    internal HomeAssistantTarget Normalize()
    {
        return new HomeAssistantTarget
        {
            EntityIds = EntityIds is null ? null : NormalizeEntityIds(EntityIds, nameof(EntityIds)),
            DeviceIds = NormalizeIds(DeviceIds, nameof(DeviceIds)),
            AreaIds = NormalizeIds(AreaIds, nameof(AreaIds)),
            FloorIds = NormalizeIds(FloorIds, nameof(FloorIds)),
            LabelIds = NormalizeIds(LabelIds, nameof(LabelIds))
        };
    }

    internal HomeAssistantTarget NormalizeForDomain(string domain)
    {
        var normalized = Normalize();
        if (normalized.EntityIds is not null)
        {
            var entityIds = new string[normalized.EntityIds.Count];
            for (var index = 0; index < normalized.EntityIds.Count; index++)
            {
                if (!HomeAssistantEntityId.TryNormalizeForDomain(normalized.EntityIds[index], domain, out entityIds[index]))
                {
                    throw new ArgumentException($"Target entity identifiers must belong to the '{domain}' domain.", nameof(EntityIds));
                }
            }

            normalized.EntityIds = entityIds;
        }

        return normalized;
    }

    private static IReadOnlyList<string> ValidateIds(string[] ids, string parameterName)
    {
        if (ids is null || ids.Length == 0 || ids.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("At least one non-empty identifier is required.", parameterName);
        }

        return ids.Select(id => id.Trim()).ToArray();
    }

    private static IReadOnlyList<string>? NormalizeIds(IReadOnlyList<string>? ids, string parameterName)
    {
        if (ids is null)
        {
            return null;
        }

        if (ids.Count == 0 || ids.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Target identifiers cannot be empty.", parameterName);
        }

        return ids.Select(id => id.Trim()).ToArray();
    }

    private static IReadOnlyList<string> NormalizeEntityIds(IReadOnlyList<string> ids, string parameterName)
    {
        if (ids is null || ids.Count == 0)
        {
            throw new ArgumentException("At least one entity identifier is required.", parameterName);
        }

        var normalized = new List<string>(ids.Count);
        foreach (var value in ids)
        {
            if (!HomeAssistantEntityId.TryNormalize(value, out var entityId))
            {
                throw new ArgumentException(
                    "Entity identifiers must use the lowercase native Home Assistant format.",
                    parameterName);
            }

            if (!normalized.Contains(entityId, StringComparer.Ordinal))
            {
                normalized.Add(entityId);
            }
        }

        return normalized;
    }
}
