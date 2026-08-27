using HomeAssistantX.Models;
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
        return HomeAssistantMediaPlayerStatus.FromState(
            await _states.GetAsync(entityId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<IReadOnlyList<HomeAssistantMediaPlayerStatus>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var states = await _states.GetAllAsync(cancellationToken).ConfigureAwait(false);
        return states
            .Where(state => string.Equals(state.Domain, Domain, StringComparison.OrdinalIgnoreCase))
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

        if (string.IsNullOrWhiteSpace(options.MediaContentId) != string.IsNullOrWhiteSpace(options.MediaContentType))
        {
            throw new ArgumentException("MediaContentId and MediaContentType must be supplied together.", nameof(options));
        }

        if ((options.Enqueue.HasValue || options.Announce.HasValue || options.MediaExtra is not null)
            && string.IsNullOrWhiteSpace(options.MediaContentId))
        {
            throw new ArgumentException("Play-media options require media content.", nameof(options));
        }

        if (options.Enqueue.HasValue && options.Announce.HasValue)
        {
            throw new ArgumentException("Enqueue and Announce cannot be combined by Home Assistant.", nameof(options));
        }

        var hasNonPowerOperation = options.Playback.HasValue
            || options.VolumePercent.HasValue
            || options.Muted.HasValue
            || !string.IsNullOrWhiteSpace(options.Source)
            || !string.IsNullOrWhiteSpace(options.SoundMode)
            || options.Shuffle.HasValue
            || options.Repeat.HasValue
            || !string.IsNullOrWhiteSpace(options.MediaContentId);
        if ((options.Power is HomeAssistantPowerAction.Off or HomeAssistantPowerAction.Toggle) && hasNonPowerOperation)
        {
            throw new ArgumentException("Power Off and Toggle cannot be combined with other media-player operations.", nameof(options));
        }

        if (!string.IsNullOrWhiteSpace(options.MediaContentId) && options.Playback.HasValue)
        {
            throw new ArgumentException("Media content cannot be combined with a separate playback action.", nameof(options));
        }

        var powerAction = options.Power.HasValue ? PowerAction(options.Power.Value) : null;
        var playbackAction = options.Playback.HasValue ? PlaybackAction(options.Playback.Value) : null;
        var repeatMode = options.Repeat.HasValue ? RepeatMode(options.Repeat.Value) : null;
        var enqueueMode = options.Enqueue.HasValue ? EnqueueMode(options.Enqueue.Value) : null;
        if (powerAction is null && playbackAction is null && !hasNonPowerOperation)
        {
            throw new ArgumentException("At least one media-player value or action is required.", nameof(options));
        }

        var results = new List<HomeAssistantServiceCallResult>();
        if (powerAction is not null)
        {
            results.Add(await CallAsync(powerAction, target, null, cancellationToken).ConfigureAwait(false));
        }

        if (options.VolumePercent.HasValue)
        {
            results.Add(await CallAsync("volume_set", target, call => call.WithData("volume_level", options.VolumePercent.Value / 100d), cancellationToken).ConfigureAwait(false));
        }

        if (options.Muted.HasValue)
        {
            results.Add(await CallAsync("volume_mute", target, call => call.WithData("is_volume_muted", options.Muted.Value), cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(options.Source))
        {
            results.Add(await CallAsync("select_source", target, call => call.WithData("source", options.Source), cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(options.SoundMode))
        {
            results.Add(await SelectSoundModeAsync(target, options.SoundMode!, cancellationToken).ConfigureAwait(false));
        }

        if (options.Shuffle.HasValue)
        {
            results.Add(await SetShuffleAsync(target, options.Shuffle.Value, cancellationToken).ConfigureAwait(false));
        }

        if (repeatMode is not null)
        {
            results.Add(await CallAsync(
                "repeat_set",
                target,
                call => call.WithData("repeat", repeatMode),
                cancellationToken).ConfigureAwait(false));
        }

        if (!string.IsNullOrWhiteSpace(options.MediaContentId))
        {
            results.Add(await PlayMediaAsync(
                target,
                options.MediaContentId!,
                options.MediaContentType!,
                new HomeAssistantPlayMediaOptions
                {
                    Enqueue = options.Enqueue,
                    Announce = options.Announce,
                    Extra = options.MediaExtra
                },
                cancellationToken).ConfigureAwait(false));
        }

        if (playbackAction is not null)
        {
            results.Add(await CallAsync(playbackAction, target, null, cancellationToken).ConfigureAwait(false));
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
        if (string.IsNullOrWhiteSpace(soundMode))
        {
            throw new ArgumentException("A sound mode is required.", nameof(soundMode));
        }

        return CallAsync(
            "select_sound_mode",
            target,
            call => call.WithData("sound_mode", soundMode),
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
        if (string.IsNullOrWhiteSpace(mediaContentId))
        {
            throw new ArgumentException("A media content identifier is required.", nameof(mediaContentId));
        }

        if (string.IsNullOrWhiteSpace(mediaContentType))
        {
            throw new ArgumentException("A media content type is required.", nameof(mediaContentType));
        }

        if (options?.Enqueue.HasValue == true && options.Announce.HasValue)
        {
            throw new ArgumentException("Enqueue and Announce cannot be combined by Home Assistant.", nameof(options));
        }

        var enqueue = options?.Enqueue.HasValue == true
            ? EnqueueMode(options.Enqueue.Value)
            : null;
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

                if (options?.Announce.HasValue == true)
                {
                    call.WithData("announce", options.Announce.Value);
                }

                if (options?.Extra is not null)
                {
                    call.WithData("extra", options.Extra);
                }
            },
            cancellationToken);
    }

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

        var values = entityIds.Select(value => value?.Trim()).ToArray();
        if (values.Length == 0
            || values.Any(value => string.IsNullOrWhiteSpace(value)
                || !value!.StartsWith("media_player.", StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException(
                "At least one media_player entity identifier is required.",
                parameterName);
        }

        return values!;
    }

    private static HomeAssistantMediaPlayerStatus? ToStatus(HomeAssistantState? state)
    {
        return state is null ? null : HomeAssistantMediaPlayerStatus.FromState(state);
    }
}
