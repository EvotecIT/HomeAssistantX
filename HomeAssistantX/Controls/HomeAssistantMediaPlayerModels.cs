using HomeAssistantX.Models;
using HomeAssistantX.Exceptions;
using System.Text.Json;

namespace HomeAssistantX.Controls;

/// <summary>Features exposed through a Home Assistant <c>media_player</c> entity.</summary>
[Flags]
public enum HomeAssistantMediaPlayerFeature : long
{
    None = 0,
    Pause = 1,
    Seek = 2,
    VolumeSet = 4,
    VolumeMute = 8,
    PreviousTrack = 16,
    NextTrack = 32,
    TurnOn = 128,
    TurnOff = 256,
    PlayMedia = 512,
    VolumeStep = 1024,
    SelectSource = 2048,
    Stop = 4096,
    ClearPlaylist = 8192,
    Play = 16384,
    ShuffleSet = 32768,
    SelectSoundMode = 65536,
    BrowseMedia = 131072,
    RepeatSet = 262144,
    Grouping = 524288,
    MediaAnnounce = 1048576,
    MediaEnqueue = 2097152,
    SearchMedia = 4194304
}

public enum HomeAssistantMediaPlayerState
{
    Other,
    Unknown,
    Unavailable,
    Off,
    On,
    Idle,
    Playing,
    Paused,
    Buffering,
    Standby
}

public enum HomeAssistantMediaPlaybackAction
{
    Play,
    Pause,
    PlayPause,
    Stop,
    Next,
    Previous
}

public enum HomeAssistantMediaVolumeStepAction
{
    Up,
    Down
}

public enum HomeAssistantMediaRepeatMode
{
    Off,
    One,
    All
}

public enum HomeAssistantMediaEnqueueMode
{
    Add,
    Next,
    Play,
    Replace
}

/// <summary>Options supported by Home Assistant's <c>media_player.play_media</c> action.</summary>
public sealed class HomeAssistantPlayMediaOptions
{
    public HomeAssistantMediaEnqueueMode? Enqueue { get; set; }

    public bool? Announce { get; set; }

    public IReadOnlyDictionary<string, object?>? Extra { get; set; }
}

/// <summary>Typed media-player changes that may be applied in one logical operation.</summary>
public sealed class HomeAssistantMediaPlayerOptions
{
    private double? _volumePercent;

    public HomeAssistantPowerAction? Power { get; set; }

    public HomeAssistantMediaPlaybackAction? Playback { get; set; }

    public double? VolumePercent
    {
        get => _volumePercent;
        set => _volumePercent = ControlValidation.Percent(value, nameof(VolumePercent));
    }

    public bool? Muted { get; set; }

    public string? Source { get; set; }

    public string? SoundMode { get; set; }

    public bool? Shuffle { get; set; }

    public HomeAssistantMediaRepeatMode? Repeat { get; set; }

    public string? MediaContentId { get; set; }

    public string? MediaContentType { get; set; }

    public HomeAssistantMediaEnqueueMode? Enqueue { get; set; }

    public bool? Announce { get; set; }

    public IReadOnlyDictionary<string, object?>? MediaExtra { get; set; }
}

/// <summary>A typed view of one raw Home Assistant media-player state.</summary>
public sealed class HomeAssistantMediaPlayerStatus
{
    private HomeAssistantMediaPlayerStatus(HomeAssistantState rawState)
    {
        RawState = rawState;
    }

    public HomeAssistantState RawState { get; }

    public string EntityId => RawState.EntityId;

    public string RawStateValue => RawState.State;

    public HomeAssistantMediaPlayerState State { get; private set; }

    public string? FriendlyName { get; private set; }

    public string? DeviceClass { get; private set; }

    public HomeAssistantMediaPlayerFeature SupportedFeatures { get; private set; }

    public double? VolumeLevel { get; private set; }

    public double? VolumePercent => VolumeLevel.HasValue ? VolumeLevel.Value * 100d : null;

    public double? VolumeStep { get; private set; }

    public bool? IsVolumeMuted { get; private set; }

    public string? Source { get; private set; }

    public IReadOnlyList<string> Sources { get; private set; } = Array.Empty<string>();

    public string? SoundMode { get; private set; }

    public IReadOnlyList<string> SoundModes { get; private set; } = Array.Empty<string>();

    public string? MediaContentId { get; private set; }

    public string? MediaContentType { get; private set; }

    public TimeSpan? MediaDuration { get; private set; }

    public TimeSpan? MediaPosition { get; private set; }

    public DateTimeOffset? MediaPositionUpdatedAt { get; private set; }

    public string? MediaTitle { get; private set; }

    public string? MediaArtist { get; private set; }

    public string? MediaAlbumName { get; private set; }

    public string? MediaAlbumArtist { get; private set; }

    public long? MediaTrack { get; private set; }

    public string? MediaSeriesTitle { get; private set; }

    public string? MediaSeason { get; private set; }

    public string? MediaEpisode { get; private set; }

    public string? MediaChannel { get; private set; }

    public string? MediaPlaylist { get; private set; }

    public string? AppId { get; private set; }

    public string? AppName { get; private set; }

    public bool? Shuffle { get; private set; }

    public string? Repeat { get; private set; }

    public IReadOnlyList<string> GroupMembers { get; private set; } = Array.Empty<string>();

    public string? MediaImageUrl { get; private set; }

    public string? EntityPicture { get; private set; }

    public string? EntityPictureLocal { get; private set; }

    public string? Manufacturer { get; private set; }

    public string? ModelName { get; private set; }

    public bool IsAvailable => State != HomeAssistantMediaPlayerState.Unavailable;

    public bool Supports(HomeAssistantMediaPlayerFeature feature)
    {
        return feature != HomeAssistantMediaPlayerFeature.None
            && (SupportedFeatures & feature) == feature;
    }

    /// <summary>Estimates playback position using HA's last position timestamp and caps it to known duration.</summary>
    public TimeSpan? GetEstimatedPosition(DateTimeOffset now)
    {
        if (!MediaPosition.HasValue)
        {
            return null;
        }

        var positionSeconds = MediaPosition.Value.TotalSeconds;
        if (State == HomeAssistantMediaPlayerState.Playing && MediaPositionUpdatedAt.HasValue)
        {
            var elapsed = now - MediaPositionUpdatedAt.Value;
            if (elapsed > TimeSpan.Zero)
            {
                positionSeconds = Math.Min(
                    TimeSpan.MaxValue.TotalSeconds,
                    positionSeconds + elapsed.TotalSeconds);
            }
        }

        if (MediaDuration.HasValue && positionSeconds > MediaDuration.Value.TotalSeconds)
        {
            return MediaDuration.Value;
        }

        if (positionSeconds < 0)
        {
            return TimeSpan.Zero;
        }

        return ToTimeSpan(positionSeconds) ?? TimeSpan.MaxValue;
    }

    public Uri? ResolveArtworkUri(Uri homeAssistantBaseUri)
    {
        if (homeAssistantBaseUri is null)
        {
            throw new ArgumentNullException(nameof(homeAssistantBaseUri));
        }

        foreach (var value in new[] { MediaImageUrl, EntityPictureLocal, EntityPicture })
        {
            if (value is null || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var trimmed = value.Trim();
            if (!Uri.TryCreate(trimmed, UriKind.RelativeOrAbsolute, out var candidate)) continue;

            if (candidate.IsAbsoluteUri)
            {
                if (IsSupportedArtworkUri(candidate)) return candidate;
                continue;
            }

            var relativeArtwork = trimmed.StartsWith("//", StringComparison.Ordinal)
                ? homeAssistantBaseUri.Scheme + ":" + trimmed
                : trimmed;
            if (Uri.TryCreate(homeAssistantBaseUri, relativeArtwork, out var resolved)
                && IsSupportedArtworkUri(resolved))
            {
                return resolved;
            }
        }

        return null;
    }

    private static bool IsSupportedArtworkUri(Uri value)
        => (value.Scheme == Uri.UriSchemeHttp || value.Scheme == Uri.UriSchemeHttps)
            && string.IsNullOrEmpty(value.UserInfo);

    public static HomeAssistantMediaPlayerStatus FromState(HomeAssistantState state)
        => FromState(state, default);

    internal static HomeAssistantMediaPlayerStatus FromState(
        HomeAssistantState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (state is null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        if (!HomeAssistantEntityId.TryNormalizeForDomain(state.EntityId, "media_player", out var normalizedEntityId)
            || !string.Equals(state.EntityId, normalizedEntityId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A canonical media_player entity state is required.", nameof(state));
        }

        if (string.IsNullOrWhiteSpace(state.State))
        {
            throw new HomeAssistantProtocolException("The Home Assistant media-player state omitted its required state value.");
        }

        var attributes = state.Attributes;
        var duration = HomeAssistantAttributeReader.GetDouble(attributes, "media_duration");
        var position = HomeAssistantAttributeReader.GetDouble(attributes, "media_position");
        return new HomeAssistantMediaPlayerStatus(state)
        {
            State = ParseState(state.State),
            FriendlyName = HomeAssistantAttributeReader.GetString(attributes, "friendly_name"),
            DeviceClass = HomeAssistantAttributeReader.GetString(attributes, "device_class"),
            SupportedFeatures = (HomeAssistantMediaPlayerFeature)(HomeAssistantAttributeReader.GetNonNegativeInt64(attributes, "supported_features") ?? 0),
            VolumeLevel = GetNormalizedVolume(attributes, "volume_level"),
            VolumeStep = GetNormalizedVolume(attributes, "volume_step"),
            IsVolumeMuted = HomeAssistantAttributeReader.GetBoolean(attributes, "is_volume_muted"),
            Source = HomeAssistantAttributeReader.GetString(attributes, "source"),
            Sources = HomeAssistantAttributeReader.GetStringList(attributes, "source_list", cancellationToken),
            SoundMode = HomeAssistantAttributeReader.GetString(attributes, "sound_mode"),
            SoundModes = HomeAssistantAttributeReader.GetStringList(attributes, "sound_mode_list", cancellationToken),
            MediaContentId = HomeAssistantAttributeReader.GetString(attributes, "media_content_id"),
            MediaContentType = HomeAssistantAttributeReader.GetString(attributes, "media_content_type"),
            MediaDuration = ToTimeSpan(duration),
            MediaPosition = ToTimeSpan(position),
            MediaPositionUpdatedAt = HomeAssistantAttributeReader.GetDateTimeOffset(attributes, "media_position_updated_at"),
            MediaTitle = HomeAssistantAttributeReader.GetString(attributes, "media_title"),
            MediaArtist = HomeAssistantAttributeReader.GetString(attributes, "media_artist"),
            MediaAlbumName = HomeAssistantAttributeReader.GetString(attributes, "media_album_name"),
            MediaAlbumArtist = HomeAssistantAttributeReader.GetString(attributes, "media_album_artist"),
            MediaTrack = HomeAssistantAttributeReader.GetInt64(attributes, "media_track"),
            MediaSeriesTitle = HomeAssistantAttributeReader.GetString(attributes, "media_series_title"),
            MediaSeason = HomeAssistantAttributeReader.GetString(attributes, "media_season"),
            MediaEpisode = HomeAssistantAttributeReader.GetString(attributes, "media_episode"),
            MediaChannel = HomeAssistantAttributeReader.GetString(attributes, "media_channel"),
            MediaPlaylist = HomeAssistantAttributeReader.GetString(attributes, "media_playlist"),
            AppId = HomeAssistantAttributeReader.GetString(attributes, "app_id"),
            AppName = HomeAssistantAttributeReader.GetString(attributes, "app_name"),
            Shuffle = HomeAssistantAttributeReader.GetBoolean(attributes, "shuffle"),
            Repeat = HomeAssistantAttributeReader.GetString(attributes, "repeat"),
            GroupMembers = GetGroupMembers(attributes, cancellationToken),
            MediaImageUrl = HomeAssistantAttributeReader.GetString(attributes, "media_image_url"),
            EntityPicture = HomeAssistantAttributeReader.GetString(attributes, "entity_picture"),
            EntityPictureLocal = HomeAssistantAttributeReader.GetString(attributes, "entity_picture_local"),
            Manufacturer = HomeAssistantAttributeReader.GetString(attributes, "manufacturer"),
            ModelName = HomeAssistantAttributeReader.GetString(attributes, "model_name")
        };
    }

    private static HomeAssistantMediaPlayerState ParseState(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "unknown" => HomeAssistantMediaPlayerState.Unknown,
            "unavailable" => HomeAssistantMediaPlayerState.Unavailable,
            "off" => HomeAssistantMediaPlayerState.Off,
            "on" => HomeAssistantMediaPlayerState.On,
            "idle" => HomeAssistantMediaPlayerState.Idle,
            "playing" => HomeAssistantMediaPlayerState.Playing,
            "paused" => HomeAssistantMediaPlayerState.Paused,
            "buffering" => HomeAssistantMediaPlayerState.Buffering,
            "standby" => HomeAssistantMediaPlayerState.Standby,
            _ => HomeAssistantMediaPlayerState.Other
        };
    }

    private static IReadOnlyList<string> GetGroupMembers(
        IReadOnlyDictionary<string, JsonElement> attributes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HomeAssistantAttributeReader.TryGetValue(attributes, "group_members", out var value))
        {
            return Array.Empty<string>();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantProtocolException("The Home Assistant media-player group members were malformed.");
        }

        var members = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.String
                || !HomeAssistantEntityId.TryNormalizeForDomain(item.GetString(), "media_player", out var member)
                || !string.Equals(item.GetString(), member, StringComparison.Ordinal)
                || !unique.Add(member))
            {
                throw new HomeAssistantProtocolException("The Home Assistant media-player group members contained an invalid or duplicate entity identifier.");
            }

            members.Add(member);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return members;
    }

    private static double? GetNormalizedVolume(
        IReadOnlyDictionary<string, System.Text.Json.JsonElement> attributes,
        string name)
    {
        var value = HomeAssistantAttributeReader.GetDouble(attributes, name);
        return value.HasValue && value.Value >= 0d && value.Value <= 1d ? value : null;
    }

    private static TimeSpan? ToTimeSpan(double? seconds)
    {
        if (!seconds.HasValue || seconds.Value < 0)
        {
            return null;
        }

        try
        {
            var ticks = decimal.Round(
                (decimal)seconds.Value * TimeSpan.TicksPerSecond,
                0,
                MidpointRounding.AwayFromZero);
            return ticks <= long.MaxValue
                ? TimeSpan.FromTicks((long)ticks)
                : null;
        }
        catch (OverflowException)
        {
            return null;
        }
    }
}

public sealed class HomeAssistantMediaPlayerStateChange
{
    internal HomeAssistantMediaPlayerStateChange(
        string entityId,
        HomeAssistantMediaPlayerStatus? previous,
        HomeAssistantMediaPlayerStatus? current)
    {
        EntityId = entityId;
        Previous = previous;
        Current = current;
    }

    public string EntityId { get; }

    public HomeAssistantMediaPlayerStatus? Previous { get; }

    public HomeAssistantMediaPlayerStatus? Current { get; }

    public bool IsRemoval => Current is null;
}
