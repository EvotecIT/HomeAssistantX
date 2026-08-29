using System.Globalization;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;
using HomeAssistantX.Services;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Recorder;

/// <summary>Provides Recorder history, logbook, statistics, and maintenance operations.</summary>
public sealed class HomeAssistantRecorderClient
{
    private readonly HomeAssistantRestClient _rest;
    private readonly HomeAssistantWebSocketClient _webSocket;
    private readonly HomeAssistantServiceClient _services;

    internal HomeAssistantRecorderClient(
        HomeAssistantRestClient rest,
        HomeAssistantWebSocketClient webSocket,
        HomeAssistantServiceClient services)
    {
        _rest = rest;
        _webSocket = webSocket;
        _services = services;
    }

    public async Task<IReadOnlyList<HomeAssistantStatisticMetadata>> GetStatisticsMetadataAsync(
        IReadOnlyCollection<string>? statisticIds = null,
        CancellationToken cancellationToken = default)
    {
        var requestedIdSnapshot = statisticIds is null ? null : NormalizeStatisticIds(statisticIds, nameof(statisticIds), cancellationToken);
        var payload = requestedIdSnapshot is null ? null : new Dictionary<string, object?> { ["statistic_ids"] = requestedIdSnapshot };
        var value = await _webSocket.RequestAsync("recorder/get_statistics_metadata", payload, cancellationToken).ConfigureAwait(false);
        var metadata = DecodeMetadata(value, "Recorder statistics metadata could not be decoded.", cancellationToken);
        var responseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in metadata)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!responseIds.Add(item.StatisticId))
                throw new HomeAssistantProtocolException("Recorder statistics metadata contained a duplicate statistic identifier.");
        }
        if (requestedIdSnapshot is not null)
        {
            var requestedIds = new HashSet<string>(requestedIdSnapshot, StringComparer.OrdinalIgnoreCase);
            foreach (var item in metadata)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!requestedIds.Contains(item.StatisticId))
                    throw new HomeAssistantProtocolException("Recorder statistics metadata contained an unexpected statistic identifier.");
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        return metadata;
    }

    public async Task<IReadOnlyList<HomeAssistantStatisticMetadata>> ListStatisticsAsync(
        HomeAssistantStatisticKind kind = HomeAssistantStatisticKind.Any,
        CancellationToken cancellationToken = default)
    {
        var payload = kind == HomeAssistantStatisticKind.Any ? null : new Dictionary<string, object?>
        {
            ["statistic_type"] = kind == HomeAssistantStatisticKind.Mean ? "mean" : kind == HomeAssistantStatisticKind.Sum ? "sum" : throw new ArgumentOutOfRangeException(nameof(kind))
        };
        var value = await _webSocket.RequestAsync("recorder/list_statistic_ids", payload, cancellationToken).ConfigureAwait(false);
        var metadata = DecodeMetadata(value, "Recorder statistic identifiers could not be decoded.", cancellationToken);
        var responseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in metadata)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!responseIds.Add(item.StatisticId))
                throw new HomeAssistantProtocolException("Recorder statistic identifiers contained a duplicate identifier.");
        }

        foreach (var item in metadata)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (kind == HomeAssistantStatisticKind.Mean
                    && !item.HasMean && item.MeanType != HomeAssistantStatisticMeanType.Circular
                || kind == HomeAssistantStatisticKind.Sum && !item.HasSum)
                throw new HomeAssistantProtocolException("Recorder statistic identifiers did not match the requested statistic type.");
        }
        cancellationToken.ThrowIfCancellationRequested();
        return metadata;
    }

    public async Task<IReadOnlyList<HomeAssistantStatisticSeries>> GetStatisticsAsync(
        HomeAssistantStatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query is null) throw new ArgumentNullException(nameof(query));
        cancellationToken.ThrowIfCancellationRequested();
        var startTime = query.StartTime;
        var endTime = query.EndTime;
        var period = query.Period;
        if (endTime.HasValue && endTime <= startTime)
            throw new ArgumentOutOfRangeException(nameof(query), "The statistics end must be after the start.");
        var requestedIdSnapshot = NormalizeStatisticIds(query.StatisticIds, nameof(query), cancellationToken);
        var payload = new Dictionary<string, object?>
        {
            ["start_time"] = startTime.ToString("O", CultureInfo.InvariantCulture),
            ["statistic_ids"] = requestedIdSnapshot,
            ["period"] = PeriodName(period)
        };
        if (endTime.HasValue) payload["end_time"] = endTime.Value.ToString("O", CultureInfo.InvariantCulture);
        IReadOnlyList<HomeAssistantStatisticType>? requestedTypes = null;
        if (query.Types is not null)
        {
            if (query.Types.Count == 0) throw new ArgumentException("Statistics types cannot be empty.", nameof(query));
            var types = new List<string>(query.Types.Count);
            var typeSnapshot = new List<HomeAssistantStatisticType>(query.Types.Count);
            foreach (var type in query.Types)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = TypeName(type);
                if (!types.Contains(name, StringComparer.Ordinal))
                {
                    types.Add(name);
                    typeSnapshot.Add(type);
                }
            }
            requestedTypes = typeSnapshot;
            payload["types"] = types.ToArray();
        }
        if (query.Units is not null)
        {
            var units = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in query.Units)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrEmpty(pair.Key)
                    || !HomeAssistantStatisticIdentifier.IsSlug(pair.Key, cancellationToken)
                    || CancellationAwareString.IsNullOrWhiteSpace(pair.Value, cancellationToken))
                    throw new ArgumentException("Statistics unit-class keys must be canonical lowercase identifiers and unit values must be non-empty.", nameof(query));
                var normalizedName = pair.Key;
                if (units.ContainsKey(normalizedName))
                    throw new ArgumentException("Statistics unit names must be unique after normalization.", nameof(query));
                units.Add(normalizedName, CancellationAwareString.Trim(pair.Value, cancellationToken));
            }
            payload["units"] = units;
        }

        TimeZoneInfo? homeTimeZone = null;
        if (period is HomeAssistantStatisticPeriod.Day or HomeAssistantStatisticPeriod.Week or HomeAssistantStatisticPeriod.Month)
        {
            var configuration = await _rest.GetConfigurationAsync(cancellationToken).ConfigureAwait(false);
            homeTimeZone = HomeAssistantCalendarTime.RequireTimeZone(
                configuration.TimeZone,
                "calendar statistics");
        }

        var value = await _webSocket.RequestAsync("recorder/statistics_during_period", payload, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object) throw new HomeAssistantProtocolException("Recorder statistics were not an object.");
        var series = new List<HomeAssistantStatisticSeries>();
        var requestedIds = new HashSet<string>(requestedIdSnapshot, StringComparer.OrdinalIgnoreCase);
        var responseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantStatisticIdentifier.TryNormalize(property.Name, cancellationToken, out var normalizedStatisticId)
                || !CancellationAwareString.EqualsOrdinal(property.Name, normalizedStatisticId, cancellationToken)
                || !requestedIds.Contains(normalizedStatisticId)
                || !responseIds.Add(normalizedStatisticId))
                throw new HomeAssistantProtocolException("Recorder statistics contained an unexpected or duplicate statistic identifier.");
            ValidateStatisticRows(
                property.Value,
                GetPeriodStart(startTime, period, homeTimeZone),
                endTime,
                period,
                homeTimeZone,
                requestedTypes,
                cancellationToken);
            var rows = HomeAssistantJson.DeserializeResponse<HomeAssistantStatisticRow[]>(
                property.Value,
                "A Recorder statistics series could not be decoded.",
                cancellationToken: cancellationToken);
            series.Add(new HomeAssistantStatisticSeries { StatisticId = normalizedStatisticId, Rows = rows });
        }
        var comparer = new CancellationAwareStringComparer(StringComparison.OrdinalIgnoreCase, cancellationToken);
        SortSeries(series, comparer);
        cancellationToken.ThrowIfCancellationRequested();
        return series;
    }

    internal static void SortSeries(
        List<HomeAssistantStatisticSeries> series,
        IComparer<string> comparer)
    {
        CancellationAwareSort.Sort(series, (left, right) => comparer.Compare(left.StatisticId, right.StatisticId));
    }

    public Task<JsonElement> ValidateStatisticsAsync(CancellationToken cancellationToken = default)
        => _webSocket.RequestAsync("recorder/validate_statistics", null, cancellationToken);

    public async Task UpdateStatisticsIssuesAsync(CancellationToken cancellationToken = default)
        => _ = await _webSocket.RequestAsync("recorder/update_statistics_issues", null, cancellationToken).ConfigureAwait(false);

    public async Task ClearStatisticsAsync(IReadOnlyCollection<string> statisticIds, CancellationToken cancellationToken = default)
        => _ = await _webSocket.RequestAsync("recorder/clear_statistics", new Dictionary<string, object?> { ["statistic_ids"] = NormalizeStatisticIds(statisticIds, nameof(statisticIds), cancellationToken) }, cancellationToken).ConfigureAwait(false);

    public async Task UpdateStatisticsMetadataAsync(string statisticId, string? unitClass, string? unitOfMeasurement, CancellationToken cancellationToken = default)
    {
        unitClass = HomeAssistantStatisticIdentifier.NormalizeOptionalUnitClass(unitClass, nameof(unitClass), cancellationToken);
        unitOfMeasurement = HomeAssistantStatisticIdentifier.NormalizeOptionalUnit(unitOfMeasurement, nameof(unitOfMeasurement), cancellationToken);
        _ = await _webSocket.RequestAsync("recorder/update_statistics_metadata", new Dictionary<string, object?>
        {
            ["statistic_id"] = RequireStatisticId(statisticId, nameof(statisticId), cancellationToken),
            ["unit_class"] = unitClass,
            ["unit_of_measurement"] = unitOfMeasurement
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangeStatisticsUnitAsync(string statisticId, string? oldUnit, string? newUnit, CancellationToken cancellationToken = default)
    {
        oldUnit = HomeAssistantStatisticIdentifier.NormalizeOptionalUnit(oldUnit, nameof(oldUnit), cancellationToken);
        newUnit = HomeAssistantStatisticIdentifier.NormalizeOptionalUnit(newUnit, nameof(newUnit), cancellationToken);
        if (CancellationAwareString.EqualsOrdinal(oldUnit, newUnit, cancellationToken))
            throw new ArgumentException("The old and new statistics units must be different.", nameof(newUnit));
        _ = await _webSocket.RequestAsync("recorder/change_statistics_unit", new Dictionary<string, object?>
        {
            ["statistic_id"] = RequireStatisticId(statisticId, nameof(statisticId), cancellationToken),
            ["old_unit_of_measurement"] = oldUnit,
            ["new_unit_of_measurement"] = newUnit
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdjustSumStatisticsAsync(string statisticId, DateTimeOffset start, double adjustment, string? unit, CancellationToken cancellationToken = default)
    {
        if (double.IsNaN(adjustment) || double.IsInfinity(adjustment)) throw new ArgumentOutOfRangeException(nameof(adjustment));
        if (adjustment == 0d) throw new ArgumentOutOfRangeException(nameof(adjustment), "A statistics sum adjustment must be non-zero.");
        unit = HomeAssistantStatisticIdentifier.NormalizeOptionalUnit(unit, nameof(unit), cancellationToken);
        _ = await _webSocket.RequestAsync("recorder/adjust_sum_statistics", new Dictionary<string, object?>
        {
            ["statistic_id"] = RequireStatisticId(statisticId, nameof(statisticId), cancellationToken),
            ["start_time"] = start.ToString("O", CultureInfo.InvariantCulture),
            ["adjustment"] = adjustment,
            ["adjustment_unit_of_measurement"] = unit
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ImportStatisticsAsync(HomeAssistantStatisticImportMetadata metadata, IReadOnlyCollection<HomeAssistantStatisticImportRow> rows, CancellationToken cancellationToken = default)
    {
        if (metadata is null) throw new ArgumentNullException(nameof(metadata));
        if (rows is null) throw new ArgumentNullException(nameof(rows));
        cancellationToken.ThrowIfCancellationRequested();
        var rowSnapshot = new List<HomeAssistantStatisticImportRow>(rows.Count);
        foreach (var row in rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (row is null)
            {
                rowSnapshot.Add(null!);
                continue;
            }
            rowSnapshot.Add(new HomeAssistantStatisticImportRow
            {
                Start = row.Start,
                Mean = row.Mean,
                Minimum = row.Minimum,
                Maximum = row.Maximum,
                LastReset = row.LastReset,
                State = row.State,
                Sum = row.Sum
            });
        }
        cancellationToken.ThrowIfCancellationRequested();
        metadata.ValidateRows(rowSnapshot, cancellationToken);
        var unitClass = HomeAssistantStatisticIdentifier.NormalizeOptionalUnitClass(metadata.UnitClass, nameof(metadata.UnitClass), cancellationToken);
        var unitOfMeasurement = HomeAssistantStatisticIdentifier.NormalizeOptionalUnit(metadata.UnitOfMeasurement, nameof(metadata.UnitOfMeasurement), cancellationToken);
        var metadataPayload = new Dictionary<string, object?>
        {
            ["statistic_id"] = RequireStatisticId(metadata.StatisticId, nameof(metadata.StatisticId), cancellationToken),
            ["source"] = Require(metadata.Source, nameof(metadata.Source), cancellationToken),
            ["name"] = metadata.Name,
            ["has_mean"] = metadata.HasMean,
            ["has_sum"] = metadata.HasSum,
            ["unit_class"] = unitClass,
            ["unit_of_measurement"] = unitOfMeasurement
        };
        metadataPayload["mean_type"] = (int)metadata.MeanType;
        var rowPayload = new List<Dictionary<string, object?>>(rowSnapshot.Count);
        foreach (var row in rowSnapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowPayload.Add(ToImportPayload(row));
        }
        cancellationToken.ThrowIfCancellationRequested();
        _ = await _webSocket.RequestAsync("recorder/import_statistics", new Dictionary<string, object?>
        {
            ["metadata"] = metadataPayload,
            ["stats"] = rowPayload.ToArray()
        }, cancellationToken).ConfigureAwait(false);
    }

    public Task<HomeAssistantServiceCallResult> PurgeAsync(int? keepDays = null, bool repack = false, bool applyFilter = false, CancellationToken cancellationToken = default)
    {
        if (keepDays.HasValue && keepDays.Value < 0) throw new ArgumentOutOfRangeException(nameof(keepDays));
        var call = new HomeAssistantServiceCall("recorder", "purge");
        if (keepDays.HasValue) call.WithData("keep_days", keepDays.Value);
        if (repack) call.WithData("repack", true);
        if (applyFilter) call.WithData("apply_filter", true);
        return _services.CallControlAsync(call, cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> PurgeEntitiesAsync(IReadOnlyCollection<string>? entityIds = null, IReadOnlyCollection<string>? domains = null, IReadOnlyCollection<string>? entityGlobs = null, int? keepDays = null, CancellationToken cancellationToken = default)
    {
        if ((entityIds is null || entityIds.Count == 0) && (domains is null || domains.Count == 0) && (entityGlobs is null || entityGlobs.Count == 0))
            throw new ArgumentException("At least one entity, domain, or entity glob is required.");
        if (keepDays.HasValue && keepDays.Value < 0) throw new ArgumentOutOfRangeException(nameof(keepDays));
        var call = new HomeAssistantServiceCall("recorder", "purge_entities");
        if (entityIds is { Count: > 0 }) call.WithData("entity_id", NormalizePurgeEntityIds(entityIds, nameof(entityIds), cancellationToken));
        if (domains is { Count: > 0 }) call.WithData("domains", NormalizePurgeDomains(domains, nameof(domains), cancellationToken));
        if (entityGlobs is { Count: > 0 }) call.WithData("entity_globs", NormalizePurgeEntityGlobs(entityGlobs, nameof(entityGlobs), cancellationToken));
        if (keepDays.HasValue) call.WithData("keep_days", keepDays.Value);
        return _services.CallControlAsync(call, cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        => _services.CallControlAsync(new HomeAssistantServiceCall("recorder", enabled ? "enable" : "disable"), cancellationToken);

    private static IReadOnlyList<HomeAssistantStatisticMetadata> DecodeMetadata(
        JsonElement value,
        string failureMessage,
        CancellationToken cancellationToken)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException(failureMessage);
        foreach (var item in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.ValueKind != JsonValueKind.Object
                || HomeAssistantJson.HasDuplicateProperties(item, cancellationToken)
                || !item.TryGetProperty("statistic_id", out var statisticId)
                || statisticId.ValueKind != JsonValueKind.String
                || statisticId.GetString() is not string statisticIdValue
                || CancellationAwareString.IsNullOrWhiteSpace(statisticIdValue, cancellationToken)
                || !HomeAssistantStatisticIdentifier.TryNormalize(statisticIdValue, cancellationToken, out var normalizedStatisticId)
                || !CancellationAwareString.EqualsOrdinal(statisticIdValue, normalizedStatisticId, cancellationToken)
                || !item.TryGetProperty("source", out var source)
                || source.ValueKind != JsonValueKind.String
                || source.GetString() is not string sourceValue
                || !HomeAssistantStatisticIdentifier.IsSlug(sourceValue, cancellationToken)
                || (statisticIdValue.Contains(':')
                    ? !HomeAssistantStatisticIdentifier.TryNormalizeExternal(statisticIdValue, cancellationToken, out _, out var statisticSource)
                        || !CancellationAwareString.EqualsOrdinal(sourceValue, statisticSource, cancellationToken)
                    : !CancellationAwareString.EqualsOrdinal(sourceValue, "recorder", cancellationToken))
                || !item.TryGetProperty("has_mean", out var hasMean)
                || hasMean.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !item.TryGetProperty("has_sum", out var hasSum)
                || hasSum.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !HasCanonicalOptionalUnit(item, "unit_of_measurement", cancellationToken)
                || !HasCanonicalOptionalUnit(item, "statistics_unit_of_measurement", cancellationToken))
            {
                throw new HomeAssistantProtocolException(failureMessage);
            }
        }

        var metadata = HomeAssistantJson.DeserializeResponse<HomeAssistantStatisticMetadata[]>(
            value,
            failureMessage,
            cancellationToken: cancellationToken);
        foreach (var item in metadata)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item.MeanType.HasValue
                    && (!Enum.IsDefined(typeof(HomeAssistantStatisticMeanType), item.MeanType.Value)
                        || item.HasMean != (item.MeanType.Value == HomeAssistantStatisticMeanType.Arithmetic))
                || !item.HasMean && !item.HasSum && item.MeanType != HomeAssistantStatisticMeanType.Circular
                || item.UnitClass is not null && !HomeAssistantStatisticIdentifier.IsSlug(item.UnitClass, cancellationToken))
                throw new HomeAssistantProtocolException(failureMessage);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return metadata;
    }

    private static bool HasCanonicalOptionalUnit(
        JsonElement value,
        string propertyName,
        CancellationToken cancellationToken)
    {
        if (!value.TryGetProperty(propertyName, out var unit) || unit.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (unit.ValueKind != JsonValueKind.String || unit.GetString() is not string text)
        {
            return false;
        }

        return text.Length == 0
            || (!CancellationAwareString.IsNullOrWhiteSpace(text, cancellationToken)
                && CancellationAwareString.EqualsOrdinal(
                    text,
                    CancellationAwareString.Trim(text, cancellationToken),
                    cancellationToken));
    }

    private static void ValidateStatisticRows(
        JsonElement value,
        DateTimeOffset earliestPeriodStart,
        DateTimeOffset? endTimeExclusive,
        HomeAssistantStatisticPeriod period,
        TimeZoneInfo? homeTimeZone,
        IReadOnlyList<HomeAssistantStatisticType>? requestedTypes,
        CancellationToken cancellationToken)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException("A Recorder statistics series could not be decoded.");
        long? previousStart = null;
        long? previousEnd = null;
        foreach (var row in value.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            var hasNonNullLastReset = row.ValueKind == JsonValueKind.Object
                && row.TryGetProperty("last_reset", out var lastResetValue)
                && lastResetValue.ValueKind != JsonValueKind.Null;
            if (row.ValueKind != JsonValueKind.Object
                || HomeAssistantJson.HasDuplicateProperties(row, cancellationToken)
                || !TryGetUnixMilliseconds(row, "start", required: true, out var start)
                || !TryGetUnixMilliseconds(row, "end", required: true, out var end)
                || !TryGetUnixMilliseconds(row, "last_reset", required: false, out var lastReset)
                || !TryGetFiniteDouble(row, "mean", out var mean)
                || !TryGetFiniteDouble(row, "min", out var minimum)
                || !TryGetFiniteDouble(row, "max", out var maximum)
                || !TryGetFiniteDouble(row, "state", out var state)
                || !TryGetFiniteDouble(row, "sum", out var sum)
                || !TryGetFiniteDouble(row, "change", out var change))
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series contained an invalid timestamp.");
            }

            if (requestedTypes is not null
                && !ContainsRequestedStatisticValues(
                    requestedTypes,
                    row,
                    cancellationToken))
            {
                throw new HomeAssistantProtocolException(
                    "A Recorder statistics series omitted a requested statistic value.");
            }

            if (end <= start)
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series contained a non-positive interval.");
            }


            if (!IsValidPeriodInterval(start, end, earliestPeriodStart, period, homeTimeZone))
            {
                throw new HomeAssistantProtocolException(
                    "A Recorder statistics series contained an interval that did not match the requested period.");
            }

            if (end <= earliestPeriodStart.ToUnixTimeMilliseconds()
                || endTimeExclusive.HasValue
                    && DateTimeOffset.FromUnixTimeMilliseconds(start) >= endTimeExclusive.Value)
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series contained a row outside the requested time window.");
            }

            if (hasNonNullLastReset && lastReset > end)
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series contained a reset timestamp after its interval.");
            }

            if (minimum.HasValue && maximum.HasValue && minimum.Value > maximum.Value)
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series contained a minimum greater than its maximum.");
            }

            if (previousStart.HasValue && start <= previousStart.Value)
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series was not ordered by strictly increasing start time.");
            }
            if (previousEnd.HasValue && start < previousEnd.Value)
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series contained overlapping intervals.");
            }
            previousStart = start;
            previousEnd = end;
        }
    }

    private static bool ContainsRequestedStatisticValues(
        IReadOnlyList<HomeAssistantStatisticType> requestedTypes,
        JsonElement row,
        CancellationToken cancellationToken)
    {
        foreach (var type in requestedTypes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var propertyName = type switch
            {
                HomeAssistantStatisticType.Change => "change",
                HomeAssistantStatisticType.LastReset => "last_reset",
                HomeAssistantStatisticType.Maximum => "max",
                HomeAssistantStatisticType.Mean => "mean",
                HomeAssistantStatisticType.Minimum => "min",
                HomeAssistantStatisticType.State => "state",
                HomeAssistantStatisticType.Sum => "sum",
                _ => throw new ArgumentOutOfRangeException(nameof(requestedTypes))
            };
            if (!row.TryGetProperty(propertyName, out _)) return false;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return true;
    }

    private static bool IsValidPeriodInterval(
        long start,
        long end,
        DateTimeOffset periodOrigin,
        HomeAssistantStatisticPeriod period,
        TimeZoneInfo? homeTimeZone)
    {
        var duration = end - start;
        if (period is HomeAssistantStatisticPeriod.FiveMinute or HomeAssistantStatisticPeriod.Hour)
        {
            var expected = period == HomeAssistantStatisticPeriod.FiveMinute
                ? TimeSpan.FromMinutes(5).Ticks / TimeSpan.TicksPerMillisecond
                : TimeSpan.FromHours(1).Ticks / TimeSpan.TicksPerMillisecond;
            return duration == expected
                && start >= periodOrigin.ToUnixTimeMilliseconds()
                && (start - periodOrigin.ToUnixTimeMilliseconds()) % expected == 0;
        }

        if (start < periodOrigin.ToUnixTimeMilliseconds()
            || homeTimeZone is null
            || !HomeAssistantCalendarTime.IsBoundary(start, homeTimeZone, CalendarPeriod(period)))
        {
            return false;
        }

        var localStart = TimeZoneInfo.ConvertTime(
            DateTimeOffset.FromUnixTimeMilliseconds(start),
            homeTimeZone);
        var localSuccessor = period switch
        {
            HomeAssistantStatisticPeriod.Day => new DateTime(
                localStart.Year, localStart.Month, localStart.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(1),
            HomeAssistantStatisticPeriod.Week => new DateTime(
                localStart.Year, localStart.Month, localStart.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(7),
            HomeAssistantStatisticPeriod.Month => new DateTime(
                localStart.Year, localStart.Month, 1, 0, 0, 0, DateTimeKind.Unspecified).AddMonths(1),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        return HomeAssistantCalendarTime.ResolveBoundary(localSuccessor, homeTimeZone).ToUnixTimeMilliseconds() == end;
    }

    private static bool TryGetUnixMilliseconds(JsonElement value, string propertyName, bool required, out long milliseconds)
    {
        milliseconds = default;
        if (!value.TryGetProperty(propertyName, out var property)) return !required;
        if (property.ValueKind == JsonValueKind.Null) return !required;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out milliseconds)) return false;
        try
        {
            _ = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            return true;
        }
        catch (Exception exception) when (exception is OverflowException || exception is ArgumentOutOfRangeException)
        {
            return false;
        }
    }

    private static bool TryGetFiniteDouble(JsonElement value, string propertyName, out double? result)
    {
        result = null;
        if (!value.TryGetProperty(propertyName, out var property) || property.ValueKind == JsonValueKind.Null) return true;
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetDouble(out var number)
            || double.IsNaN(number) || double.IsInfinity(number)) return false;
        result = number;
        return true;
    }

    private static Dictionary<string, object?> ToImportPayload(HomeAssistantStatisticImportRow row)
    {
        if (row is null) throw new ArgumentException("Statistics rows cannot contain null values.");
        var result = new Dictionary<string, object?> { ["start"] = row.Start.ToString("O", CultureInfo.InvariantCulture) };
        AddFinite(result, "mean", row.Mean); AddFinite(result, "min", row.Minimum); AddFinite(result, "max", row.Maximum);
        AddFinite(result, "state", row.State); AddFinite(result, "sum", row.Sum);
        if (row.LastReset.HasValue) result["last_reset"] = row.LastReset.Value.ToString("O", CultureInfo.InvariantCulture);
        return result;
    }

    private static void AddFinite(IDictionary<string, object?> payload, string name, double? value)
    {
        if (!value.HasValue) return;
        if (double.IsNaN(value.Value) || double.IsInfinity(value.Value)) throw new ArgumentOutOfRangeException(name);
        payload[name] = value.Value;
    }

    private static string Require(
        string value,
        string name,
        CancellationToken cancellationToken)
        => CancellationAwareString.IsNullOrWhiteSpace(value, cancellationToken)
            ? throw new ArgumentException("A non-empty value is required.", name)
            : CancellationAwareString.Trim(value, cancellationToken);

    private static string[] RequireIds(IReadOnlyCollection<string> values, string name)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("At least one non-empty identifier is required.", name);
        return values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    internal static string[] NormalizePurgeEntityGlobs(
        IReadOnlyCollection<string> values,
        string name,
        CancellationToken cancellationToken)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("At least one entity glob is required.", name);
        }

        var normalized = new List<string>(values.Count);
        var seen = new HashSet<string>(new CancellationAwareOrdinalStringEqualityComparer(cancellationToken));
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantRecorderEntityGlob.TryNormalize(value, out var entityGlob, cancellationToken))
            {
                throw new ArgumentException(
                    "Entity globs must be Home Assistant fnmatch pattern strings such as '*', 'sensor*', or 'sensor.kitchen_*'.",
                    name);
            }

            if (seen.Add(entityGlob)) normalized.Add(entityGlob);
        }

        cancellationToken.ThrowIfCancellationRequested();
        return normalized.ToArray();
    }

    internal static string[] NormalizePurgeEntityIds(
        IReadOnlyCollection<string> values,
        string name,
        CancellationToken cancellationToken)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("At least one entity identifier is required.", name);
        }

        var normalized = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantEntityId.TryNormalize(value, out var entityId))
            {
                throw new ArgumentException("Entity identifiers must use the native Home Assistant format.", name);
            }

            if (seen.Add(entityId))
            {
                normalized.Add(entityId);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return normalized.ToArray();
    }

    internal static string[] NormalizeStatisticIds(
        IReadOnlyCollection<string> values,
        string name,
        CancellationToken cancellationToken)
    {
        if (values is null || values.Count == 0) throw new ArgumentException("At least one statistic identifier is required.", name);
        var normalized = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantStatisticIdentifier.TryNormalize(value, cancellationToken, out var statisticId))
                throw new ArgumentException("Statistic identifiers must use '<domain>.<object>' or '<source>:<name>' with canonical lowercase slug segments.", name);
            if (seen.Add(statisticId)) normalized.Add(statisticId);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return normalized.ToArray();
    }

    private static string RequireStatisticId(
        string value,
        string name,
        CancellationToken cancellationToken)
    {
        if (!HomeAssistantStatisticIdentifier.TryNormalize(value, cancellationToken, out var statisticId))
            throw new ArgumentException("Statistic identifiers must use '<domain>.<object>' or '<source>:<name>' with canonical lowercase slug segments.", name);
        return statisticId;
    }

    internal static string[] NormalizePurgeDomains(
        IReadOnlyCollection<string> values,
        string name,
        CancellationToken cancellationToken)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("At least one domain is required.", name);
        }

        var normalized = new List<string>(values.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantEntityId.TryNormalizeDomain(value, cancellationToken, out var domain))
            {
                throw new ArgumentException("Domains must use the native Home Assistant format.", name);
            }

            if (seen.Add(domain))
            {
                normalized.Add(domain);
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        return normalized.ToArray();
    }

    private static DateTimeOffset GetPeriodStart(
        DateTimeOffset value,
        HomeAssistantStatisticPeriod period,
        TimeZoneInfo? homeTimeZone)
    {
        if (period is HomeAssistantStatisticPeriod.FiveMinute or HomeAssistantStatisticPeriod.Hour)
        {
            var utcValue = value.ToUniversalTime();
            var utcMinute = period == HomeAssistantStatisticPeriod.FiveMinute
                ? utcValue.Minute - utcValue.Minute % 5
                : 0;
            return new DateTimeOffset(
                utcValue.Year,
                utcValue.Month,
                utcValue.Day,
                utcValue.Hour,
                utcMinute,
                0,
                TimeSpan.Zero);
        }

        var localValue = homeTimeZone is null
            ? value
            : TimeZoneInfo.ConvertTime(value, homeTimeZone);
        if (homeTimeZone is null)
        {
            throw new ArgumentOutOfRangeException(nameof(period));
        }

        var localBoundary = period switch
        {
            HomeAssistantStatisticPeriod.Day => new DateTime(
                localValue.Year, localValue.Month, localValue.Day, 0, 0, 0, DateTimeKind.Unspecified),
            HomeAssistantStatisticPeriod.Week => new DateTime(
                localValue.Year, localValue.Month, localValue.Day, 0, 0, 0, DateTimeKind.Unspecified)
                .AddDays(-((7 + (int)localValue.DayOfWeek - (int)DayOfWeek.Monday) % 7)),
            HomeAssistantStatisticPeriod.Month => new DateTime(
                localValue.Year, localValue.Month, 1, 0, 0, 0, DateTimeKind.Unspecified),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
        return HomeAssistantCalendarTime.ResolveBoundary(localBoundary, homeTimeZone);
    }

    private static HomeAssistantCalendarPeriod CalendarPeriod(HomeAssistantStatisticPeriod period) => period switch
    {
        HomeAssistantStatisticPeriod.Day => HomeAssistantCalendarPeriod.Day,
        HomeAssistantStatisticPeriod.Week => HomeAssistantCalendarPeriod.Week,
        HomeAssistantStatisticPeriod.Month => HomeAssistantCalendarPeriod.Month,
        _ => throw new ArgumentOutOfRangeException(nameof(period))
    };

    private static string PeriodName(HomeAssistantStatisticPeriod value) => value switch
    {
        HomeAssistantStatisticPeriod.FiveMinute => "5minute", HomeAssistantStatisticPeriod.Hour => "hour",
        HomeAssistantStatisticPeriod.Day => "day", HomeAssistantStatisticPeriod.Week => "week",
        HomeAssistantStatisticPeriod.Month => "month",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };

    private static string TypeName(HomeAssistantStatisticType value) => value switch
    {
        HomeAssistantStatisticType.Change => "change", HomeAssistantStatisticType.LastReset => "last_reset",
        HomeAssistantStatisticType.Maximum => "max", HomeAssistantStatisticType.Mean => "mean",
        HomeAssistantStatisticType.Minimum => "min", HomeAssistantStatisticType.State => "state",
        HomeAssistantStatisticType.Sum => "sum", _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
