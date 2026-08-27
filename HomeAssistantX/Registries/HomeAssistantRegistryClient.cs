using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Registries;

/// <summary>Loads Home Assistant area, floor, device, entity, and configuration-entry registries.</summary>
public sealed class HomeAssistantRegistryClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantRegistryClient(HomeAssistantWebSocketClient webSocket)
    {
        _webSocket = webSocket;
    }

    public async Task<HomeAssistantRegistrySnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var areasTask = _webSocket.RequestAsync("config/area_registry/list", null, cancellationToken);
        var floorsTask = _webSocket.RequestAsync("config/floor_registry/list", null, cancellationToken);
        var devicesTask = _webSocket.RequestAsync("config/device_registry/list", null, cancellationToken);
        var partialEntitiesTask = _webSocket.RequestAsync("config/entity_registry/list", null, cancellationToken);
        var configEntriesTask = GetConfigEntriesAsync(cancellationToken);
        await Task.WhenAll(areasTask, floorsTask, devicesTask, partialEntitiesTask, configEntriesTask).ConfigureAwait(false);

        var partialEntities = DeserializeArray<HomeAssistantEntityRegistryEntry>(
            await partialEntitiesTask.ConfigureAwait(false),
            "entity registry");
        var entities = partialEntities;
        if (partialEntities.Count > 0)
        {
            var extendedEntities = await _webSocket.RequestAsync(
                "config/entity_registry/get_entries",
                new Dictionary<string, object?>
                {
                    ["entity_ids"] = partialEntities.Select(entry => entry.EntityId).ToArray()
                },
                cancellationToken).ConfigureAwait(false);
            entities = DeserializeExtendedEntities(extendedEntities, partialEntities);
        }

        var configEntries = await configEntriesTask.ConfigureAwait(false);
        return new HomeAssistantRegistrySnapshot
        {
            Areas = DeserializeArray<HomeAssistantArea>(await areasTask.ConfigureAwait(false), "area registry"),
            Floors = DeserializeArray<HomeAssistantFloor>(await floorsTask.ConfigureAwait(false), "floor registry"),
            Devices = DeserializeArray<HomeAssistantDeviceRegistryEntry>(await devicesTask.ConfigureAwait(false), "device registry"),
            Entities = entities,
            ConfigEntries = configEntries.Entries,
            IsConfigEntryEnrichmentAvailable = configEntries.IsAvailable
        };
    }

    private async Task<ConfigEntryLoadResult> GetConfigEntriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var value = await _webSocket.RequestAsync("config_entries/get", null, cancellationToken).ConfigureAwait(false);
            return new ConfigEntryLoadResult(DeserializeConfigEntries(value), true);
        }
        catch (HomeAssistantCommandException exception)
            when (string.Equals(exception.Code, "unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return new ConfigEntryLoadResult(Array.Empty<HomeAssistantConfigEntry>(), false);
        }
    }

    private static IReadOnlyList<HomeAssistantEntityRegistryEntry> DeserializeExtendedEntities(
        JsonElement value,
        IReadOnlyList<HomeAssistantEntityRegistryEntry> partialEntries)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("The Home Assistant extended entity registry response had an unexpected shape.");
        }

        var extendedEntries = HomeAssistantJson.DeserializeResponse<Dictionary<string, HomeAssistantEntityRegistryEntry?>>(
            value,
            "The Home Assistant extended entity registry response could not be decoded.",
            allowNullCollectionEntries: true);

        return partialEntries.Select(partial =>
        {
            if (!extendedEntries.TryGetValue(partial.EntityId, out var extended) || extended is null)
            {
                return partial;
            }

            foreach (var pair in partial.AdditionalData)
            {
                if (!extended.AdditionalData.ContainsKey(pair.Key))
                {
                    extended.AdditionalData[pair.Key] = pair.Value;
                }
            }

            return extended;
        }).ToArray();
    }

    private static IReadOnlyList<T> DeserializeArray<T>(JsonElement value, string name)
    {
        return HomeAssistantJson.DeserializeResponse<T[]>(value, "The Home Assistant " + name + " response could not be decoded.");
    }

    private static IReadOnlyList<HomeAssistantConfigEntry> DeserializeConfigEntries(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return DeserializeArray<HomeAssistantConfigEntry>(value, "configuration-entry registry");
        }

        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("entries", out var entries))
        {
            return DeserializeArray<HomeAssistantConfigEntry>(entries, "configuration-entry registry");
        }

        throw new HomeAssistantProtocolException("The Home Assistant configuration-entry registry response had an unexpected shape.");
    }

    private sealed class ConfigEntryLoadResult
    {
        public ConfigEntryLoadResult(IReadOnlyList<HomeAssistantConfigEntry> entries, bool isAvailable)
        {
            Entries = entries;
            IsAvailable = isAvailable;
        }

        public IReadOnlyList<HomeAssistantConfigEntry> Entries { get; }

        public bool IsAvailable { get; }
    }
}
