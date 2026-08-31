using HomeAssistantX.Configuration;
using HomeAssistantX.Services;
using HomeAssistantX.States;

namespace HomeAssistantX.Controls;

/// <summary>Provides typed controls for commonly used Home Assistant domains.</summary>
public sealed class HomeAssistantControlsClient
{
    internal HomeAssistantControlsClient(
        HomeAssistantServiceClient services,
        HomeAssistantStateClient states,
        HomeAssistantClientOptions options)
    {
        Lights = new HomeAssistantLightClient(services);
        Switches = new HomeAssistantSwitchClient(services);
        Climate = new HomeAssistantClimateClient(services);
        Covers = new HomeAssistantCoverClient(services);
        MediaPlayers = new HomeAssistantMediaPlayerClient(services, states);
        Remotes = new HomeAssistantRemoteClient(services, states, options);
        Locks = new HomeAssistantLockClient(services);
        Routines = new HomeAssistantRoutineClient(services);
        Fans = new HomeAssistantFanClient(services);
        Valves = new HomeAssistantValveClient(services);
        Vacuums = new HomeAssistantVacuumClient(services);
        LawnMowers = new HomeAssistantLawnMowerClient(services);
        Alarms = new HomeAssistantAlarmClient(services);
        Sirens = new HomeAssistantSirenClient(services);
        Humidifiers = new HomeAssistantHumidifierClient(services);
        WaterHeaters = new HomeAssistantWaterHeaterClient(services);
        Helpers = new HomeAssistantHelperClient(services);
    }

    public HomeAssistantLightClient Lights { get; }

    public HomeAssistantSwitchClient Switches { get; }

    public HomeAssistantClimateClient Climate { get; }

    public HomeAssistantCoverClient Covers { get; }

    public HomeAssistantMediaPlayerClient MediaPlayers { get; }

    public HomeAssistantRemoteClient Remotes { get; }

    public HomeAssistantLockClient Locks { get; }

    public HomeAssistantRoutineClient Routines { get; }

    public HomeAssistantFanClient Fans { get; }

    public HomeAssistantValveClient Valves { get; }

    public HomeAssistantVacuumClient Vacuums { get; }

    public HomeAssistantLawnMowerClient LawnMowers { get; }

    public HomeAssistantAlarmClient Alarms { get; }

    public HomeAssistantSirenClient Sirens { get; }

    public HomeAssistantHumidifierClient Humidifiers { get; }

    public HomeAssistantWaterHeaterClient WaterHeaters { get; }

    public HomeAssistantHelperClient Helpers { get; }
}
