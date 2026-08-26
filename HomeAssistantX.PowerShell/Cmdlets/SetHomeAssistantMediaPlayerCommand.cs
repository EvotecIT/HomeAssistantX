using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Sets common media-player power, playback, volume, mute, source, and content values.</summary>
/// <example>
///   <summary>Set room volume and begin playback</summary>
///   <code>Set-HomeAssistantMediaPlayer -Area LivingRoom -VolumePercent 30 -Playback Play -WhatIf</code>
/// </example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantMediaPlayer", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantMediaPlayerCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Optional power action.</summary>
    [Parameter]
    public HomeAssistantPowerAction? Power { get; set; }

    /// <summary>Optional playback action.</summary>
    [Parameter]
    public HomeAssistantMediaPlaybackAction? Playback { get; set; }

    /// <summary>Volume from 0 through 100 percent.</summary>
    [Parameter]
    [ValidateRange(0d, 100d)]
    public double? VolumePercent { get; set; }

    /// <summary>Sets or clears mute.</summary>
    [Parameter]
    public bool? Muted { get; set; }

    /// <summary>Input source exposed by the target.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Source { get; set; }

    /// <summary>Content identifier passed to <c>media_player.play_media</c>.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? MediaContentId { get; set; }

    /// <summary>Content type paired with <see cref="MediaContentId"/>.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? MediaContentType { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        if (!Power.HasValue
            && !Playback.HasValue
            && !VolumePercent.HasValue
            && !Muted.HasValue
            && string.IsNullOrWhiteSpace(Source)
            && string.IsNullOrWhiteSpace(MediaContentId)
            && string.IsNullOrWhiteSpace(MediaContentType))
        {
            throw new ArgumentException("Specify at least one media-player value or action.");
        }

        if (string.IsNullOrWhiteSpace(MediaContentId) != string.IsNullOrWhiteSpace(MediaContentType))
        {
            throw new ArgumentException("MediaContentId and MediaContentType must be supplied together.");
        }

        var options = new HomeAssistantMediaPlayerOptions
        {
            Power = Power,
            Playback = Playback,
            VolumePercent = VolumePercent,
            Muted = Muted,
            Source = Source,
            MediaContentId = MediaContentId,
            MediaContentType = MediaContentType
        };
        var target = await ResolveTargetAsync("media_player").ConfigureAwait(false);
        if (ShouldProcess(target.Description, "Set media player values"))
        {
            WriteObject(await Client.Controls.MediaPlayers.SetAsync(target.Target, options, CancelToken).ConfigureAwait(false), true);
        }
    }
}
