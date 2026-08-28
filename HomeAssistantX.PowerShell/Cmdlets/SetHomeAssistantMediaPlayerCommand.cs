using System.Collections;
using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Models;
using HomeAssistantX.Services;

namespace HomeAssistantX.PowerShell;

/// <summary>Controls media-player power, playback, volume, source, grouping, queueing, and content.</summary>
/// <example>
///   <summary>Set room volume and begin playback</summary>
///   <code>Set-HomeAssistantMediaPlayer -Area LivingRoom -VolumePercent 30 -Playback Play -WhatIf</code>
/// </example>
/// <example>
///   <summary>Play an announcement without guessing the Home Assistant payload shape</summary>
///   <code>Set-HomeAssistantMediaPlayer -Entity media_player.kitchen -MediaContentId 'media-source://media_source/local/dinner.mp3' -MediaContentType music -Announce</code>
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

    /// <summary>Raises or lowers the volume by the target's native step.</summary>
    [Parameter]
    public HomeAssistantMediaVolumeStepAction? VolumeStep { get; set; }

    /// <summary>Sets or clears mute.</summary>
    [Parameter]
    public bool? Muted { get; set; }

    /// <summary>Input source exposed by the target.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? Source { get; set; }

    /// <summary>Sound mode exposed by the target.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? SoundMode { get; set; }

    /// <summary>Enables or disables shuffle.</summary>
    [Parameter]
    public bool? Shuffle { get; set; }

    /// <summary>Sets the repeat mode.</summary>
    [Parameter]
    public HomeAssistantMediaRepeatMode? Repeat { get; set; }

    /// <summary>Seeks to an absolute position in seconds.</summary>
    [Parameter]
    public double? SeekSeconds { get; set; }

    /// <summary>Clears the target playlist.</summary>
    [Parameter]
    public SwitchParameter ClearPlaylist { get; set; }

    /// <summary>Media-player entity identifiers to join to the selected group leader.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string[]? JoinMember { get; set; }

    /// <summary>Removes the selected media players from their groups.</summary>
    [Parameter]
    public SwitchParameter Unjoin { get; set; }

    /// <summary>Content identifier passed to <c>media_player.play_media</c>.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? MediaContentId { get; set; }

    /// <summary>Content type paired with <see cref="MediaContentId"/>.</summary>
    [Parameter]
    [ValidateNotNullOrEmpty]
    public string? MediaContentType { get; set; }

    /// <summary>Controls where content is placed in the target's queue.</summary>
    [Parameter]
    public HomeAssistantMediaEnqueueMode? Enqueue { get; set; }

    /// <summary>Requests announcement playback. Home Assistant does not allow this with <see cref="Enqueue"/>.</summary>
    [Parameter]
    public SwitchParameter Announce { get; set; }

    /// <summary>Provider-specific extra play-media values. Use only when the integration requires them.</summary>
    [Parameter]
    public Hashtable? MediaExtra { get; set; }

    protected override async Task ProcessTargetRecordAsync()
    {
        ValidateOptionalEnum(Power, nameof(Power));
        ValidateOptionalEnum(Playback, nameof(Playback));
        ValidateOptionalEnum(VolumeStep, nameof(VolumeStep));
        ValidateOptionalEnum(Repeat, nameof(Repeat));
        ValidateOptionalEnum(Enqueue, nameof(Enqueue));
        ValidateFinitePercent(VolumePercent, nameof(VolumePercent));
        var seekPosition = ToTimeSpan(SeekSeconds, nameof(SeekSeconds));
        var joinMembers = JoinMember is null
            ? null
            : HomeAssistantMediaPlayerClient.NormalizeEntityIds(JoinMember, nameof(JoinMember), CancelToken);
        var mediaExtra = ConvertExtra(MediaExtra, CancelToken);

        if (MediaContentId is not null && string.IsNullOrWhiteSpace(MediaContentId))
        {
            throw new ArgumentException("A supplied MediaContentId cannot be empty.", nameof(MediaContentId));
        }

        if (MediaContentType is not null && string.IsNullOrWhiteSpace(MediaContentType))
        {
            throw new ArgumentException("A supplied MediaContentType cannot be empty.", nameof(MediaContentType));
        }

        if (Source is not null && string.IsNullOrWhiteSpace(Source))
        {
            throw new ArgumentException("A supplied Source cannot be empty.", nameof(Source));
        }

        if (SoundMode is not null && string.IsNullOrWhiteSpace(SoundMode))
        {
            throw new ArgumentException("A supplied SoundMode cannot be empty.", nameof(SoundMode));
        }

        var hasContent = !string.IsNullOrWhiteSpace(MediaContentId)
            || !string.IsNullOrWhiteSpace(MediaContentType);
        if (!HasAnyOperation(hasContent))
        {
            throw new ArgumentException("Specify at least one media-player value or action.");
        }

        if (string.IsNullOrWhiteSpace(MediaContentId) != string.IsNullOrWhiteSpace(MediaContentType))
        {
            throw new ArgumentException("MediaContentId and MediaContentType must be supplied together.");
        }

        if ((Enqueue.HasValue || Announce.IsPresent || MediaExtra is not null) && !hasContent)
        {
            throw new ArgumentException("Enqueue, Announce, and MediaExtra require MediaContentId and MediaContentType.");
        }

        if (Enqueue.HasValue && Announce.IsPresent)
        {
            throw new ArgumentException("Enqueue and Announce cannot be combined by Home Assistant.");
        }

        if (VolumePercent.HasValue && VolumeStep.HasValue)
        {
            throw new ArgumentException("VolumePercent and VolumeStep cannot be combined.");
        }

        if (JoinMember is not null && Unjoin.IsPresent)
        {
            throw new ArgumentException("JoinMember and Unjoin cannot be combined.");
        }

        if (hasContent && (Playback.HasValue || SeekSeconds.HasValue))
        {
            throw new ArgumentException("Media content cannot be combined with Playback or SeekSeconds.");
        }

        var hasNonPowerOperation = Playback.HasValue
            || VolumePercent.HasValue
            || VolumeStep.HasValue
            || Muted.HasValue
            || !string.IsNullOrWhiteSpace(Source)
            || !string.IsNullOrWhiteSpace(SoundMode)
            || Shuffle.HasValue
            || Repeat.HasValue
            || SeekSeconds.HasValue
            || ClearPlaylist.IsPresent
            || JoinMember is not null
            || Unjoin.IsPresent
            || hasContent;
        if (Power is HomeAssistantPowerAction.Off or HomeAssistantPowerAction.Toggle && hasNonPowerOperation)
        {
            throw new ArgumentException("Power Off and Toggle cannot be combined with other media-player operations.");
        }

        var target = await ResolveTargetAsync("media_player").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, "Set media player values"))
        {
            return;
        }

        var settings = HasSettings()
            ? new HomeAssistantMediaPlayerOptions
            {
                Power = Power,
                VolumePercent = VolumePercent,
                Muted = Muted,
                Source = Source,
                SoundMode = SoundMode,
                Shuffle = Shuffle,
                Repeat = Repeat
            }
            : null;
        var results = await Client.Controls.MediaPlayers.ExecuteSequenceAsync(
            target.Target,
            settings,
            VolumeStep,
            ClearPlaylist.IsPresent,
            joinMembers,
            Unjoin.IsPresent,
            hasContent ? MediaContentId : null,
            hasContent ? MediaContentType : null,
            hasContent
                ? new HomeAssistantPlayMediaOptions
                {
                    Enqueue = Enqueue,
                    Announce = Announce.IsPresent ? true : null,
                    Extra = mediaExtra
                }
                : null,
            seekPosition,
            Playback,
            CancelToken).ConfigureAwait(false);

        WriteObject(results, true);
    }

    private bool HasAnyOperation(bool hasContent)
    {
        return Power.HasValue
            || Playback.HasValue
            || VolumePercent.HasValue
            || VolumeStep.HasValue
            || Muted.HasValue
            || !string.IsNullOrWhiteSpace(Source)
            || !string.IsNullOrWhiteSpace(SoundMode)
            || Shuffle.HasValue
            || Repeat.HasValue
            || SeekSeconds.HasValue
            || ClearPlaylist.IsPresent
            || JoinMember is not null
            || Unjoin.IsPresent
            || hasContent
            || Enqueue.HasValue
            || Announce.IsPresent
            || MediaExtra is not null;
    }

    private bool HasSettings()
    {
        return Power.HasValue
            || VolumePercent.HasValue
            || Muted.HasValue
            || !string.IsNullOrWhiteSpace(Source)
            || !string.IsNullOrWhiteSpace(SoundMode)
            || Shuffle.HasValue
            || Repeat.HasValue;
    }

    private static IReadOnlyDictionary<string, object?>? ConvertExtra(Hashtable? values, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (values is null)
        {
            return null;
        }

        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (DictionaryEntry entry in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (entry.Key is not string key || string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("MediaExtra keys must be non-empty strings.", nameof(MediaExtra));
            }

            result.Add(key, entry.Value);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return HomeAssistantMediaPlayerClient.FreezeMediaExtra(result, nameof(MediaExtra), cancellationToken);
    }

    private static TimeSpan? ToTimeSpan(double? value, string name)
    {
        if (!value.HasValue)
        {
            return null;
        }

        try
        {
            if (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0d)
            {
                throw new OverflowException();
            }

            var ticks = decimal.Round(
                (decimal)value.Value * TimeSpan.TicksPerSecond,
                0,
                MidpointRounding.AwayFromZero);
            if (ticks > long.MaxValue)
            {
                throw new OverflowException();
            }

            return TimeSpan.FromTicks((long)ticks);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(name, "The value must be a finite non-negative number of seconds within the TimeSpan range.");
        }
    }

    private static void ValidateFinitePercent(double? value, string name)
    {
        if (value.HasValue
            && (double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value < 0d || value.Value > 100d))
        {
            throw new ArgumentOutOfRangeException(name, "The value must be a finite percentage from zero through 100.");
        }
    }

    private static void ValidateOptionalEnum<T>(T? value, string name)
        where T : struct, Enum
    {
        if (value.HasValue && !Enum.IsDefined(typeof(T), value.Value))
        {
            throw new ArgumentOutOfRangeException(name, value.Value, $"Unsupported {name} value.");
        }
    }
}
