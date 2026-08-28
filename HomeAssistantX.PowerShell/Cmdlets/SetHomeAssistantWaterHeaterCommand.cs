using System.Management.Automation;
using HomeAssistantX.Controls;
using HomeAssistantX.Services;
namespace HomeAssistantX.PowerShell;

/// <summary>Sets water-heater power, temperature, operation mode, or away mode.</summary>
/// <example><summary>Set a water heater temperature</summary><code>Set-HomeAssistantWaterHeater -Entity water_heater.tank -Temperature 52</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantWaterHeater", SupportsShouldProcess = true, DefaultParameterSetName = EntityParameterSet)]
[OutputType(typeof(HomeAssistantServiceCallResult))]
public sealed class SetHomeAssistantWaterHeaterCommand : HomeAssistantTargetCmdlet
{
    /// <summary>Turns the water heater on or off.</summary>
    [Parameter] public HomeAssistantWaterHeaterAction? Action { get; set; }
    /// <summary>Target temperature in the entity's configured unit.</summary>
    [Parameter] public double? Temperature { get; set; }
    /// <summary>Operation mode, optionally combined with Temperature.</summary>
    [Parameter][ValidateNotNullOrEmpty] public string? OperationMode { get; set; }
    /// <summary>Enables or disables away mode.</summary>
    [Parameter] public bool? AwayMode { get; set; }
    protected override async Task ProcessTargetRecordAsync()
    {
        if (Temperature.HasValue && (double.IsNaN(Temperature.Value) || double.IsInfinity(Temperature.Value))) throw new ArgumentOutOfRangeException(nameof(Temperature));
        if (OperationMode is not null)
        {
            if (string.IsNullOrWhiteSpace(OperationMode)) throw new ArgumentException("A non-empty operation mode is required.", nameof(OperationMode));
        }
        var count = (Action.HasValue ? 1 : 0) + (Temperature.HasValue ? 1 : 0) + (!Temperature.HasValue && OperationMode is not null ? 1 : 0) + (AwayMode.HasValue ? 1 : 0);
        if (count != 1) throw new ArgumentException("Specify exactly one water-heater operation; OperationMode may accompany Temperature.");
        if (Action.HasValue && !Enum.IsDefined(typeof(HomeAssistantWaterHeaterAction), Action.Value)) throw new ArgumentOutOfRangeException(nameof(Action));
        var target = await ResolveTargetAsync("water_heater").ConfigureAwait(false);
        if (!ShouldProcess(target.Description, "Set water heater")) return;
        WriteObject(Action.HasValue ? await Client.Controls.WaterHeaters.ActAsync(target.Target, Action.Value, CancelToken).ConfigureAwait(false)
            : Temperature.HasValue ? await Client.Controls.WaterHeaters.SetTemperatureAsync(target.Target, Temperature.Value, OperationMode, CancelToken).ConfigureAwait(false)
            : AwayMode.HasValue ? await Client.Controls.WaterHeaters.SetAwayModeAsync(target.Target, AwayMode.Value, CancelToken).ConfigureAwait(false)
            : await Client.Controls.WaterHeaters.SetOperationModeAsync(target.Target, OperationMode!, CancelToken).ConfigureAwait(false));
    }
}
