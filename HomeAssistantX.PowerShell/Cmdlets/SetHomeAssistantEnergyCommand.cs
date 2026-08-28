using System.Management.Automation;
using System.Text.Json;
using HomeAssistantX.Energy;
using HomeAssistantX.Protocol;

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
        CancelToken.ThrowIfCancellationRequested();
        var update = new HomeAssistantEnergyPreferencesUpdate
        {
            EnergySources = await ParseArrayAsync(EnergySourcesJson, nameof(EnergySourcesJson), CancelToken).ConfigureAwait(false),
            DeviceConsumption = await ParseArrayAsync(DeviceConsumptionJson, nameof(DeviceConsumptionJson), CancelToken).ConfigureAwait(false),
            DeviceConsumptionWater = await ParseArrayAsync(WaterConsumptionJson, nameof(WaterConsumptionJson), CancelToken).ConfigureAwait(false)
        };
        if (update.EnergySources is null && update.DeviceConsumption is null && update.DeviceConsumptionWater is null)
            throw new ArgumentException("At least one Energy preference JSON array is required.");
        _ = update.ToPayload(CancelToken);
        if (!ShouldProcess(ConnectionDisplayName, "Update Energy dashboard preferences")) return;
        var result = await Client.Energy.SavePreferencesAsync(update, CancelToken).ConfigureAwait(false);
        if (PassThru) WriteObject(result);
    }

    private static async Task<JsonElement?> ParseArrayAsync(
        string? json,
        string parameterName,
        CancellationToken cancellationToken)
    {
        if (json is null) return null;
        try
        {
            using var document = await HomeAssistantJson.ParseDocumentAsync(json, cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new ArgumentException("The value must be a JSON array.", parameterName);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (item.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Every Energy preference entry must be a JSON object.", parameterName);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return await HomeAssistantJson.SnapshotResponseAsync(
                document.RootElement,
                "The Energy preference JSON could not be copied.",
                cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new ArgumentException("The value must be valid JSON.", parameterName, ex);
        }
    }
}
