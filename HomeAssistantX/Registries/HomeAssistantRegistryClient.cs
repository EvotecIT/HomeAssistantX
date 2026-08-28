using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Registries;

/// <summary>Loads and manages Home Assistant registries.</summary>
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
        var labelsTask = GetLabelsForSnapshotAsync(cancellationToken);
        await Task.WhenAll(areasTask, floorsTask, devicesTask, partialEntitiesTask, configEntriesTask, labelsTask).ConfigureAwait(false);

        var partialEntities = DeserializeArray<HomeAssistantEntityRegistryEntry>(
            await partialEntitiesTask.ConfigureAwait(false),
            "entity registry",
            cancellationToken);
        ValidateAssignmentCollections(partialEntities, cancellationToken);
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
            entities = DeserializeExtendedEntities(extendedEntities, partialEntities, cancellationToken);
            ValidateAssignmentCollections(entities, cancellationToken);
        }

        var configEntries = await configEntriesTask.ConfigureAwait(false);
        var areas = DeserializeArray<HomeAssistantArea>(await areasTask.ConfigureAwait(false), "area registry", cancellationToken);
        var floors = DeserializeArray<HomeAssistantFloor>(await floorsTask.ConfigureAwait(false), "floor registry", cancellationToken);
        var devices = DeserializeArray<HomeAssistantDeviceRegistryEntry>(await devicesTask.ConfigureAwait(false), "device registry", cancellationToken);
        ValidateAssignmentCollections(areas, cancellationToken);
        ValidateAssignmentCollections(floors, cancellationToken);
        ValidateAssignmentCollections(devices, cancellationToken);
        return new HomeAssistantRegistrySnapshot
        {
            Areas = areas,
            Floors = floors,
            Devices = devices,
            Entities = entities,
            ConfigEntries = configEntries.Entries,
            Labels = (await labelsTask.ConfigureAwait(false)).Entries,
            IsConfigEntryEnrichmentAvailable = configEntries.IsAvailable,
            IsLabelRegistryAvailable = (await labelsTask.ConfigureAwait(false)).IsAvailable
        };
    }

    public async Task<IReadOnlyList<HomeAssistantLabel>> GetLabelsAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("config/label_registry/list", null, cancellationToken).ConfigureAwait(false);
        var labels = DeserializeArray<HomeAssistantLabel>(value, "label registry", cancellationToken);
        ValidateLabels(labels, cancellationToken);
        return labels;
    }

    public async Task<HomeAssistantLabel> CreateLabelAsync(
        HomeAssistantLabelCreate label,
        CancellationToken cancellationToken = default)
    {
        if (label is null)
        {
            throw new ArgumentNullException(nameof(label));
        }

        var payload = new Dictionary<string, object?> { ["name"] = label.Name };
        AddOptional(payload, "color", label.Color);
        AddOptional(payload, "description", label.Description);
        AddOptional(payload, "icon", label.Icon);
        var created = DeserializeObject<HomeAssistantLabel>(
            await _webSocket.RequestAsync("config/label_registry/create", payload, cancellationToken).ConfigureAwait(false),
            "created label",
            cancellationToken);
        ValidateLabel(created, cancellationToken);
        return created;
    }

    public async Task<HomeAssistantLabel> UpdateLabelAsync(
        string labelId,
        HomeAssistantLabelUpdate update,
        CancellationToken cancellationToken = default)
    {
        labelId = HomeAssistantRegistryValidation.Require(labelId, nameof(labelId));
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var payload = BeginUpdate("label_id", labelId, update.GetChanges(), nameof(update));
        var updated = DeserializeObject<HomeAssistantLabel>(
            await _webSocket.RequestAsync("config/label_registry/update", payload, cancellationToken).ConfigureAwait(false),
            "updated label",
            cancellationToken);
        ValidateLabel(updated, cancellationToken);
        if (!string.Equals(updated.LabelId, labelId, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("The updated Home Assistant label did not match the requested identifier.");
        return updated;
    }

    public Task DeleteLabelAsync(string labelId, CancellationToken cancellationToken = default)
    {
        labelId = HomeAssistantRegistryValidation.Require(labelId, nameof(labelId));
        return IgnoreResultAsync("config/label_registry/delete", new Dictionary<string, object?> { ["label_id"] = labelId }, cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantCategory>> GetCategoriesAsync(
        string scope,
        CancellationToken cancellationToken = default)
    {
        scope = HomeAssistantRegistryValidation.Require(scope, nameof(scope));
        var value = await _webSocket.RequestAsync("config/category_registry/list", new Dictionary<string, object?>
        {
            ["scope"] = scope
        }, cancellationToken).ConfigureAwait(false);
        var categories = DeserializeArray<HomeAssistantCategory>(value, "category registry", cancellationToken);
        ValidateCategories(categories, cancellationToken);
        return categories;
    }

    public async Task<HomeAssistantCategory> CreateCategoryAsync(
        string scope,
        HomeAssistantCategoryCreate category,
        CancellationToken cancellationToken = default)
    {
        scope = HomeAssistantRegistryValidation.Require(scope, nameof(scope));
        if (category is null)
        {
            throw new ArgumentNullException(nameof(category));
        }

        var payload = new Dictionary<string, object?> { ["scope"] = scope, ["name"] = category.Name };
        AddOptional(payload, "icon", category.Icon);
        var created = DeserializeObject<HomeAssistantCategory>(
            await _webSocket.RequestAsync("config/category_registry/create", payload, cancellationToken).ConfigureAwait(false),
            "created category",
            cancellationToken);
        ValidateCategory(created, cancellationToken);
        return created;
    }

    public async Task<HomeAssistantCategory> UpdateCategoryAsync(
        string scope,
        string categoryId,
        HomeAssistantCategoryUpdate update,
        CancellationToken cancellationToken = default)
    {
        scope = HomeAssistantRegistryValidation.Require(scope, nameof(scope));
        categoryId = HomeAssistantRegistryValidation.Require(categoryId, nameof(categoryId));
        if (update is null)
        {
            throw new ArgumentNullException(nameof(update));
        }

        var payload = BeginUpdate("category_id", categoryId, update.GetChanges(), nameof(update));
        payload["scope"] = scope;
        var updated = DeserializeObject<HomeAssistantCategory>(
            await _webSocket.RequestAsync("config/category_registry/update", payload, cancellationToken).ConfigureAwait(false),
            "updated category",
            cancellationToken);
        ValidateCategory(updated, cancellationToken);
        if (!string.Equals(updated.CategoryId, categoryId, StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("The updated Home Assistant category did not match the requested identifier.");
        return updated;
    }

    public Task DeleteCategoryAsync(string scope, string categoryId, CancellationToken cancellationToken = default)
    {
        scope = HomeAssistantRegistryValidation.Require(scope, nameof(scope));
        categoryId = HomeAssistantRegistryValidation.Require(categoryId, nameof(categoryId));
        return IgnoreResultAsync("config/category_registry/delete", new Dictionary<string, object?>
        {
            ["scope"] = scope,
            ["category_id"] = categoryId
        }, cancellationToken);
    }

    private async Task<ConfigEntryLoadResult> GetConfigEntriesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var value = await _webSocket.RequestAsync("config_entries/get", null, cancellationToken).ConfigureAwait(false);
            return new ConfigEntryLoadResult(DeserializeConfigEntries(value, cancellationToken), true);
        }
        catch (HomeAssistantCommandException exception)
            when (string.Equals(exception.Code, "unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return new ConfigEntryLoadResult(Array.Empty<HomeAssistantConfigEntry>(), false);
        }
    }

    private async Task<LabelLoadResult> GetLabelsForSnapshotAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new LabelLoadResult(await GetLabelsAsync(cancellationToken).ConfigureAwait(false), true);
        }
        catch (HomeAssistantCommandException exception)
            when (string.Equals(exception.Code, "unknown_command", StringComparison.OrdinalIgnoreCase)
                || string.Equals(exception.Code, "unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return new LabelLoadResult(Array.Empty<HomeAssistantLabel>(), false);
        }
    }

    internal static IReadOnlyList<HomeAssistantEntityRegistryEntry> DeserializeExtendedEntities(
        JsonElement value,
        IReadOnlyList<HomeAssistantEntityRegistryEntry> partialEntries,
        CancellationToken cancellationToken)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("The Home Assistant extended entity registry response had an unexpected shape.");
        }

        var extendedEntries = HomeAssistantJson.DeserializeResponse<Dictionary<string, HomeAssistantEntityRegistryEntry?>>(
            value,
            "The Home Assistant extended entity registry response could not be decoded.",
            allowNullCollectionEntries: true,
            cancellationToken: cancellationToken);

        var merged = new List<HomeAssistantEntityRegistryEntry>(partialEntries.Count);
        foreach (var partial in partialEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!extendedEntries.TryGetValue(partial.EntityId, out var extended) || extended is null)
            {
                merged.Add(partial);
                continue;
            }

            foreach (var pair in partial.AdditionalData)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!extended.AdditionalData.ContainsKey(pair.Key))
                {
                    extended.AdditionalData[pair.Key] = pair.Value;
                }
            }

            merged.Add(extended);
        }

        return merged;
    }

    private static IReadOnlyList<T> DeserializeArray<T>(
        JsonElement value,
        string name,
        CancellationToken cancellationToken)
    {
        return HomeAssistantJson.DeserializeResponse<T[]>(
            value,
            "The Home Assistant " + name + " response could not be decoded.",
            cancellationToken: cancellationToken);
    }

    internal static void ValidateAssignmentCollections(
        IEnumerable<HomeAssistantArea> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireAssignmentCollection(entry.Aliases, "area aliases", cancellationToken);
            RequireIdentifierAssignmentCollection(entry.Labels, "area labels", cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void ValidateAssignmentCollections(
        IEnumerable<HomeAssistantFloor> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireAssignmentCollection(entry.Aliases, "floor aliases", cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void ValidateAssignmentCollections(
        IEnumerable<HomeAssistantDeviceRegistryEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireIdentifierAssignmentCollection(entry.ConfigEntries, "device configuration-entry assignments", cancellationToken);
            RequireIdentifierAssignmentCollection(entry.Labels, "device label assignments", cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static void ValidateAssignmentCollections(
        IEnumerable<HomeAssistantEntityRegistryEntry> entries,
        CancellationToken cancellationToken)
    {
        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RequireAssignmentCollection(entry.Aliases, "entity aliases", cancellationToken, allowNullEntries: true);
            RequireIdentifierAssignmentCollection(entry.Labels, "entity label assignments", cancellationToken);
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static void RequireAssignmentCollection<T>(
        IEnumerable<T>? values,
        string responseName,
        CancellationToken cancellationToken,
        bool allowNullEntries = false)
    {
        if (values is null)
        {
            throw new HomeAssistantProtocolException("The Home Assistant " + responseName + " were null.");
        }
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!allowNullEntries && value is null)
            {
                throw new HomeAssistantProtocolException("The Home Assistant " + responseName + " contained a null value.");
            }
        }
    }

    private static void RequireIdentifierAssignmentCollection(
        IEnumerable<string>? values,
        string responseName,
        CancellationToken cancellationToken)
    {
        if (values is null)
        {
            throw new HomeAssistantProtocolException("The Home Assistant " + responseName + " were null.");
        }

        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(value)
                || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
                || !identities.Add(value))
            {
                throw new HomeAssistantProtocolException(
                    "The Home Assistant " + responseName + " contained an invalid or duplicate identifier.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private static T DeserializeObject<T>(
        JsonElement value,
        string name,
        CancellationToken cancellationToken)
    {
        return HomeAssistantJson.DeserializeResponse<T>(
            value,
            "The Home Assistant " + name + " response could not be decoded.",
            cancellationToken: cancellationToken);
    }

    private static void ValidateLabel(HomeAssistantLabel label, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(label.LabelId)
            || !string.Equals(label.LabelId, label.LabelId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(label.Name)
            || !string.Equals(label.Name, label.Name.Trim(), StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A Home Assistant label did not contain its required identifier and name.");
    }

    private static void ValidateCategory(HomeAssistantCategory category, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(category.CategoryId)
            || !string.Equals(category.CategoryId, category.CategoryId.Trim(), StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(category.Name)
            || !string.Equals(category.Name, category.Name.Trim(), StringComparison.Ordinal))
            throw new HomeAssistantProtocolException("A Home Assistant category did not contain its required identifier and name.");
    }

    internal static void ValidateLabels(
        IEnumerable<HomeAssistantLabel> labels,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identities = new List<string>();
        foreach (var label in labels)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateLabel(label, cancellationToken);
            identities.Add(label.LabelId);
        }
        RequireUniqueIdentities(identities, "label registry", cancellationToken);
    }

    internal static void ValidateCategories(
        IEnumerable<HomeAssistantCategory> categories,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var identities = new List<string>();
        foreach (var category in categories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateCategory(category, cancellationToken);
            identities.Add(category.CategoryId);
        }
        RequireUniqueIdentities(identities, "category registry", cancellationToken);
    }

    private static void RequireUniqueIdentities(
        IEnumerable<string> identities,
        string registryName,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var identity in identities)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!seen.Add(identity))
                throw new HomeAssistantProtocolException("The Home Assistant " + registryName + " response contained a duplicate identifier.");
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private async Task IgnoreResultAsync(
        string command,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        await _webSocket.RequestAsync(command, payload, cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<string, object?> BeginUpdate(
        string idName,
        string id,
        IReadOnlyDictionary<string, object?> changes,
        string parameterName)
    {
        if (changes.Count == 0)
        {
            throw new ArgumentException("At least one registry field must be changed.", parameterName);
        }

        var payload = new Dictionary<string, object?> { [idName] = id };
        foreach (var pair in changes)
        {
            payload[pair.Key] = pair.Value;
        }

        return payload;
    }

    private static void AddOptional(IDictionary<string, object?> payload, string name, string? value)
    {
        if (value is not null)
        {
            payload[name] = value;
        }
    }

    private static IReadOnlyList<HomeAssistantConfigEntry> DeserializeConfigEntries(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return DeserializeArray<HomeAssistantConfigEntry>(value, "configuration-entry registry", cancellationToken);
        }

        if (value.ValueKind == JsonValueKind.Object
            && value.TryGetProperty("entries", out var entries))
        {
            return DeserializeArray<HomeAssistantConfigEntry>(entries, "configuration-entry registry", cancellationToken);
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

    private sealed class LabelLoadResult
    {
        internal LabelLoadResult(IReadOnlyList<HomeAssistantLabel> entries, bool isAvailable)
        {
            Entries = entries;
            IsAvailable = isAvailable;
        }

        internal IReadOnlyList<HomeAssistantLabel> Entries { get; }

        internal bool IsAvailable { get; }
    }
}
