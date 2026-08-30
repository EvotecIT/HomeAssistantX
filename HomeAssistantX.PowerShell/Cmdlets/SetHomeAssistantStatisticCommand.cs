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

            foreach (var row in ImportRow)
            {
                CancelToken.ThrowIfCancellationRequested();
                _importRows.Add(row is null ? null! : Snapshot(row));
            }
            return;
        }

        if (!HomeAssistantStatisticIdentifier.TryNormalize(StatisticId, CancelToken, out var statisticId))
            throw new ArgumentException("Statistic identifiers must use '<domain>.<object>' or '<source>:<name>' with canonical lowercase slug segments.", nameof(StatisticId));

        switch (ParameterSetName)
        {
            case MetadataSet:
                RequireExclusive(UnitClass, ClearUnitClass, nameof(UnitClass), nameof(ClearUnitClass), CancelToken);
                RequireExclusive(UnitOfMeasurement, ClearUnitOfMeasurement, nameof(UnitOfMeasurement), nameof(ClearUnitOfMeasurement), CancelToken, allowEmptySentinel: true);
                var hasUnitClassValue = MyInvocation.BoundParameters.ContainsKey(nameof(UnitClass));
                var hasUnitValue = MyInvocation.BoundParameters.ContainsKey(nameof(UnitOfMeasurement));
                if (hasUnitClassValue && UnitClass is null) throw new ArgumentNullException(nameof(UnitClass), "Use ClearUnitClass to remove the unit class.");
                if (hasUnitValue && UnitOfMeasurement is null) throw new ArgumentNullException(nameof(UnitOfMeasurement), "Use ClearUnitOfMeasurement to remove the unit.");
                var hasUnitClass = hasUnitClassValue || ClearUnitClass;
                var hasUnitOfMeasurement = hasUnitValue || ClearUnitOfMeasurement;
                if (!hasUnitClass && !hasUnitOfMeasurement)
                {
                    throw new ArgumentException("Specify a unit class or unit-of-measurement change.");
                }
                var requestedUnitClass = ClearUnitClass
                    ? null
                    : hasUnitClassValue
                        ? HomeAssistantStatisticIdentifier.NormalizeOptionalUnitClass(UnitClass, nameof(UnitClass), CancelToken)
                        : null;
                var requestedUnitOfMeasurement = ClearUnitOfMeasurement
                    ? null
                    : hasUnitValue
                        ? HomeAssistantStatisticIdentifier.NormalizeOptionalUnit(UnitOfMeasurement, nameof(UnitOfMeasurement), CancelToken)
                        : null;
                var availableMetadata = await Client.Recorder.GetStatisticsMetadataAsync(new[] { statisticId }, CancelToken).ConfigureAwait(false);
                HomeAssistantStatisticMetadata? matchingMetadata = null;
                foreach (var item in availableMetadata)
                {
                    CancelToken.ThrowIfCancellationRequested();
                    if (!HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinalIgnoreCase(item.StatisticId, statisticId, CancelToken)) continue;
                    matchingMetadata = item;
                    break;
                }
                if (matchingMetadata is null)
                {
                    throw new InvalidOperationException("Home Assistant did not return metadata for the requested Recorder statistic.");
                }
                var unitClass = hasUnitClass ? requestedUnitClass : matchingMetadata.UnitClass;
                var unitOfMeasurement = hasUnitOfMeasurement ? requestedUnitOfMeasurement : matchingMetadata.StatisticsUnitOfMeasurement;
                if (string.Equals(unitClass, matchingMetadata.UnitClass, StringComparison.Ordinal)
                    && string.Equals(unitOfMeasurement, matchingMetadata.StatisticsUnitOfMeasurement, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException("The requested Recorder statistic metadata already matches the current values.");
                }
                if (!ShouldProcess(ConnectionDisplayName, "Update Recorder statistics metadata for 1 identifier")) return;
                await Client.Recorder.UpdateStatisticsMetadataAsync(statisticId, unitClass, unitOfMeasurement, CancelToken).ConfigureAwait(false);
                return;
            case UnitSet:
                RequireExclusive(OldUnit, ClearOldUnit, nameof(OldUnit), nameof(ClearOldUnit), CancelToken, required: true, allowEmptySentinel: true);
                RequireExclusive(NewUnit, ClearNewUnit, nameof(NewUnit), nameof(ClearNewUnit), CancelToken, required: true, allowEmptySentinel: true);
                var oldUnit = ClearOldUnit ? null : HomeAssistantX.Protocol.CancellationAwareString.Trim(OldUnit!, CancelToken);
                var newUnit = ClearNewUnit ? null : HomeAssistantX.Protocol.CancellationAwareString.Trim(NewUnit!, CancelToken);
                if (string.Equals(oldUnit, newUnit, StringComparison.Ordinal))
                    throw new ArgumentException("OldUnit and NewUnit must resolve to different values.");
                if (!ShouldProcess(ConnectionDisplayName, "Convert stored Recorder statistics for 1 identifier to a new unit")) return;
                await Client.Recorder.ChangeStatisticsUnitAsync(statisticId, oldUnit, newUnit, CancelToken).ConfigureAwait(false);
                return;
            case AdjustSet:
                if (double.IsNaN(AdjustSum) || double.IsInfinity(AdjustSum)) throw new ArgumentOutOfRangeException(nameof(AdjustSum));
                if (AdjustSum == 0d) throw new ArgumentOutOfRangeException(nameof(AdjustSum), "AdjustSum must be non-zero.");
                var adjustmentUnit = HomeAssistantStatisticIdentifier.NormalizeOptionalUnit(Unit, nameof(Unit), CancelToken);
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

        var metadata = ValidateImport(ImportMetadata, _importRows, CancelToken);
        if (!ShouldProcess(ConnectionDisplayName, $"Import {_importRows.Count} Recorder statistics rows for 1 identifier")) return;
        await Client.Recorder.ImportStatisticsAsync(metadata, _importRows, CancelToken).ConfigureAwait(false);
    }

    private static HomeAssistantStatisticImportMetadata ValidateImport(
        HomeAssistantStatisticImportMetadata? metadata,
        IReadOnlyCollection<HomeAssistantStatisticImportRow> rows,
        CancellationToken cancellationToken)
    {
        if (metadata is null) throw new ArgumentException("Import metadata is required.", nameof(ImportMetadata));
        cancellationToken.ThrowIfCancellationRequested();
        var normalizedSource = metadata.Source is null
            ? null
            : HomeAssistantX.Protocol.CancellationAwareString.Trim(metadata.Source, cancellationToken);
        if (!HomeAssistantStatisticIdentifier.TryNormalizeExternal(metadata.StatisticId, cancellationToken, out var statisticId, out var source)
            || !HomeAssistantX.Protocol.CancellationAwareString.EqualsOrdinal(source, normalizedSource, cancellationToken))
            throw new ArgumentException("Import metadata requires a canonical external StatisticId whose source prefix exactly matches Source.", nameof(ImportMetadata));
        var normalized = new HomeAssistantStatisticImportMetadata
        {
            StatisticId = statisticId,
            Source = source,
            Name = metadata.Name,
            HasMean = metadata.HasMean,
            HasSum = metadata.HasSum,
            MeanType = metadata.MeanType,
            UnitClass = HomeAssistantStatisticIdentifier.NormalizeOptionalUnitClass(metadata.UnitClass, nameof(metadata.UnitClass), cancellationToken),
            UnitOfMeasurement = HomeAssistantStatisticIdentifier.NormalizeOptionalUnit(metadata.UnitOfMeasurement, nameof(metadata.UnitOfMeasurement), cancellationToken)
        };
        normalized.ValidateRows(rows, cancellationToken);
        return normalized;
    }

    private static HomeAssistantStatisticImportRow Snapshot(HomeAssistantStatisticImportRow row)
        => new()
        {
            Start = row.Start,
            Mean = row.Mean,
            Minimum = row.Minimum,
            Maximum = row.Maximum,
            LastReset = row.LastReset,
            State = row.State,
            Sum = row.Sum
        };

    private static void RequireExclusive(
        string? value,
        bool clear,
        string valueName,
        string clearName,
        CancellationToken cancellationToken,
        bool required = false,
        bool allowEmptySentinel = false)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is not null && clear) throw new ArgumentException($"{valueName} and {clearName} cannot be combined.");
        if (required && value is null && !clear) throw new ArgumentException($"Specify {valueName} or {clearName}.");
        if (value is not null
            && HomeAssistantX.Protocol.CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken)
            && !(allowEmptySentinel && value.Length == 0))
            throw new ArgumentException($"{valueName} must not be blank.", valueName);
        cancellationToken.ThrowIfCancellationRequested();
    }

}
