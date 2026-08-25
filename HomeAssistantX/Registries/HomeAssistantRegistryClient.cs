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
        var entitiesTask = _webSocket.RequestAsync("config/entity_registry/list", null, cancellationToken);
        var configEntriesTask = _webSocket.RequestAsync("config_entries/get", null, cancellationToken);
        await Task.WhenAll(areasTask, floorsTask, devicesTask, entitiesTask, configEntriesTask).ConfigureAwait(false);

        return new HomeAssistantRegistrySnapshot
        {
            Areas = DeserializeArray<HomeAssistantArea>(await areasTask.ConfigureAwait(false), "area registry"),
            Floors = DeserializeArray<HomeAssistantFloor>(await floorsTask.ConfigureAwait(false), "floor registry"),
            Devices = DeserializeArray<HomeAssistantDeviceRegistryEntry>(await devicesTask.ConfigureAwait(false), "device registry"),
            Entities = DeserializeArray<HomeAssistantEntityRegistryEntry>(await entitiesTask.ConfigureAwait(false), "entity registry"),
            ConfigEntries = DeserializeConfigEntries(await configEntriesTask.ConfigureAwait(false))
        };
    }

    private static IReadOnlyList<T> DeserializeArray<T>(JsonElement value, string name)
    {
        return value.Deserialize<T[]>(HomeAssistantJson.SerializerOptions)
            ?? throw new HomeAssistantProtocolException("The Home Assistant " + name + " response could not be decoded.");
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
}
