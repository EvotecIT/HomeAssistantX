namespace HomeAssistantX.Services;

/// <summary>Identifies Home Assistant action names whose semantics are owned and typed by HomeAssistantX.</summary>
internal static class HomeAssistantStandardActionCatalog
{
    private static readonly HashSet<string> Actions = new(StringComparer.Ordinal)
    {
        "alarm_control_panel.alarm_arm_away", "alarm_control_panel.alarm_arm_custom_bypass", "alarm_control_panel.alarm_arm_home", "alarm_control_panel.alarm_arm_night", "alarm_control_panel.alarm_arm_vacation", "alarm_control_panel.alarm_disarm", "alarm_control_panel.alarm_trigger",
        "automation.trigger", "automation.turn_off", "automation.turn_on", "automation.toggle", "button.press", "input_button.press",
        "climate.set_fan_mode", "climate.set_humidity", "climate.set_hvac_mode", "climate.set_preset_mode", "climate.set_temperature", "climate.turn_off", "climate.turn_on",
        "cover.close_cover", "cover.close_cover_tilt", "cover.open_cover", "cover.open_cover_tilt", "cover.set_cover_position", "cover.set_cover_tilt_position", "cover.stop_cover", "cover.stop_cover_tilt", "cover.toggle", "cover.toggle_cover_tilt",
        "date.set_value", "datetime.set_value", "time.set_value",
        "fan.decrease_speed", "fan.increase_speed", "fan.oscillate", "fan.set_direction", "fan.set_percentage", "fan.set_preset_mode", "fan.turn_off", "fan.turn_on", "fan.toggle",
        "humidifier.set_humidity", "humidifier.set_mode", "humidifier.turn_off", "humidifier.turn_on", "humidifier.toggle",
        "input_boolean.turn_off", "input_boolean.turn_on", "input_datetime.set_datetime", "input_number.decrement", "input_number.increment", "input_number.set_value", "input_select.select_next", "input_select.select_option", "input_select.select_previous", "input_select.set_options", "input_text.set_value",
        "lawn_mower.dock", "lawn_mower.pause", "lawn_mower.start_mowing", "light.turn_off", "light.turn_on", "light.toggle", "lock.lock", "lock.open", "lock.unlock",
        "media_player.clear_playlist", "media_player.join", "media_player.media_next_track", "media_player.media_pause", "media_player.media_play", "media_player.media_play_pause", "media_player.media_previous_track", "media_player.media_seek", "media_player.media_stop", "media_player.play_media", "media_player.repeat_set", "media_player.select_sound_mode", "media_player.select_source", "media_player.shuffle_set", "media_player.turn_off", "media_player.turn_on", "media_player.toggle", "media_player.unjoin", "media_player.volume_down", "media_player.volume_mute", "media_player.volume_set", "media_player.volume_up",
        "number.set_value", "select.select_next", "select.select_option", "select.select_previous", "text.set_value",
        "notify.send_message", "persistent_notification.create", "persistent_notification.dismiss", "persistent_notification.dismiss_all", "recorder.disable", "recorder.enable", "recorder.purge", "recorder.purge_entities",
        "remote.delete_command", "remote.learn_command", "remote.send_command", "remote.turn_off", "remote.turn_on", "remote.toggle", "scene.turn_on", "script.toggle", "script.turn_off", "script.turn_on",
        "siren.toggle", "siren.turn_off", "siren.turn_on", "switch.toggle", "switch.turn_off", "switch.turn_on", "update.install",
        "vacuum.clean_area", "vacuum.clean_spot", "vacuum.locate", "vacuum.pause", "vacuum.return_to_base", "vacuum.send_command", "vacuum.set_fan_speed", "vacuum.start", "vacuum.stop",
        "valve.close_valve", "valve.open_valve", "valve.set_valve_position", "valve.stop_valve", "valve.toggle",
        "water_heater.set_away_mode", "water_heater.set_operation_mode", "water_heater.set_temperature", "water_heater.turn_off", "water_heater.turn_on", "weather.get_forecasts"
    };

    internal static bool IsKnown(string domain, string action) => Actions.Contains(domain + "." + action);
}
