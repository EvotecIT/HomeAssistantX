using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Media;

/// <summary>Browses and resolves global media sources and media exposed by individual players.</summary>
public sealed class HomeAssistantMediaBrowserClient
{
    private const int MaximumResolvedUrlLength = 16 * 1024;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantMediaBrowserClient(HomeAssistantWebSocketClient webSocket) => _webSocket = webSocket;

    public Task<HomeAssistantMediaItem> BrowseSourcesAsync(string? mediaContentId = null, CancellationToken cancellationToken = default)
        => RequestItemAsync("media_source/browse_media", OptionalContentId(mediaContentId, cancellationToken), cancellationToken);

    public async Task<IReadOnlyList<HomeAssistantMediaItem>> SearchSourcesAsync(string searchQuery, string? mediaContentId = null, IReadOnlyCollection<string>? mediaClasses = null, CancellationToken cancellationToken = default)
        => (await SearchSourcesResponseAsync(searchQuery, mediaContentId, mediaClasses, cancellationToken).ConfigureAwait(false)).Items;

    /// <summary>Searches global media sources and preserves response-level provider metadata.</summary>
    public Task<HomeAssistantMediaSearchResponse> SearchSourcesResponseAsync(string searchQuery, string? mediaContentId = null, IReadOnlyCollection<string>? mediaClasses = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = OptionalContentId(mediaContentId, cancellationToken);
        payload["search_query"] = RequirePreserved(searchQuery, nameof(searchQuery), cancellationToken);
        AddMediaClasses(payload, mediaClasses, cancellationToken);
        return RequestSearchAsync("media_source/search_media", payload, cancellationToken);
    }

    public async Task<HomeAssistantResolvedMedia> ResolveAsync(string mediaContentId, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
    {
        var payload = new Dictionary<string, object?> { ["media_content_id"] = RequirePreserved(mediaContentId, nameof(mediaContentId), cancellationToken) };
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
        if (!IsValidResolvedUrl(result.Url, cancellationToken))
            throw new HomeAssistantProtocolException("The resolved media response contained an invalid URL.");
        if (result.MimeType is not null && !IsValidMediaType(result.MimeType, cancellationToken))
            throw new HomeAssistantProtocolException("The resolved media response contained an invalid MIME type.");
        return result;
    }

    public Task<HomeAssistantMediaItem> BrowsePlayerAsync(string entityId, string? mediaContentType = null, string? mediaContentId = null, CancellationToken cancellationToken = default)
    {
        var payload = PlayerPayload(entityId, mediaContentType, mediaContentId, cancellationToken);
        return RequestItemAsync("media_player/browse_media", payload, cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantMediaItem>> SearchPlayerAsync(string entityId, string searchQuery, string? mediaContentType = null, string? mediaContentId = null, IReadOnlyCollection<string>? mediaClasses = null, CancellationToken cancellationToken = default)
        => (await SearchPlayerResponseAsync(entityId, searchQuery, mediaContentType, mediaContentId, mediaClasses, cancellationToken).ConfigureAwait(false)).Items;

    /// <summary>Searches media exposed by a player and preserves response-level provider metadata.</summary>
    public Task<HomeAssistantMediaSearchResponse> SearchPlayerResponseAsync(string entityId, string searchQuery, string? mediaContentType = null, string? mediaContentId = null, IReadOnlyCollection<string>? mediaClasses = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var payload = PlayerPayload(entityId, mediaContentType, mediaContentId, cancellationToken);
        payload["search_query"] = RequirePreserved(searchQuery, nameof(searchQuery), cancellationToken);
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
        var result = FindSearchResult(value, cancellationToken);
        if (result.ValueKind != JsonValueKind.Array)
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

    internal static void ValidateItemShape(JsonElement value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.Object)
            throw new HomeAssistantProtocolException("The media response omitted its required identity or actionability fields.");

        var mediaClass = default(JsonElement);
        var mediaContentId = default(JsonElement);
        var mediaContentType = default(JsonElement);
        var canPlay = default(JsonElement);
        var canExpand = default(JsonElement);
        var canSearch = default(JsonElement);
        var notShown = default(JsonElement);
        var childrenMediaClass = default(JsonElement);
        var children = default(JsonElement);
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            HomeAssistantJson.ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
            if (property.NameEquals("media_class")) mediaClass = property.Value;
            else if (property.NameEquals("media_content_id")) mediaContentId = property.Value;
            else if (property.NameEquals("media_content_type")) mediaContentType = property.Value;
            else if (property.NameEquals("can_play")) canPlay = property.Value;
            else if (property.NameEquals("can_expand")) canExpand = property.Value;
            else if (property.NameEquals("can_search")) canSearch = property.Value;
            else if (property.NameEquals("not_shown")) notShown = property.Value;
            else if (property.NameEquals("children_media_class")) childrenMediaClass = property.Value;
            else if (property.NameEquals("children")) children = property.Value;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (mediaClass.ValueKind == JsonValueKind.Undefined
            || mediaClass.ValueKind != JsonValueKind.String
            || !IsCanonicalMediaClass(mediaClass.GetString(), cancellationToken)
            || mediaContentId.ValueKind == JsonValueKind.Undefined
            || mediaContentId.ValueKind != JsonValueKind.String
            || mediaContentType.ValueKind == JsonValueKind.Undefined
            || mediaContentType.ValueKind != JsonValueKind.String
            || canPlay.ValueKind == JsonValueKind.Undefined
            || canPlay.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || canExpand.ValueKind == JsonValueKind.Undefined
            || canExpand.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || canSearch.ValueKind == JsonValueKind.Undefined
            || canSearch.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
            || notShown.ValueKind != JsonValueKind.Undefined
                && (notShown.ValueKind != JsonValueKind.Number || !notShown.TryGetInt32(out var hiddenCount) || hiddenCount < 0))
        {
            throw new HomeAssistantProtocolException("The media response omitted its required identity or actionability fields.");
        }

        if (childrenMediaClass.ValueKind != JsonValueKind.Undefined
            && childrenMediaClass.ValueKind != JsonValueKind.Null
            && (childrenMediaClass.ValueKind != JsonValueKind.String
                || !IsCanonicalMediaClass(childrenMediaClass.GetString(), cancellationToken)))
            throw new HomeAssistantProtocolException("The media response contained a noncanonical children media class.");

        if ((canPlay.GetBoolean() || canExpand.GetBoolean() || canSearch.GetBoolean())
            && (!IsActionableContentId(mediaContentId.GetString(), cancellationToken)
                || !IsCanonicalActionableSelector(mediaContentType.GetString(), cancellationToken)))
            throw new HomeAssistantProtocolException("An actionable media response contained a noncanonical selector.");

        if (children.ValueKind == JsonValueKind.Undefined) return;
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
        if (!HasNonWhitespace(item.Title, cancellationToken))
            throw new HomeAssistantProtocolException("The media response omitted an item title.");
        if (!IsCanonicalMediaClass(item.MediaClass, cancellationToken))
            throw new HomeAssistantProtocolException("The media response contained a noncanonical media class.");
        if (item.ChildrenMediaClass is not null && !IsCanonicalMediaClass(item.ChildrenMediaClass, cancellationToken))
            throw new HomeAssistantProtocolException("The media response contained a noncanonical children media class.");
        if ((item.CanPlay || item.CanExpand || item.CanSearch)
            && (!IsActionableContentId(item.MediaContentId, cancellationToken)
                || !IsCanonicalActionableSelector(item.MediaContentType, cancellationToken)))
            throw new HomeAssistantProtocolException("The media response contained an item without a media content identifier or type.");
        if (item.Children is null)
            throw new HomeAssistantProtocolException("The media response contained a null children collection.");
        if (item.SearchMediaClasses is not null)
        {
            foreach (var mediaClass in item.SearchMediaClasses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCanonicalTrimmed(mediaClass, cancellationToken))
                    throw new HomeAssistantProtocolException("The media response contained a noncanonical search media class.");
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

    private static bool IsCanonicalMediaClass(string? value, CancellationToken cancellationToken)
        => IsCanonicalTrimmed(value, cancellationToken);

    internal static bool IsValidResolvedUrl(
        string? value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null
            || value.Length == 0
            || value.Length > MaximumResolvedUrlLength
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[value.Length - 1])
            || ContainsWhitespace(value, cancellationToken))
        {
            return false;
        }

        if (value.StartsWith("/", StringComparison.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.StartsWith("//", StringComparison.Ordinal)
                || ContainsCharacter(value, '\\', cancellationToken)
                || !Uri.TryCreate(value, UriKind.Relative, out _))
            {
                cancellationToken.ThrowIfCancellationRequested();
                return false;
            }

            cancellationToken.ThrowIfCancellationRequested();
            var resolved = new Uri(new Uri("https://homeassistant.invalid", UriKind.Absolute), value);
            cancellationToken.ThrowIfCancellationRequested();
            var canonical = string.Equals(resolved.PathAndQuery + resolved.Fragment, value, StringComparison.Ordinal);
            cancellationToken.ThrowIfCancellationRequested();
            return canonical;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var valid = Uri.TryCreate(value, UriKind.Absolute, out var absolute)
            && absolute.IsWellFormedOriginalString()
            && (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(absolute.UserInfo);
        cancellationToken.ThrowIfCancellationRequested();
        return valid;
    }

    private static bool ContainsCharacter(
        string value,
        char expected,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (value[index] == expected) return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private static bool IsCanonicalActionableSelector(string? value, CancellationToken cancellationToken)
        => IsCanonicalTrimmed(value, cancellationToken);

    private static Dictionary<string, object?> PlayerPayload(
        string entityId,
        string? mediaContentType,
        string? mediaContentId,
        CancellationToken cancellationToken)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "media_player", cancellationToken, out entityId))
            throw new ArgumentException("A media-player entity identifier is required.", nameof(entityId));
        var payload = new Dictionary<string, object?> { ["entity_id"] = entityId };
        if (mediaContentType is not null)
        {
            payload["media_content_type"] = Require(mediaContentType, nameof(mediaContentType), cancellationToken);
        }
        if (mediaContentId is not null)
        {
            payload["media_content_id"] = RequirePreserved(mediaContentId, nameof(mediaContentId), cancellationToken);
        }
        return payload;
    }

    private static Dictionary<string, object?> OptionalContentId(
        string? mediaContentId,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>();
        if (mediaContentId is not null)
        {
            payload["media_content_id"] = RequirePreserved(mediaContentId, nameof(mediaContentId), cancellationToken);
        }
        return payload;
    }

    private static void AddMediaClasses(
        IDictionary<string, object?> payload,
        IReadOnlyCollection<string>? mediaClasses,
        CancellationToken cancellationToken)
    {
        if (mediaClasses is null) return;
        var values = new List<string>(mediaClasses.Count);
        var seen = new HashSet<string>(new CancellationAwareStringEqualityComparer(cancellationToken));
        foreach (var mediaClass in mediaClasses)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalized = Require(mediaClass, nameof(mediaClasses), cancellationToken);
            ObserveString(normalized, cancellationToken);
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

    private static JsonElement FindSearchResult(JsonElement value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind != JsonValueKind.Object) return default;
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            HomeAssistantJson.ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
            if (property.NameEquals("result")) return property.Value;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return default;
    }

    private static string Require(
        string value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (value is null) throw new ArgumentNullException(parameterName);
        cancellationToken.ThrowIfCancellationRequested();
        var start = 0;
        while (start < value.Length && char.IsWhiteSpace(value[start]))
        {
            if ((start & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            start++;
        }
        var end = value.Length - 1;
        while (end >= start && char.IsWhiteSpace(value[end]))
        {
            if (((value.Length - 1 - end) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            end--;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (end < start) throw new ArgumentException("A non-empty value is required.", parameterName);
        return start == 0 && end == value.Length - 1
            ? value
            : value.Substring(start, end - start + 1);
    }

    private static string RequirePreserved(
        string value,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (value is null) throw new ArgumentNullException(parameterName);
        if (!HasNonWhitespace(value, cancellationToken))
            throw new ArgumentException("A non-empty value is required.", parameterName);
        ObserveString(value, cancellationToken);
        return value;
    }

    private static bool HasNonWhitespace(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (!char.IsWhiteSpace(value[index])) return true;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private static bool IsCanonicalTrimmed(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return value is not null
            && value.Length != 0
            && !char.IsWhiteSpace(value[0])
            && !char.IsWhiteSpace(value[value.Length - 1]);
    }

    private static bool IsActionableContentId(string? value, CancellationToken cancellationToken)
    {
        if (!HasNonWhitespace(value, cancellationToken)) return false;
        ObserveString(value!, cancellationToken);
        return true;
    }

    private static void ObserveString(string value, CancellationToken cancellationToken)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal static bool IsValidMediaType(
        string value,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrEmpty(value)
            || char.IsWhiteSpace(value[0])
            || char.IsWhiteSpace(value[value.Length - 1]))
        {
            return false;
        }

        var index = 0;
        if (!ReadMediaTypeToken(value, ref index, cancellationToken)
            || index >= value.Length
            || value[index++] != '/'
            || !ReadMediaTypeToken(value, ref index, cancellationToken))
        {
            return false;
        }

        while (true)
        {
            SkipOptionalWhitespace(value, ref index, cancellationToken);
            if (index == value.Length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }

            if (value[index++] != ';') return false;
            SkipOptionalWhitespace(value, ref index, cancellationToken);
            if (!ReadMediaTypeToken(value, ref index, cancellationToken)) return false;
            SkipOptionalWhitespace(value, ref index, cancellationToken);
            if (index >= value.Length || value[index++] != '=') return false;
            SkipOptionalWhitespace(value, ref index, cancellationToken);
            if (!ReadMediaTypeParameterValue(value, ref index, cancellationToken)) return false;
        }
    }

    private static bool ReadMediaTypeToken(
        string value,
        ref int index,
        CancellationToken cancellationToken)
    {
        var start = index;
        while (index < value.Length)
        {
            if (((index - start) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            var character = value[index];
            if (character is >= 'A' and <= 'Z'
                || character is >= 'a' and <= 'z'
                || character is >= '0' and <= '9'
                || character is '!' or '#' or '$' or '%' or '&' or '\'' or '*'
                    or '+' or '-' or '.' or '^' or '_' or '`' or '|' or '~')
            {
                index++;
                continue;
            }
            break;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return index > start;
    }

    private static bool ReadMediaTypeParameterValue(
        string value,
        ref int index,
        CancellationToken cancellationToken)
    {
        if (index >= value.Length) return false;
        if (value[index] != '"') return ReadMediaTypeToken(value, ref index, cancellationToken);

        index++;
        var escaped = false;
        var contentLength = 0;
        while (index < value.Length)
        {
            if ((contentLength & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            var character = value[index++];
            contentLength++;
            if (escaped)
            {
                if (IsInvalidQuotedCharacter(character)) return false;
                escaped = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }
            if (character == '"')
            {
                cancellationToken.ThrowIfCancellationRequested();
                return true;
            }
            if (IsInvalidQuotedCharacter(character))
            {
                return false;
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }

    private static bool IsInvalidQuotedCharacter(char value)
        => value is '\r' or '\n' or < ' ' && value != '\t' || value == '\u007f';

    private static void SkipOptionalWhitespace(
        string value,
        ref int index,
        CancellationToken cancellationToken)
    {
        var start = index;
        while (index < value.Length && value[index] is ' ' or '\t')
        {
            if (((index - start) & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            index++;
        }
        cancellationToken.ThrowIfCancellationRequested();
    }

    private static bool ContainsWhitespace(string value, CancellationToken cancellationToken)
    {
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            if (char.IsWhiteSpace(value[index])) return true;
        }

        cancellationToken.ThrowIfCancellationRequested();
        return false;
    }
}
