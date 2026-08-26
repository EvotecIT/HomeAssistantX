using HomeAssistantX.Services;
using HomeAssistantX.States;

namespace HomeAssistantX.Controls;

/// <summary>Provides typed controls for commonly used Home Assistant domains.</summary>
public sealed class HomeAssistantControlsClient
{
    internal HomeAssistantControlsClient(
        HomeAssistantServiceClient services,
        HomeAssistantStateClient states)
    {
        Lights = new HomeAssistantLightClient(services);
        Switches = new HomeAssistantSwitchClient(services);
        Climate = new HomeAssistantClimateClient(services);
        Covers = new HomeAssistantCoverClient(services);
        MediaPlayers = new HomeAssistantMediaPlayerClient(services, states);
        Remotes = new HomeAssistantRemoteClient(services, states);
        Locks = new HomeAssistantLockClient(services);
    }

    public HomeAssistantLightClient Lights { get; }

    public HomeAssistantSwitchClient Switches { get; }

    public HomeAssistantClimateClient Climate { get; }

    public HomeAssistantCoverClient Covers { get; }

    public HomeAssistantMediaPlayerClient MediaPlayers { get; }

    public HomeAssistantRemoteClient Remotes { get; }

    public HomeAssistantLockClient Locks { get; }
}
