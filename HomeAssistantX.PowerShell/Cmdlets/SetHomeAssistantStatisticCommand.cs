using System.Management.Automation;
using HomeAssistantX.Recorder;

namespace HomeAssistantX.PowerShell;

/// <summary>Updates metadata, converts units, adjusts sums, or imports Recorder statistics.</summary>
/// <example><summary>Preview a sum correction</summary><code>Set-HomeAssistantStatistic -StatisticId sensor.grid_energy -AdjustSum 1.25 -StartTime (Get-Date) -Unit kWh -WhatIf</code></example>
[Cmdlet(VerbsCommon.Set, "HomeAssistantStatistic", SupportsShouldProcess = true, ConfirmImpact = ConfirmImpact.High)]
public sealed class SetHomeAssistantStatisticCommand : HomeAssistantCmdlet
{
    private const string MetadataSet = "Metadata";
    private const string UnitSet = "Unit";
    private const string AdjustSet = "AdjustSum";
    private const string ImportSet = "Import";
    private readonly List<HomeAssistantStatisticImportRow> _importRows = new();

    [Parameter(Mandatory = true, Position = 0, ParameterSetName = MetadataSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = UnitSet)]
    [Parameter(Mandatory = true, Position = 0, ParameterSetName = AdjustSet)]
    [ValidateNotNullOrEmpty]
    public string StatisticId { get; set; } = string.Empty;

    [Parameter(ParameterSetName = MetadataSet)] public string? UnitClass { get; set; }
    [Parameter(ParameterSetName = MetadataSet)] public SwitchParameter ClearUnitClass { get; set; }
    [Parameter(ParameterSetName = MetadataSet)] public string? UnitOfMeasurement { get; set; }
    [Parameter(ParameterSetName = MetadataSet)] public SwitchParameter ClearUnitOfMeasurement { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = UnitSet)][ValidateSwitchPresent] public SwitchParameter ChangeUnit { get; set; }
    [Parameter(ParameterSetName = UnitSet)] public string? OldUnit { get; set; }
    [Parameter(ParameterSetName = UnitSet)] public string? NewUnit { get; set; }
    [Parameter(ParameterSetName = UnitSet)] public SwitchParameter ClearOldUnit { get; set; }
    [Parameter(ParameterSetName = UnitSet)] public SwitchParameter ClearNewUnit { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = AdjustSet)] public double AdjustSum { get; set; }
    [Parameter(Mandatory = true, ParameterSetName = AdjustSet)] public DateTimeOffset StartTime { get; set; }
    [Parameter(ParameterSetName = AdjustSet)] public string? Unit { get; set; }

    [Parameter(Mandatory = true, ParameterSetName = ImportSet)] public HomeAssistantStatisticImportMetadata? ImportMetadata { get; set; }
    [Parameter(Mandatory = true, ValueFromPipeline = true, ParameterSetName = ImportSet)] public HomeAssistantStatisticImportRow[] ImportRow { get; set; } = Array.Empty<HomeAssistantStatisticImportRow>();

    protected override async Task ProcessRecordAsync()
    {
        if (ParameterSetName == ImportSet)
        {
            if (ImportRow is null || ImportRow.Length == 0)
            {
                throw new ArgumentException("At least one import row is required.", nameof(ImportRow));
            }

            _importRows.AddRange(ImportRow);
            return;
        }

        if (!HomeAssistantStatisticIdentifier.TryNormalize(StatisticId, out var statisticId))
            throw new ArgumentException("Statistic identifiers must use '<domain>.<object>' or '<source>:<name>' with canonical lowercase slug segments.", nameof(StatisticId));

        switch (ParameterSetName)
        {
            case MetadataSet:
                RequireExclusive(UnitClass, ClearUnitClass, nameof(UnitClass), nameof(ClearUnitClass));
                RequireExclusive(UnitOfMeasurement, ClearUnitOfMeasurement, nameof(UnitOfMeasurement), nameof(ClearUnitOfMeasurement), required: true);
                var unitClass = ClearUnitClass ? null : UnitClass;
                if (!ClearUnitClass && !MyInvocation.BoundParameters.ContainsKey(nameof(UnitClass)))
                {
                    var metadata = await Client.Recorder.GetStatisticsMetadataAsync(new[] { statisticId }, CancelToken).ConfigureAwait(false);
                    unitClass = metadata.FirstOrDefault(item => string.Equals(item.StatisticId, statisticId, StringComparison.OrdinalIgnoreCase))?.UnitClass;
                }
                unitClass = NormalizeOptionalUnit(unitClass, nameof(UnitClass));
                var unitOfMeasurement = ClearUnitOfMeasurement ? null : NormalizeOptionalUnit(UnitOfMeasurement, nameof(UnitOfMeasurement));
                if (!ShouldProcess(ConnectionDisplayName, "Update Recorder statistics metadata for 1 identifier")) return;
                await Client.Recorder.UpdateStatisticsMetadataAsync(statisticId, unitClass, unitOfMeasurement, CancelToken).ConfigureAwait(false);
                return;
            case UnitSet:
                RequireExclusive(OldUnit, ClearOldUnit, nameof(OldUnit), nameof(ClearOldUnit), required: true);
                RequireExclusive(NewUnit, ClearNewUnit, nameof(NewUnit), nameof(ClearNewUnit), required: true);
                var oldUnit = ClearOldUnit ? null : OldUnit!.Trim();
                var newUnit = ClearNewUnit ? null : NewUnit!.Trim();
                if (string.Equals(oldUnit, newUnit, StringComparison.Ordinal))
                    throw new ArgumentException("OldUnit and NewUnit must resolve to different values.");
                if (!ShouldProcess(ConnectionDisplayName, "Convert stored Recorder statistics for 1 identifier to a new unit")) return;
                await Client.Recorder.ChangeStatisticsUnitAsync(statisticId, oldUnit, newUnit, CancelToken).ConfigureAwait(false);
                return;
            case AdjustSet:
                if (double.IsNaN(AdjustSum) || double.IsInfinity(AdjustSum)) throw new ArgumentOutOfRangeException(nameof(AdjustSum));
                var adjustmentUnit = Unit is null
                    ? null
                    : string.IsNullOrWhiteSpace(Unit)
                        ? throw new ArgumentException("Unit must not be blank.", nameof(Unit))
                        : Unit.Trim();
                if (!ShouldProcess(ConnectionDisplayName, $"Adjust Recorder sum by {AdjustSum} for 1 identifier")) return;
                await Client.Recorder.AdjustSumStatisticsAsync(statisticId, StartTime, AdjustSum, adjustmentUnit, CancelToken).ConfigureAwait(false);
                return;
            default:
                throw new InvalidOperationException("Unexpected statistics parameter set.");
        }
    }

    protected override async Task EndProcessingAsync()
    {
        if (ParameterSetName != ImportSet)
        {
            return;
        }

        var metadata = ValidateImport(ImportMetadata, _importRows);
        if (!ShouldProcess(ConnectionDisplayName, $"Import {_importRows.Count} Recorder statistics rows for 1 identifier")) return;
        await Client.Recorder.ImportStatisticsAsync(metadata, _importRows, CancelToken).ConfigureAwait(false);
    }

    private static HomeAssistantStatisticImportMetadata ValidateImport(HomeAssistantStatisticImportMetadata? metadata, IReadOnlyCollection<HomeAssistantStatisticImportRow> rows)
    {
        if (metadata is null) throw new ArgumentException("Import metadata is required.", nameof(ImportMetadata));
        if (!HomeAssistantStatisticIdentifier.TryNormalizeExternal(metadata.StatisticId, out var statisticId, out var source)
            || !string.Equals(source, metadata.Source?.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("Import metadata requires a canonical external StatisticId whose source prefix exactly matches Source.", nameof(ImportMetadata));
        var normalized = new HomeAssistantStatisticImportMetadata
        {
            StatisticId = statisticId,
            Source = source,
            Name = metadata.Name,
            HasMean = metadata.HasMean,
            HasSum = metadata.HasSum,
            MeanType = metadata.MeanType,
            UnitClass = NormalizeOptionalUnit(metadata.UnitClass, nameof(metadata.UnitClass)),
            UnitOfMeasurement = NormalizeOptionalUnit(metadata.UnitOfMeasurement, nameof(metadata.UnitOfMeasurement))
        };
        normalized.ValidateRows(rows);
        return normalized;
    }

    private static void RequireExclusive(string? value, bool clear, string valueName, string clearName, bool required = false)
    {
        if (value is not null && clear) throw new ArgumentException($"{valueName} and {clearName} cannot be combined.");
        if (required && value is null && !clear) throw new ArgumentException($"Specify {valueName} or {clearName}.");
        if (value is not null && string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{valueName} must not be blank.", valueName);
    }

    private static string? NormalizeOptionalUnit(string? value, string parameterName)
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{parameterName} must not be blank.", parameterName);
        return value.Trim();
    }
}
