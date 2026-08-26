using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

/// <summary>Invokes common Home Assistant media-player actions with typed values.</summary>
public sealed class HomeAssistantMediaPlayerClient : HomeAssistantControlClientBase
{
    internal HomeAssistantMediaPlayerClient(HomeAssistantServiceClient services) : base(services, "media_player") { }

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

        var hasNonPowerOperation = options.Playback.HasValue
            || options.VolumePercent.HasValue
            || options.Muted.HasValue
            || !string.IsNullOrWhiteSpace(options.Source)
            || !string.IsNullOrWhiteSpace(options.MediaContentId);
        if ((options.Power is HomeAssistantPowerAction.Off or HomeAssistantPowerAction.Toggle) && hasNonPowerOperation)
        {
            throw new ArgumentException("Power Off and Toggle cannot be combined with other media-player operations.", nameof(options));
        }

        var powerAction = options.Power.HasValue ? PowerAction(options.Power.Value) : null;
        var playbackAction = options.Playback.HasValue ? PlaybackAction(options.Playback.Value) : null;
        if (powerAction is null && playbackAction is null && !hasNonPowerOperation)
        {
            throw new ArgumentException("At least one media-player value or action is required.", nameof(options));
        }

        var results = new List<HomeAssistantServiceCallResult>();
        if (powerAction is not null)
        {
            results.Add(await CallAsync(powerAction, target, null, cancellationToken).ConfigureAwait(false));
        }

        if (playbackAction is not null)
        {
            results.Add(await CallAsync(playbackAction, target, null, cancellationToken).ConfigureAwait(false));
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

        if (!string.IsNullOrWhiteSpace(options.MediaContentId))
        {
            results.Add(await CallAsync("play_media", target, call => call
                .WithData("media_content_id", options.MediaContentId)
                .WithData("media_content_type", options.MediaContentType), cancellationToken).ConfigureAwait(false));
        }

        return results;
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
}
