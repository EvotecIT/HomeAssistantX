using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.Subscriptions;

namespace HomeAssistantX.Controls;

/// <summary>Invokes common Home Assistant media-player actions with typed values.</summary>
public sealed class HomeAssistantMediaPlayerClient : HomeAssistantControlClientBase
{
    private readonly HomeAssistantStateClient _states;

    internal HomeAssistantMediaPlayerClient(
        HomeAssistantServiceClient services,
        HomeAssistantStateClient states)
        : base(services, "media_player")
    {
        _states = states;
    }

    public async Task<HomeAssistantMediaPlayerStatus> GetAsync(
        string entityId,
        CancellationToken cancellationToken = default)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, Domain, out var normalizedEntityId))
            throw new ArgumentException("A media-player entity identifier is required.", nameof(entityId));
        var state = await _states.GetAsync(normalizedEntityId, cancellationToken).ConfigureAwait(false);
        return HomeAssistantMediaPlayerStatus.FromState(HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId));
    }

    public async Task<IReadOnlyList<HomeAssistantMediaPlayerStatus>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var states = await _states.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return HomeAssistantEntityId.RequireResponseDomainStates(states, Domain)
            .Select(HomeAssistantMediaPlayerStatus.FromState)
            .ToArray();
    }

    public Task<IHomeAssistantSubscription> SubscribeAsync(
        Func<HomeAssistantMediaPlayerStateChange, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        if (handler is null)
        {
            throw new ArgumentNullException(nameof(handler));
        }

        return _states.SubscribeAsync(
            HomeAssistantStateFilter.ForDomains(Domain),
            (change, token) => handler(
                new HomeAssistantMediaPlayerStateChange(
                    change.EntityId,
                    ToStatus(change.PreviousState),
                    ToStatus(change.CurrentState)),
                token),
            cancellationToken);
    }

    public async Task<IReadOnlyList<HomeAssistantServiceCallResult>> SetAsync(
        HomeAssistantTarget target,
        HomeAssistantMediaPlayerOptions options,
        CancellationToken cancellationToken = default)
    {
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        cancellationToken.ThrowIfCancellationRequested();
        var frozenTarget = (target ?? throw new ArgumentNullException(nameof(target))).NormalizeForDomain(Domain);
        var power = options.Power;
        var playback = options.Playback;
        var volumePercent = options.VolumePercent;
        var muted = options.Muted;
        var source = NormalizeOptional(options.Source, nameof(options.Source));
        var soundMode = NormalizeOptional(options.SoundMode, nameof(options.SoundMode));
        var shuffle = options.Shuffle;
        var repeat = options.Repeat;
        var mediaContentId = NormalizeOptional(options.MediaContentId, nameof(options.MediaContentId));
        var mediaContentType = NormalizeOptional(options.MediaContentType, nameof(options.MediaContentType));
        var enqueue = options.Enqueue;
        var announce = options.Announce;
        var mediaExtra = options.MediaExtra;

        if (string.IsNullOrWhiteSpace(mediaContentId) != string.IsNullOrWhiteSpace(mediaContentType))
        {
            throw new ArgumentException("MediaContentId and MediaContentType must be supplied together.", nameof(options));
        }

        if ((enqueue.HasValue || announce == true || mediaExtra is not null)
            && string.IsNullOrWhiteSpace(mediaContentId))
        {
            throw new ArgumentException("Play-media options require media content.", nameof(options));
        }

        if (enqueue.HasValue && announce == true)
        {
            throw new ArgumentException("Enqueue and Announce cannot be combined by Home Assistant.", nameof(options));
        }

        var hasNonPowerOperation = playback.HasValue
            || volumePercent.HasValue
            || muted.HasValue
            || !string.IsNullOrWhiteSpace(source)
            || !string.IsNullOrWhiteSpace(soundMode)
            || shuffle.HasValue
            || repeat.HasValue
            || !string.IsNullOrWhiteSpace(mediaContentId);
        if ((power is HomeAssistantPowerAction.Off or HomeAssistantPowerAction.Toggle) && hasNonPowerOperation)
        {
            throw new ArgumentException("Power Off and Toggle cannot be combined with other media-player operations.", nameof(options));
        }

        if (!string.IsNullOrWhiteSpace(mediaContentId) && playback.HasValue)
        {
            throw new ArgumentException("Media content cannot be combined with a separate playback action.", nameof(options));
        }

        var powerAction = power.HasValue ? PowerAction(power.Value) : null;
        var playbackAction = playback.HasValue ? PlaybackAction(playback.Value) : null;
        var repeatMode = repeat.HasValue ? RepeatMode(repeat.Value) : null;
        var enqueueMode = enqueue.HasValue ? EnqueueMode(enqueue.Value) : null;
        var frozenMediaExtra = FreezeMediaExtra(mediaExtra, nameof(options.MediaExtra));
        if (powerAction is null && playbackAction is null && !hasNonPowerOperation)
        {
            throw new ArgumentException("At least one media-player value or action is required.", nameof(options));
        }

        var results = new List<HomeAssistantServiceCallResult>();
        if (powerAction is not null)
        {
            results.Add(await CallAsync(powerAction, frozenTarget, null, cancellationToken).ConfigureAwait(false));
        }

        if (volumePercent.HasValue)
        {
            results.Add(await CallAsync("volume_set", frozenTarget, call => call.WithData("volume_level", volumePercent.Value / 100d), cancellationToken).ConfigureAwait(false));
        }

        if (muted.HasValue)
        {
            results.Add(await CallAsync("volume_mute", frozenTarget, call => call.WithData("is_volume_muted", muted.Value), cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            results.Add(await CallAsync("select_source", frozenTarget, call => call.WithData("source", source), cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(soundMode))
        {
            results.Add(await SelectSoundModeAsync(frozenTarget, soundMode!, cancellationToken).ConfigureAwait(false));
        }

        if (shuffle.HasValue)
        {
            results.Add(await SetShuffleAsync(frozenTarget, shuffle.Value, cancellationToken).ConfigureAwait(false));
        }

        if (repeatMode is not null)
        {
            results.Add(await CallAsync(
                "repeat_set",
                frozenTarget,
                call => call.WithData("repeat", repeatMode),
                cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(mediaContentId))
        {
            results.Add(await PlayMediaAsync(
                frozenTarget,
                mediaContentId!,
                mediaContentType!,
                new HomeAssistantPlayMediaOptions
                {
                    Enqueue = enqueue,
                    Announce = announce,
                    Extra = frozenMediaExtra
                },
                cancellationToken).ConfigureAwait(false));
        }

        if (playbackAction is not null)
        {
            results.Add(await CallAsync(playbackAction, frozenTarget, null, cancellationToken).ConfigureAwait(false));
        }

        return results;
    }

    public Task<HomeAssistantServiceCallResult> SetVolumeAsync(
        HomeAssistantTarget target,
        double volumePercent,
        CancellationToken cancellationToken = default)
    {
        var validated = ControlValidation.Percent(volumePercent, nameof(volumePercent))!.Value;
        return CallAsync(
            "volume_set",
            target,
            call => call.WithData("volume_level", validated / 100d),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> StepVolumeAsync(
        HomeAssistantTarget target,
        HomeAssistantMediaVolumeStepAction action,
        CancellationToken cancellationToken = default)
    {
        var service = action switch
        {
            HomeAssistantMediaVolumeStepAction.Up => "volume_up",
            HomeAssistantMediaVolumeStepAction.Down => "volume_down",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unsupported volume-step action.")
        };
        return CallAsync(service, target, null, cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SeekAsync(
        HomeAssistantTarget target,
        TimeSpan position,
        CancellationToken cancellationToken = default)
    {
        var validated = ControlValidation.Duration(position, nameof(position))!.Value;
        return CallAsync(
            "media_seek",
            target,
            call => call.WithData("seek_position", validated.TotalSeconds),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetShuffleAsync(
        HomeAssistantTarget target,
        bool shuffle,
        CancellationToken cancellationToken = default)
    {
        return CallAsync(
            "shuffle_set",
            target,
            call => call.WithData("shuffle", shuffle),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetRepeatAsync(
        HomeAssistantTarget target,
        HomeAssistantMediaRepeatMode repeat,
        CancellationToken cancellationToken = default)
    {
        var value = RepeatMode(repeat);
        return CallAsync(
            "repeat_set",
            target,
            call => call.WithData("repeat", value),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SelectSoundModeAsync(
        HomeAssistantTarget target,
        string soundMode,
        CancellationToken cancellationToken = default)
    {
        var normalizedSoundMode = ControlValidation.Required(soundMode, nameof(soundMode));

        return CallAsync(
            "select_sound_mode",
            target,
            call => call.WithData("sound_mode", normalizedSoundMode),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> ClearPlaylistAsync(
        HomeAssistantTarget target,
        CancellationToken cancellationToken = default)
    {
        return CallAsync("clear_playlist", target, null, cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> JoinAsync(
        HomeAssistantTarget target,
        IEnumerable<string> groupMembers,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var members = ValidateEntityIds(groupMembers, nameof(groupMembers));
        return CallAsync(
            "join",
            target,
            call => call.WithData("group_members", members),
            cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> UnjoinAsync(
        HomeAssistantTarget target,
        CancellationToken cancellationToken = default)
    {
        return CallAsync("unjoin", target, null, cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> PlayMediaAsync(
        HomeAssistantTarget target,
        string mediaContentId,
        string mediaContentType,
        HomeAssistantPlayMediaOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        mediaContentId = ControlValidation.Required(mediaContentId, nameof(mediaContentId));
        mediaContentType = ControlValidation.Required(mediaContentType, nameof(mediaContentType));
        var enqueueOption = options?.Enqueue;
        var announce = options?.Announce;
        var extra = options?.Extra;
        if (enqueueOption.HasValue && announce == true)
        {
            throw new ArgumentException("Enqueue and Announce cannot be combined by Home Assistant.", nameof(options));
        }

        var enqueue = enqueueOption.HasValue
            ? EnqueueMode(enqueueOption.Value)
            : null;
        var frozenExtra = FreezeMediaExtra(extra, nameof(options));
        return CallAsync(
            "play_media",
            target,
            call =>
            {
                call.WithData("media_content_id", mediaContentId)
                    .WithData("media_content_type", mediaContentType);
                if (enqueue is not null)
                {
                    call.WithData("enqueue", enqueue);
                }

                if (announce.HasValue)
                {
                    call.WithData("announce", announce.Value);
                }

                if (frozenExtra is not null)
                {
                    call.WithData("extra", frozenExtra);
                }
            },
            cancellationToken);
    }

    internal static IReadOnlyDictionary<string, object?>? FreezeMediaExtra(
        IReadOnlyDictionary<string, object?>? extra,
        string parameterName)
    {
        if (extra is null)
        {
            return null;
        }

        return HomeAssistantJson.FreezeObject(extra, parameterName, "MediaExtra");
    }

    private static string? NormalizeOptional(string? value, string name)
        => value is null ? null : ControlValidation.Required(value, name);

    private static string PowerAction(HomeAssistantPowerAction value) => value switch
    {
        HomeAssistantPowerAction.On => "turn_on",
        HomeAssistantPowerAction.Off => "turn_off",
        HomeAssistantPowerAction.Toggle => "toggle",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported power action.")
    };

    private static string PlaybackAction(HomeAssistantMediaPlaybackAction value) => value switch
    {
        HomeAssistantMediaPlaybackAction.Play => "media_play",
        HomeAssistantMediaPlaybackAction.Pause => "media_pause",
        HomeAssistantMediaPlaybackAction.PlayPause => "media_play_pause",
        HomeAssistantMediaPlaybackAction.Stop => "media_stop",
        HomeAssistantMediaPlaybackAction.Next => "media_next_track",
        HomeAssistantMediaPlaybackAction.Previous => "media_previous_track",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported playback action.")
    };

    private static string RepeatMode(HomeAssistantMediaRepeatMode value) => value switch
    {
        HomeAssistantMediaRepeatMode.Off => "off",
        HomeAssistantMediaRepeatMode.One => "one",
        HomeAssistantMediaRepeatMode.All => "all",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported repeat mode.")
    };

    private static string EnqueueMode(HomeAssistantMediaEnqueueMode value) => value switch
    {
        HomeAssistantMediaEnqueueMode.Add => "add",
        HomeAssistantMediaEnqueueMode.Next => "next",
        HomeAssistantMediaEnqueueMode.Play => "play",
        HomeAssistantMediaEnqueueMode.Replace => "replace",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unsupported enqueue mode.")
    };

    private static IReadOnlyList<string> ValidateEntityIds(
        IEnumerable<string> entityIds,
        string parameterName)
    {
        if (entityIds is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        var values = entityIds.Select(value =>
        {
            return HomeAssistantEntityId.TryNormalizeForDomain(value, "media_player", out var normalized)
                ? normalized
                : null;
        }).ToArray();
        if (values.Length == 0
            || values.Any(value => value is null))
        {
            throw new ArgumentException(
                "At least one media_player entity identifier is required.",
                parameterName);
        }

        return values!;
    }

    private static HomeAssistantMediaPlayerStatus? ToStatus(HomeAssistantState? state)
    {
        return state is null
            ? null
            : HomeAssistantMediaPlayerStatus.FromState(HomeAssistantEntityId.RequireResponseDomain(state, "media_player"));
    }
}
