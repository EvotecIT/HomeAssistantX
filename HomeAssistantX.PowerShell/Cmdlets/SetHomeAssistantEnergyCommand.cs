using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Energy;

namespace HomeAssistantX.PowerShell;

/// <summary>Updates one or more Energy dashboard preference collections.</summary>
/// <example><summary>Preview replacing device consumption</summary><code>Set-HomeAssistantEnergy -DeviceConsumptionJson '[{"stat_consumption":"sensor.ev_energy"}]' -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantEnergy", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
[OutputType(typeof(HomeAssistantEnergyPreferences))]
public sealed class SetHomeAssistantEnergyCommand : HomeAssistantCmdlet
{
    [Parameter] public string? EnergySourcesJson { get; set; }
    [Parameter] public string? DeviceConsumptionJson { get; set; }
    [Parameter] public string? WaterConsumptionJson { get; set; }
    [Parameter] public SwitchParameter PassThru { get; set; }

    protected override async Task ProcessRecordAsync()
    {
        var update = new HomeAssistantEnergyPreferencesUpdate
        {
            EnergySources = ParseArray(EnergySourcesJson, nameof(EnergySourcesJson)),
            DeviceConsumption = ParseArray(DeviceConsumptionJson, nameof(DeviceConsumptionJson)),
            DeviceConsumptionWater = ParseArray(WaterConsumptionJson, nameof(WaterConsumptionJson))
        };
        if (update.EnergySources is null && update.DeviceConsumption is null && update.DeviceConsumptionWater is null)
            throw new ArgumentException("At least one Energy preference JSON array is required.");
        if (!ShouldProcess(ConnectionDisplayName, "Update Energy dashboard preferences")) return;
        var result = await Client.Energy.SavePreferencesAsync(update, CancelToken).ConfigureAwait(false);
        if (PassThru) WriteObject(result);
    }

    private static JsonElement? ParseArray(string? json, string parameterName)
    {
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("The value must be a JSON array.", parameterName);
        if (document.RootElement.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.Object))
            throw new ArgumentException("Every Energy preference entry must be a JSON object.", parameterName);
        return document.RootElement.Clone();
    }
}
