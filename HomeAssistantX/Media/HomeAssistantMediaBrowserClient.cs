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
        cancellationToken.ThrowIfCancellationRequested();
        var payload = OptionalContentId(mediaContentId);
        payload["search_query"] = Require(searchQuery, nameof(searchQuery));
        AddMediaClasses(payload, mediaClasses, cancellationToken);
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
        RequireNoDuplicateProperties(value, "The resolved media response contained duplicate JSON properties.", cancellationToken);
        var result = await HomeAssistantJson.DeserializeResponseAsync<HomeAssistantResolvedMedia>(
            value,
            "The resolved media response could not be decoded.",
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsValidResolvedUrl(result.Url))
            throw new HomeAssistantProtocolException("The resolved media response contained an invalid URL.");
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
        cancellationToken.ThrowIfCancellationRequested();
        var payload = PlayerPayload(entityId, mediaContentType, mediaContentId);
        payload["search_query"] = Require(searchQuery, nameof(searchQuery));
        AddMediaClasses(payload, mediaClasses, cancellationToken);
        return RequestSearchAsync("media_player/search_media", payload, cancellationToken);
    }

    private async Task<HomeAssistantMediaItem> RequestItemAsync(string command, IReadOnlyDictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        var value = await _webSocket.RequestAsync(command, payload, cancellationToken).ConfigureAwait(false);
        return await DecodeItemAsync(value, cancellationToken).ConfigureAwait(false);
    }

    private async Task<HomeAssistantMediaSearchResponse> RequestSearchAsync(string command, IReadOnlyDictionary<string, object?> payload, CancellationToken cancellationToken)
    {
        var value = await _webSocket.RequestAsync(command, payload, cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        RequireNoDuplicateProperties(value, "The media search response contained duplicate JSON properties.", cancellationToken);
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException("The media search response had an unexpected shape.");
        foreach (var item in result.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateItemShape(item, cancellationToken);
        }

        var response = await HomeAssistantJson.DeserializeResponseAsync<HomeAssistantMediaSearchResponse>(
            value,
            "The media search response could not be decoded.",
            cancellationToken).ConfigureAwait(false);
        if (response.Items is null)
            throw new HomeAssistantProtocolException("The media search response contained a null result.");
        foreach (var item in response.Items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item is null)
                throw new HomeAssistantProtocolException("The media search response contained a null result.");
            ValidateItemCollections(item, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return response;
    }

    internal static async Task<HomeAssistantMediaItem> DecodeItemAsync(
        JsonElement value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        RequireNoDuplicateProperties(value, "The media browse response contained duplicate JSON properties.", cancellationToken);
        ValidateItemShape(value, cancellationToken);
        var result = await HomeAssistantJson.DeserializeResponseAsync<HomeAssistantMediaItem>(
            value,
            "The media browse response could not be decoded.",
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateItemCollections(result, cancellationToken);
        return result;
    }

    private static void ValidateItemShape(JsonElement value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.Object
            || !value.TryGetProperty("media_class", out var mediaClass)
            || mediaClass.ValueKind != JsonValueKind.String
            || !value.TryGetProperty("media_content_id", out var mediaContentId)
            || mediaContentId.ValueKind != JsonValueKind.String
            || !value.TryGetProperty("media_content_type", out var mediaContentType)
            || mediaContentType.ValueKind != JsonValueKind.String
            || !value.TryGetProperty("can_play", out var canPlay)
            || canPlay.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !value.TryGetProperty("can_expand", out var canExpand)
            || canExpand.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || !value.TryGetProperty("can_search", out var canSearch)
            || canSearch.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || value.TryGetProperty("not_shown", out var notShown)
                && (notShown.ValueKind != JsonValueKind.Number || !notShown.TryGetInt32(out var hiddenCount) || hiddenCount < 0))
        {
            throw new HomeAssistantProtocolException("The media response omitted its required identity or actionability fields.");
        }

        if (!value.TryGetProperty("children", out var children)) return;
        if (children.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException("The media response contained an invalid children collection.");
        foreach (var child in children.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidateItemShape(child, cancellationToken);
        }
    }

    private static void ValidateItemCollections(
        HomeAssistantMediaItem item,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(item.Title))
            throw new HomeAssistantProtocolException("The media response omitted an item title.");
        if ((item.CanPlay || item.CanExpand)
            && (string.IsNullOrWhiteSpace(item.MediaContentId) || string.IsNullOrWhiteSpace(item.MediaContentType)))
            throw new HomeAssistantProtocolException("The media response contained an item without a media content identifier or type.");
        if (item.Children is null)
            throw new HomeAssistantProtocolException("The media response contained a null children collection.");
        if (item.SearchMediaClasses is not null)
        {
            foreach (var mediaClass in item.SearchMediaClasses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (mediaClass is null)
                    throw new HomeAssistantProtocolException("The media response contained a null search media class.");
            }
        }

        foreach (var child in item.Children)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (child is null)
                throw new HomeAssistantProtocolException("The media response contained a null child.");
            ValidateItemCollections(child, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static bool IsValidResolvedUrl(string? value)
    {
        if (value is null
            || value.Length == 0
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            if (value.StartsWith("//", StringComparison.Ordinal)
                || value.Contains('\\')
                || !Uri.TryCreate(value, UriKind.Relative, out _))
            {
                return false;
            }

            var resolved = new Uri(new Uri("https://homeassistant.invalid", UriKind.Absolute), value);
            return string.Equals(resolved.PathAndQuery + resolved.Fragment, value, StringComparison.Ordinal);
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && absolute.IsWellFormedOriginalString();
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

    private static void AddMediaClasses(
        IDictionary<string, object?> payload,
        IReadOnlyCollection<string>? mediaClasses,
        CancellationToken cancellationToken)
    {
        if (mediaClasses is null) return;
        var values = new List<string>(mediaClasses.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mediaClass in mediaClasses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(mediaClass))
                throw new ArgumentException("Media classes cannot be empty.", nameof(mediaClasses));
            var normalized = mediaClass.Trim();
            if (seen.Add(normalized)) values.Add(normalized);
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (values.Count == 0) throw new ArgumentException("Media classes cannot be empty.", nameof(mediaClasses));
        payload["media_filter_classes"] = values.ToArray();
    }

    private static void RequireNoDuplicateProperties(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (HomeAssistantJson.HasDuplicateProperties(value, cancellationToken))
            throw new HomeAssistantProtocolException(failureMessage);
    }

    private static string Require(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A non-empty value is required.", parameterName);
        return value.Trim();
    }
}
