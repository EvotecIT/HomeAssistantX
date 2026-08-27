using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Media;

/// <summary>Browses and resolves global media sources and media exposed by individual players.</summary>
public sealed class HomeAssistantMediaBrowserClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantMediaBrowserClient(HomeAssistantWebSocketClient webSocket) => _webSocket = webSocket;

    public Task<HomeAssistantMediaItem> BrowseSourcesAsync(string? mediaContentId = null, CancellationToken cancellationToken = default)
        => RequestItemAsync("media_source/browse_media", OptionalContentId(mediaContentId), cancellationToken);

    public async Task<IReadOnlyList<HomeAssistantMediaItem>> SearchSourcesAsync(string searchQuery, string? mediaContentId = null, IReadOnlyCollection<string>? mediaClasses = null, CancellationToken cancellationToken = default)
        => (await SearchSourcesResponseAsync(searchQuery, mediaContentId, mediaClasses, cancellationToken).ConfigureAwait(false)).Items;

    /// <summary>Searches global media sources and preserves response-level provider metadata.</summary>
    public Task<HomeAssistantMediaSearchResponse> SearchSourcesResponseAsync(string searchQuery, string? mediaContentId = null, IReadOnlyCollection<string>? mediaClasses = null, CancellationToken cancellationToken = default)
    {
        var payload = OptionalContentId(mediaContentId);
        payload["search_query"] = Require(searchQuery, nameof(searchQuery));
        AddMediaClasses(payload, mediaClasses);
        return RequestSearchAsync("media_source/search_media", payload, cancellationToken);
    }

    public async Task<HomeAssistantResolvedMedia> ResolveAsync(string mediaContentId, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?> { ["media_content_id"] = Require(mediaContentId, nameof(mediaContentId)) };
        if (expiration.HasValue)
        {
            if (expiration.Value <= TimeSpan.Zero || expiration.Value.TotalSeconds > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(expiration));
            payload["expires"] = (int)Math.Ceiling(expiration.Value.TotalSeconds);
        }
        var value = await _webSocket.RequestAsync("media_source/resolve_media", payload, cancellationToken).ConfigureAwait(false);
        var result = HomeAssistantJson.DeserializeResponse<HomeAssistantResolvedMedia>(value, "The resolved media response could not be decoded.");
        if (string.IsNullOrWhiteSpace(result.Url)) throw new HomeAssistantProtocolException("The resolved media response omitted its URL.");
        return result;
    }

    public Task<HomeAssistantMediaItem> BrowsePlayerAsync(string entityId, string? mediaContentType = null, string? mediaContentId = null, CancellationToken cancellationToken = default)
    {
        var payload = PlayerPayload(entityId, mediaContentType, mediaContentId);
        return RequestItemAsync("media_player/browse_media", payload, cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantMediaItem>> SearchPlayerAsync(string entityId, string searchQuery, string? mediaContentType = null, string? mediaContentId = null, IReadOnlyCollection<string>? mediaClasses = null, CancellationToken cancellationToken = default)
        => (await SearchPlayerResponseAsync(entityId, searchQuery, mediaContentType, mediaContentId, mediaClasses, cancellationToken).ConfigureAwait(false)).Items;

    /// <summary>Searches media exposed by a player and preserves response-level provider metadata.</summary>
    public Task<HomeAssistantMediaSearchResponse> SearchPlayerResponseAsync(string entityId, string searchQuery, string? mediaContentType = null, string? mediaContentId = null, IReadOnlyCollection<string>? mediaClasses = null, CancellationToken cancellationToken = default)
    {
        var payload = PlayerPayload(entityId, mediaContentType, mediaContentId);
        payload["search_query"] = Require(searchQuery, nameof(searchQuery));
        AddMediaClasses(payload, mediaClasses);
        return RequestSearchAsync("media_player/search_media", payload, cancellationToken);
    }

    private async Task<HomeAssistantMediaItem> RequestItemAsync(string command, IReadOnlyDictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        var value = await _webSocket.RequestAsync(command, payload, cancellationToken).ConfigureAwait(false);
        return DecodeItem(value);
    }

    private async Task<HomeAssistantMediaSearchResponse> RequestSearchAsync(string command, IReadOnlyDictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        var value = await _webSocket.RequestAsync(command, payload, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException("The media search response had an unexpected shape.");
        var response = HomeAssistantJson.DeserializeResponse<HomeAssistantMediaSearchResponse>(value, "The media search response could not be decoded.");
        if (response.Items is null)
            throw new HomeAssistantProtocolException("The media search response contained a null result.");
        HomeAssistantJson.RequireNoNullCollectionEntries(response.Items, "The media search response contained a null result.");
        foreach (var item in response.Items) ValidateItemCollections(item);
        return response;
    }

    private static HomeAssistantMediaItem DecodeItem(JsonElement value)
    {
        var result = HomeAssistantJson.DeserializeResponse<HomeAssistantMediaItem>(value, "The media browse response could not be decoded.");
        ValidateItemCollections(result);
        return result;
    }

    private static void ValidateItemCollections(HomeAssistantMediaItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Title))
            throw new HomeAssistantProtocolException("The media response omitted an item title.");
        if (item.Children is null)
            throw new HomeAssistantProtocolException("The media response contained a null children collection.");
        HomeAssistantJson.RequireNoNullCollectionEntries(item.Children, "The media response contained a null child.");
        if (item.SearchMediaClasses is not null)
            HomeAssistantJson.RequireNoNullCollectionEntries(item.SearchMediaClasses, "The media response contained a null search media class.");
        foreach (var child in item.Children) ValidateItemCollections(child);
    }

    private static Dictionary<string, object?> PlayerPayload(string entityId, string? mediaContentType, string? mediaContentId)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "media_player", out entityId))
            throw new ArgumentException("A media-player entity identifier is required.", nameof(entityId));
        var payload = new Dictionary<string, object?> { ["entity_id"] = entityId };
        if (mediaContentType is not null)
        {
            payload["media_content_type"] = Require(mediaContentType, nameof(mediaContentType));
        }
        if (mediaContentId is not null)
        {
            payload["media_content_id"] = Require(mediaContentId, nameof(mediaContentId));
        }
        return payload;
    }

    private static Dictionary<string, object?> OptionalContentId(string? mediaContentId)
    {
        var payload = new Dictionary<string, object?>();
        if (mediaContentId is not null) payload["media_content_id"] = Require(mediaContentId, nameof(mediaContentId));
        return payload;
    }

    private static void AddMediaClasses(IDictionary<string, object?> payload, IReadOnlyCollection<string>? mediaClasses)
    {
        if (mediaClasses is null) return;
        if (mediaClasses.Count == 0 || mediaClasses.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("Media classes cannot be empty.", nameof(mediaClasses));
        payload["media_filter_classes"] = mediaClasses.Select(item => item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
        return value.Trim();
    }
}
