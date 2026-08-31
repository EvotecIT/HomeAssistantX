using HomeAssistantX.Models;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Protocol;
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
    private readonly object _sync = new();
    private HomeAssistantMediaEnqueueMode? _enqueue;
    private bool? _announce;
    private IReadOnlyDictionary<string, object?>? _extra;

    public HomeAssistantMediaEnqueueMode? Enqueue
    {
        get { lock (_sync) return _enqueue; }
        set { lock (_sync) _enqueue = value; }
    }

    public bool? Announce
    {
        get { lock (_sync) return _announce; }
        set { lock (_sync) _announce = value; }
    }

    public IReadOnlyDictionary<string, object?>? Extra
    {
        get { lock (_sync) return _extra; }
        set { lock (_sync) _extra = value; }
    }

    internal HomeAssistantPlayMediaOptions Snapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HomeAssistantMediaEnqueueMode? enqueue;
        bool? announce;
        IReadOnlyDictionary<string, object?>? extra;
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            enqueue = _enqueue;
            announce = _announce;
            extra = _extra;
        }

        var frozenExtra = extra is null
            ? null
            : HomeAssistantJson.FreezeObject(
                extra,
                nameof(Extra),
                "MediaExtra",
                cancellationToken);
        return new HomeAssistantPlayMediaOptions
        {
            Enqueue = enqueue,
            Announce = announce,
            Extra = frozenExtra
        };
    }
}

/// <summary>Typed media-player changes that may be applied in one logical operation.</summary>
public sealed class HomeAssistantMediaPlayerOptions
{
    private readonly object _sync = new();
    private double? _volumePercent;
    private HomeAssistantPowerAction? _power;
    private HomeAssistantMediaPlaybackAction? _playback;
    private bool? _muted;
    private string? _source;
    private string? _soundMode;
    private bool? _shuffle;
    private HomeAssistantMediaRepeatMode? _repeat;
    private string? _mediaContentId;
    private string? _mediaContentType;
    private HomeAssistantMediaEnqueueMode? _enqueue;
    private bool? _announce;
    private IReadOnlyDictionary<string, object?>? _mediaExtra;

    public HomeAssistantPowerAction? Power
    {
        get { lock (_sync) return _power; }
        set { lock (_sync) _power = value; }
    }

    public HomeAssistantMediaPlaybackAction? Playback
    {
        get { lock (_sync) return _playback; }
        set { lock (_sync) _playback = value; }
    }

    public double? VolumePercent
    {
        get { lock (_sync) return _volumePercent; }
        set
        {
            var validated = ControlValidation.Percent(value, nameof(VolumePercent));
            lock (_sync) _volumePercent = validated;
        }
    }

    public bool? Muted
    {
        get { lock (_sync) return _muted; }
        set { lock (_sync) _muted = value; }
    }

    public string? Source
    {
        get { lock (_sync) return _source; }
        set { lock (_sync) _source = value; }
    }

    public string? SoundMode
    {
        get { lock (_sync) return _soundMode; }
        set { lock (_sync) _soundMode = value; }
    }

    public bool? Shuffle
    {
        get { lock (_sync) return _shuffle; }
        set { lock (_sync) _shuffle = value; }
    }

    public HomeAssistantMediaRepeatMode? Repeat
    {
        get { lock (_sync) return _repeat; }
        set { lock (_sync) _repeat = value; }
    }

    public string? MediaContentId
    {
        get { lock (_sync) return _mediaContentId; }
        set { lock (_sync) _mediaContentId = value; }
    }

    public string? MediaContentType
    {
        get { lock (_sync) return _mediaContentType; }
        set { lock (_sync) _mediaContentType = value; }
    }

    public HomeAssistantMediaEnqueueMode? Enqueue
    {
        get { lock (_sync) return _enqueue; }
        set { lock (_sync) _enqueue = value; }
    }

    public bool? Announce
    {
        get { lock (_sync) return _announce; }
        set { lock (_sync) _announce = value; }
    }

    public IReadOnlyDictionary<string, object?>? MediaExtra
    {
        get { lock (_sync) return _mediaExtra; }
        set { lock (_sync) _mediaExtra = value; }
    }

    internal HomeAssistantMediaPlayerOptions Snapshot(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        double? volumePercent;
        HomeAssistantPowerAction? power;
        HomeAssistantMediaPlaybackAction? playback;
        bool? muted;
        string? source;
        string? soundMode;
        bool? shuffle;
        HomeAssistantMediaRepeatMode? repeat;
        string? mediaContentId;
        string? mediaContentType;
        HomeAssistantMediaEnqueueMode? enqueue;
        bool? announce;
        IReadOnlyDictionary<string, object?>? mediaExtra;
        lock (_sync)
        {
            cancellationToken.ThrowIfCancellationRequested();
            volumePercent = _volumePercent;
            power = _power;
            playback = _playback;
            muted = _muted;
            source = _source;
            soundMode = _soundMode;
            shuffle = _shuffle;
            repeat = _repeat;
            mediaContentId = _mediaContentId;
            mediaContentType = _mediaContentType;
            enqueue = _enqueue;
            announce = _announce;
            mediaExtra = _mediaExtra;
        }

        var frozenMediaExtra = mediaExtra is null
            ? null
            : HomeAssistantJson.FreezeObject(
                mediaExtra,
                nameof(MediaExtra),
                "MediaExtra",
                cancellationToken);
        return new HomeAssistantMediaPlayerOptions
        {
            Power = power,
            Playback = playback,
            VolumePercent = volumePercent,
            Muted = muted,
            Source = source,
            SoundMode = soundMode,
            Shuffle = shuffle,
            Repeat = repeat,
            MediaContentId = mediaContentId,
            MediaContentType = mediaContentType,
            Enqueue = enqueue,
            Announce = announce,
            MediaExtra = frozenMediaExtra
        };
    }
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

        if (!HomeAssistantEntityId.TryNormalizeForDomain(state.EntityId, "media_player", cancellationToken, out var normalizedEntityId)
            || !string.Equals(state.EntityId, normalizedEntityId, StringComparison.Ordinal))
        {
            throw new ArgumentException("A canonical media_player entity state is required.", nameof(state));
        }

        if (CancellationAwareString.IsNullOrWhiteSpace(state.State, cancellationToken))
        {
            throw new HomeAssistantProtocolException("The Home Assistant media-player state omitted its required state value.");
        }

        var attributes = state.Attributes;
        var duration = HomeAssistantAttributeReader.GetDouble(attributes, "media_duration", cancellationToken);
        var position = HomeAssistantAttributeReader.GetDouble(attributes, "media_position", cancellationToken);
        return new HomeAssistantMediaPlayerStatus(state)
        {
            State = ParseState(state.State, cancellationToken),
            FriendlyName = HomeAssistantAttributeReader.GetString(attributes, "friendly_name", cancellationToken),
            DeviceClass = HomeAssistantAttributeReader.GetString(attributes, "device_class", cancellationToken),
            SupportedFeatures = (HomeAssistantMediaPlayerFeature)(HomeAssistantAttributeReader.GetNonNegativeInt64(attributes, "supported_features", cancellationToken) ?? 0),
            VolumeLevel = GetNormalizedVolume(attributes, "volume_level", cancellationToken),
            VolumeStep = GetNormalizedVolume(attributes, "volume_step", cancellationToken),
            IsVolumeMuted = HomeAssistantAttributeReader.GetBoolean(attributes, "is_volume_muted", cancellationToken),
            Source = HomeAssistantAttributeReader.GetString(attributes, "source", cancellationToken),
            Sources = HomeAssistantAttributeReader.GetStringList(attributes, "source_list", cancellationToken),
            SoundMode = HomeAssistantAttributeReader.GetString(attributes, "sound_mode", cancellationToken),
            SoundModes = HomeAssistantAttributeReader.GetStringList(attributes, "sound_mode_list", cancellationToken),
            MediaContentId = HomeAssistantAttributeReader.GetString(attributes, "media_content_id", cancellationToken),
            MediaContentType = HomeAssistantAttributeReader.GetString(attributes, "media_content_type", cancellationToken),
            MediaDuration = ToTimeSpan(duration),
            MediaPosition = ToTimeSpan(position),
            MediaPositionUpdatedAt = HomeAssistantAttributeReader.GetDateTimeOffset(attributes, "media_position_updated_at", cancellationToken),
            MediaTitle = HomeAssistantAttributeReader.GetString(attributes, "media_title", cancellationToken),
            MediaArtist = HomeAssistantAttributeReader.GetString(attributes, "media_artist", cancellationToken),
            MediaAlbumName = HomeAssistantAttributeReader.GetString(attributes, "media_album_name", cancellationToken),
            MediaAlbumArtist = HomeAssistantAttributeReader.GetString(attributes, "media_album_artist", cancellationToken),
            MediaTrack = HomeAssistantAttributeReader.GetInt64(attributes, "media_track", cancellationToken),
            MediaSeriesTitle = HomeAssistantAttributeReader.GetString(attributes, "media_series_title", cancellationToken),
            MediaSeason = HomeAssistantAttributeReader.GetString(attributes, "media_season", cancellationToken),
            MediaEpisode = HomeAssistantAttributeReader.GetString(attributes, "media_episode", cancellationToken),
            MediaChannel = HomeAssistantAttributeReader.GetString(attributes, "media_channel", cancellationToken),
            MediaPlaylist = HomeAssistantAttributeReader.GetString(attributes, "media_playlist", cancellationToken),
            AppId = HomeAssistantAttributeReader.GetString(attributes, "app_id", cancellationToken),
            AppName = HomeAssistantAttributeReader.GetString(attributes, "app_name", cancellationToken),
            Shuffle = HomeAssistantAttributeReader.GetBoolean(attributes, "shuffle", cancellationToken),
            Repeat = HomeAssistantAttributeReader.GetString(attributes, "repeat", cancellationToken),
            GroupMembers = GetGroupMembers(attributes, cancellationToken),
            MediaImageUrl = HomeAssistantAttributeReader.GetStrictString(attributes, "media_image_url", cancellationToken),
            EntityPicture = HomeAssistantAttributeReader.GetStrictString(attributes, "entity_picture", cancellationToken),
            EntityPictureLocal = HomeAssistantAttributeReader.GetStrictString(attributes, "entity_picture_local", cancellationToken),
            Manufacturer = HomeAssistantAttributeReader.GetString(attributes, "manufacturer", cancellationToken),
            ModelName = HomeAssistantAttributeReader.GetString(attributes, "model_name", cancellationToken)
        };
    }

    private static HomeAssistantMediaPlayerState ParseState(
        string state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        HomeAssistantMediaPlayerState result;
        if (state.Equals("unknown", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.Unknown;
        else if (state.Equals("unavailable", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.Unavailable;
        else if (state.Equals("off", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.Off;
        else if (state.Equals("on", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.On;
        else if (state.Equals("idle", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.Idle;
        else if (state.Equals("playing", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.Playing;
        else if (state.Equals("paused", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.Paused;
        else if (state.Equals("buffering", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.Buffering;
        else if (state.Equals("standby", StringComparison.OrdinalIgnoreCase)) result = HomeAssistantMediaPlayerState.Standby;
        else result = HomeAssistantMediaPlayerState.Other;
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    internal static IReadOnlyList<string> GetGroupMembers(
        IReadOnlyDictionary<string, JsonElement> attributes,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HomeAssistantAttributeReader.TryGetValue(
                attributes,
                "group_members",
                out var value,
                cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Array.Empty<string>();
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new HomeAssistantProtocolException("The Home Assistant media-player group members were malformed.");
        }

        if (value.GetArrayLength() == 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Array.Empty<string>();
        }

        return RunGroupMemberDecode(
            () => GetGroupMembersCore(value, cancellationToken),
            cancellationToken);
    }

    private static IReadOnlyList<string> RunGroupMemberDecode(
        Func<IReadOnlyList<string>> operation,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled)
        {
            return operation();
        }

        var operationTask = Task.Factory.StartNew(
            operation,
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = cancellationToken.Register(() => canceled.TrySetResult(true));
        var completed = Task.WhenAny(operationTask, canceled.Task).ConfigureAwait(false).GetAwaiter().GetResult();
        if (!ReferenceEquals(completed, operationTask))
        {
            _ = operationTask.ContinueWith(
                static task => _ = task.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
            cancellationToken.ThrowIfCancellationRequested();
        }

        var result = operationTask.ConfigureAwait(false).GetAwaiter().GetResult();
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    private static IReadOnlyList<string> GetGroupMembersCore(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var members = new List<string>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rawMember = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
            if (item.ValueKind != JsonValueKind.String
                || !HomeAssistantEntityId.TryNormalizeForDomain(rawMember, "media_player", cancellationToken, out var member)
                || !CancellationAwareString.EqualsOrdinal(rawMember, member, cancellationToken)
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
        string name,
        CancellationToken cancellationToken)
    {
        var value = HomeAssistantAttributeReader.GetDouble(attributes, name, cancellationToken);
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
