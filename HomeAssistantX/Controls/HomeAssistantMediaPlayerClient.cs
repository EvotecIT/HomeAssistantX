using HomeAssistantX.Services;

namespace HomeAssistantX.Controls;

/// <summary>Invokes common Home Assistant media-player actions with typed values.</summary>
public sealed class HomeAssistantMediaPlayerClient : HomeAssistantControlClientBase
{
    internal HomeAssistantMediaPlayerClient(HomeAssistantServiceClient services) : base(services, "media_player") { }

    public async Task<IReadOnlyList<HomeAssistantServiceCallResult>> SetAsync(HomeAssistantTarget target, HomeAssistantMediaPlayerOptions options, CancellationToken cancellationToken = default)
    {
        if (options is null) throw new ArgumentNullException(nameof(options));
        if (string.IsNullOrWhiteSpace(options.MediaContentId) != string.IsNullOrWhiteSpace(options.MediaContentType))
        {
            throw new ArgumentException("MediaContentId and MediaContentType must be supplied together.", nameof(options));
        }

        var results = new List<HomeAssistantServiceCallResult>();
        if (options.Power.HasValue) results.Add(await CallAsync(PowerAction(options.Power.Value), target, null, cancellationToken).ConfigureAwait(false));
        if (options.Playback.HasValue) results.Add(await CallAsync(PlaybackAction(options.Playback.Value), target, null, cancellationToken).ConfigureAwait(false));
        if (options.VolumePercent.HasValue) results.Add(await CallAsync("volume_set", target, call => call.WithData("volume_level", options.VolumePercent.Value / 100d), cancellationToken).ConfigureAwait(false));
        if (options.Muted.HasValue) results.Add(await CallAsync("volume_mute", target, call => call.WithData("is_volume_muted", options.Muted.Value), cancellationToken).ConfigureAwait(false));
        if (!string.IsNullOrWhiteSpace(options.Source)) results.Add(await CallAsync("select_source", target, call => call.WithData("source", options.Source), cancellationToken).ConfigureAwait(false));
        if (!string.IsNullOrWhiteSpace(options.MediaContentId) || !string.IsNullOrWhiteSpace(options.MediaContentType))
        {
            results.Add(await CallAsync("play_media", target, call => call.WithData("media_content_id", options.MediaContentId).WithData("media_content_type", options.MediaContentType), cancellationToken).ConfigureAwait(false));
        }

        if (results.Count == 0) throw new ArgumentException("At least one media-player value or action is required.", nameof(options));
        return results;
    }

    private static string PowerAction(HomeAssistantPowerAction value) => value switch { HomeAssistantPowerAction.On => "turn_on", HomeAssistantPowerAction.Off => "turn_off", _ => "toggle" };

    private static string PlaybackAction(HomeAssistantMediaPlaybackAction value) => value switch { HomeAssistantMediaPlaybackAction.Play => "media_play", HomeAssistantMediaPlaybackAction.Pause => "media_pause", HomeAssistantMediaPlaybackAction.PlayPause => "media_play_pause", HomeAssistantMediaPlaybackAction.Stop => "media_stop", HomeAssistantMediaPlaybackAction.Next => "media_next_track", _ => "media_previous_track" };
}
