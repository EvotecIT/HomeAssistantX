using System.Management.Automation;
using HomeAssistantX.Media;

namespace HomeAssistantX.PowerShell;

/// <summary>Browses, searches, or resolves Home Assistant media sources and media-player libraries.</summary>
/// <example><summary>Browse the media-source root</summary><code>Get-HomeAssistantMedia</code></example>
/// <example><summary>Search a media player's library</summary><code>Get-HomeAssistantMedia -PlayerEntityId media_player.kitchen -Search dinner -MediaClass music</code></example>
[Cmdlet(VerbsCommon.Get, "HomeAssistantMedia", DefaultParameterSetName = SourcesSet)]
[OutputType(typeof(HomeAssistantMediaItem))]
[OutputType(typeof(HomeAssistantResolvedMedia))]
public sealed class GetHomeAssistantMediaCommand : HomeAssistantCmdlet
{
    private const string SourcesSet = "Sources";
    private const string SourceSearchSet = "SourceSearch";
    private const string ResolveSet = "Resolve";
    private const string PlayerSet = "Player";
    private const string PlayerSearchSet = "PlayerSearch";

    [Parameter(ParameterSetName = SourcesSet)]
    [Parameter(ParameterSetName = SourceSearchSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = ResolveSet)]
    [Parameter(ParameterSetName = PlayerSet)]
    [Parameter(ParameterSetName = PlayerSearchSet)]
    [ValidateNotNullOrEmpty] public string? MediaContentId { get; set; }
    [Parameter(ParameterSetName = PlayerSet)]
    [Parameter(ParameterSetName = PlayerSearchSet)]
    [ValidateNotNullOrEmpty] public string? MediaContentType { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = SourceSearchSet)]
    [Parameter(Mandatory = true, ParameterSetName = PlayerSearchSet)]
    [ValidateNotNullOrEmpty] public string? Search { get; set; }
    [Parameter(ParameterSetName = SourceSearchSet)]
    [Parameter(ParameterSetName = PlayerSearchSet)]
    [ValidateNotNullOrEmpty] public string[]? MediaClass { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = ResolveSet)][ValidateSwitchPresent] public SwitchParameter Resolve { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = PlayerSet)]
    [Parameter(Mandatory = true, ParameterSetName = PlayerSearchSet)]
    [ValidateNotNullOrEmpty] public string? PlayerEntityId { get; set; }
    [Parameter(ParameterSetName = ResolveSet)][ValidateRange(1, int.MaxValue)] public int? ExpiresInSeconds { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        switch (ParameterSetName)
        {
            case SourceSearchSet:
                WriteObject(await Client.Media.SearchSourcesAsync(Search!, MediaContentId, MediaClass, CancelToken).ConfigureAwait(false), true);
                break;
            case ResolveSet:
                WriteObject(await Client.Media.ResolveAsync(MediaContentId!, ExpiresInSeconds.HasValue ? TimeSpan.FromSeconds(ExpiresInSeconds.Value) : null, CancelToken).ConfigureAwait(false));
                break;
            case PlayerSet:
                WriteObject(await Client.Media.BrowsePlayerAsync(PlayerEntityId!, MediaContentType, MediaContentId, CancelToken).ConfigureAwait(false));
                break;
            case PlayerSearchSet:
                WriteObject(await Client.Media.SearchPlayerAsync(PlayerEntityId!, Search!, MediaContentType, MediaContentId, MediaClass, CancelToken).ConfigureAwait(false), true);
                break;
            default:
                WriteObject(await Client.Media.BrowseSourcesAsync(MediaContentId, CancelToken).ConfigureAwait(false));
                break;
        }
    }
}
