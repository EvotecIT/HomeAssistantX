using System.Globalization;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Services;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Recorder;

/// <summary>Provides Recorder history, logbook, statistics, and maintenance operations.</summary>
public sealed class HomeAssistantRecorderClient
{
    private readonly HomeAssistantWebSocketClient _webSocket;
    private readonly HomeAssistantServiceClient _services;

    internal HomeAssistantRecorderClient(HomeAssistantWebSocketClient webSocket, HomeAssistantServiceClient services)
    {
        _webSocket = webSocket;
        _services = services;
    }

    public async Task<IReadOnlyList<HomeAssistantStatisticMetadata>> GetStatisticsMetadataAsync(
        IReadOnlyCollection<string>? statisticIds = null,
        CancellationToken cancellationToken = default)
    {
        var requestedIdSnapshot = statisticIds is null ? null : RequireStatisticIds(statisticIds, nameof(statisticIds));
        var payload = requestedIdSnapshot is null ? null : new Dictionary<string, object?> { ["statistic_ids"] = requestedIdSnapshot };
        var value = await _webSocket.RequestAsync("recorder/get_statistics_metadata", payload, cancellationToken).ConfigureAwait(false);
        var metadata = DecodeMetadata(value, "Recorder statistics metadata could not be decoded.");
        var responseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (metadata.Any(item => !responseIds.Add(item.StatisticId)))
            throw new HomeAssistantProtocolException("Recorder statistics metadata contained a duplicate statistic identifier.");
        if (requestedIdSnapshot is not null)
        {
            var requestedIds = new HashSet<string>(requestedIdSnapshot, StringComparer.OrdinalIgnoreCase);
            if (metadata.Any(item => !requestedIds.Contains(item.StatisticId)))
                throw new HomeAssistantProtocolException("Recorder statistics metadata contained an unexpected statistic identifier.");
        }
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
        var metadata = DecodeMetadata(value, "Recorder statistic identifiers could not be decoded.");
        var responseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (metadata.Any(item => !responseIds.Add(item.StatisticId)))
        {
            throw new HomeAssistantProtocolException("Recorder statistic identifiers contained a duplicate identifier.");
        }

        return metadata;
    }

    public async Task<IReadOnlyList<HomeAssistantStatisticSeries>> GetStatisticsAsync(
        HomeAssistantStatisticsQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query is null) throw new ArgumentNullException(nameof(query));
        if (query.EndTime.HasValue && query.EndTime <= query.StartTime)
            throw new ArgumentOutOfRangeException(nameof(query), "The statistics end must be after the start.");
        var requestedIdSnapshot = RequireStatisticIds(query.StatisticIds, nameof(query));
        var payload = new Dictionary<string, object?>
        {
            ["start_time"] = query.StartTime.ToString("O", CultureInfo.InvariantCulture),
            ["statistic_ids"] = requestedIdSnapshot,
            ["period"] = PeriodName(query.Period)
        };
        if (query.EndTime.HasValue) payload["end_time"] = query.EndTime.Value.ToString("O", CultureInfo.InvariantCulture);
        if (query.Types is not null)
        {
            if (query.Types.Count == 0) throw new ArgumentException("Statistics types cannot be empty.", nameof(query));
            payload["types"] = query.Types.Select(TypeName).Distinct(StringComparer.Ordinal).ToArray();
        }
        if (query.Units is not null)
        {
            if (query.Units.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || string.IsNullOrWhiteSpace(pair.Value)))
                throw new ArgumentException("Statistics unit names and values must be non-empty.", nameof(query));
            var units = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pair in query.Units)
            {
                var normalizedName = pair.Key.Trim();
                if (units.ContainsKey(normalizedName))
                    throw new ArgumentException("Statistics unit names must be unique after normalization.", nameof(query));
                units.Add(normalizedName, pair.Value.Trim());
            }
            payload["units"] = units;
        }

        var value = await _webSocket.RequestAsync("recorder/statistics_during_period", payload, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object) throw new HomeAssistantProtocolException("Recorder statistics were not an object.");
        var series = new List<HomeAssistantStatisticSeries>();
        var requestedIds = new HashSet<string>(requestedIdSnapshot, StringComparer.OrdinalIgnoreCase);
        var responseIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (!HomeAssistantStatisticIdentifier.TryNormalize(property.Name, out var normalizedStatisticId)
                || !string.Equals(property.Name, normalizedStatisticId, StringComparison.Ordinal)
                || !requestedIds.Contains(normalizedStatisticId)
                || !responseIds.Add(normalizedStatisticId))
                throw new HomeAssistantProtocolException("Recorder statistics contained an unexpected or duplicate statistic identifier.");
            ValidateStatisticRows(property.Value);
            var rows = HomeAssistantJson.DeserializeResponse<HomeAssistantStatisticRow[]>(property.Value, "A Recorder statistics series could not be decoded.");
            series.Add(new HomeAssistantStatisticSeries { StatisticId = normalizedStatisticId, Rows = rows });
        }
        return series.OrderBy(item => item.StatisticId, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public Task<JsonElement> ValidateStatisticsAsync(CancellationToken cancellationToken = default)
        => _webSocket.RequestAsync("recorder/validate_statistics", null, cancellationToken);

    public async Task UpdateStatisticsIssuesAsync(CancellationToken cancellationToken = default)
        => _ = await _webSocket.RequestAsync("recorder/update_statistics_issues", null, cancellationToken).ConfigureAwait(false);

    public async Task ClearStatisticsAsync(IReadOnlyCollection<string> statisticIds, CancellationToken cancellationToken = default)
        => _ = await _webSocket.RequestAsync("recorder/clear_statistics", new Dictionary<string, object?> { ["statistic_ids"] = RequireStatisticIds(statisticIds, nameof(statisticIds)) }, cancellationToken).ConfigureAwait(false);

    public async Task UpdateStatisticsMetadataAsync(string statisticId, string? unitClass, string? unitOfMeasurement, CancellationToken cancellationToken = default)
    {
        unitClass = NormalizeOptionalUnit(unitClass, nameof(unitClass));
        unitOfMeasurement = NormalizeOptionalUnit(unitOfMeasurement, nameof(unitOfMeasurement));
        _ = await _webSocket.RequestAsync("recorder/update_statistics_metadata", new Dictionary<string, object?>
        {
            ["statistic_id"] = RequireStatisticId(statisticId, nameof(statisticId)),
            ["unit_class"] = unitClass,
            ["unit_of_measurement"] = unitOfMeasurement
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ChangeStatisticsUnitAsync(string statisticId, string? oldUnit, string? newUnit, CancellationToken cancellationToken = default)
    {
        oldUnit = NormalizeOptionalUnit(oldUnit, nameof(oldUnit));
        newUnit = NormalizeOptionalUnit(newUnit, nameof(newUnit));
        if (string.Equals(oldUnit, newUnit, StringComparison.Ordinal))
            throw new ArgumentException("The old and new statistics units must be different.", nameof(newUnit));
        _ = await _webSocket.RequestAsync("recorder/change_statistics_unit", new Dictionary<string, object?>
        {
            ["statistic_id"] = RequireStatisticId(statisticId, nameof(statisticId)),
            ["old_unit_of_measurement"] = oldUnit,
            ["new_unit_of_measurement"] = newUnit
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task AdjustSumStatisticsAsync(string statisticId, DateTimeOffset start, double adjustment, string? unit, CancellationToken cancellationToken = default)
    {
        if (double.IsNaN(adjustment) || double.IsInfinity(adjustment)) throw new ArgumentOutOfRangeException(nameof(adjustment));
        if (adjustment == 0d) throw new ArgumentOutOfRangeException(nameof(adjustment), "A statistics sum adjustment must be non-zero.");
        unit = NormalizeOptionalUnit(unit, nameof(unit));
        _ = await _webSocket.RequestAsync("recorder/adjust_sum_statistics", new Dictionary<string, object?>
        {
            ["statistic_id"] = RequireStatisticId(statisticId, nameof(statisticId)),
            ["start_time"] = start.ToString("O", CultureInfo.InvariantCulture),
            ["adjustment"] = adjustment,
            ["adjustment_unit_of_measurement"] = unit
        }, cancellationToken).ConfigureAwait(false);
    }

    public async Task ImportStatisticsAsync(HomeAssistantStatisticImportMetadata metadata, IReadOnlyCollection<HomeAssistantStatisticImportRow> rows, CancellationToken cancellationToken = default)
    {
        if (metadata is null) throw new ArgumentNullException(nameof(metadata));
        metadata.ValidateRows(rows);
        var unitClass = NormalizeOptionalUnit(metadata.UnitClass, nameof(metadata.UnitClass));
        var unitOfMeasurement = NormalizeOptionalUnit(metadata.UnitOfMeasurement, nameof(metadata.UnitOfMeasurement));
        var metadataPayload = new Dictionary<string, object?>
        {
            ["statistic_id"] = RequireStatisticId(metadata.StatisticId, nameof(metadata.StatisticId)),
            ["source"] = Require(metadata.Source, nameof(metadata.Source)),
            ["name"] = metadata.Name,
            ["has_mean"] = metadata.HasMean,
            ["has_sum"] = metadata.HasSum,
            ["unit_class"] = unitClass,
            ["unit_of_measurement"] = unitOfMeasurement
        };
        metadataPayload["mean_type"] = (int)metadata.MeanType;
        var rowPayload = rows.Select(ToImportPayload).ToArray();
        _ = await _webSocket.RequestAsync("recorder/import_statistics", new Dictionary<string, object?>
        {
            ["metadata"] = metadataPayload,
            ["stats"] = rowPayload
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
        if (entityIds is { Count: > 0 }) call.WithData("entity_id", RequireEntityIds(entityIds, nameof(entityIds)));
        if (domains is { Count: > 0 }) call.WithData("domains", RequireDomains(domains, nameof(domains)));
        if (entityGlobs is { Count: > 0 }) call.WithData("entity_globs", RequireIds(entityGlobs, nameof(entityGlobs)));
        if (keepDays.HasValue) call.WithData("keep_days", keepDays.Value);
        return _services.CallControlAsync(call, cancellationToken);
    }

    public Task<HomeAssistantServiceCallResult> SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
        => _services.CallControlAsync(new HomeAssistantServiceCall("recorder", enabled ? "enable" : "disable"), cancellationToken);

    private static IReadOnlyList<HomeAssistantStatisticMetadata> DecodeMetadata(JsonElement value, string failureMessage)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException(failureMessage);
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("statistic_id", out var statisticId)
                || statisticId.ValueKind != JsonValueKind.String
                || statisticId.GetString() is not string statisticIdValue
                || string.IsNullOrWhiteSpace(statisticIdValue)
                || !HomeAssistantStatisticIdentifier.TryNormalize(statisticIdValue, out var normalizedStatisticId)
                || !string.Equals(statisticIdValue, normalizedStatisticId, StringComparison.Ordinal)
                || !item.TryGetProperty("source", out var source)
                || source.ValueKind != JsonValueKind.String
                || source.GetString() is not string sourceValue
                || !HomeAssistantStatisticIdentifier.IsSlug(sourceValue)
                || (statisticIdValue.Contains(':')
                    ? !HomeAssistantStatisticIdentifier.TryNormalizeExternal(statisticIdValue, out _, out var statisticSource)
                        || !string.Equals(sourceValue, statisticSource, StringComparison.Ordinal)
                    : !string.Equals(sourceValue, "recorder", StringComparison.Ordinal))
                || !item.TryGetProperty("has_mean", out var hasMean)
                || hasMean.ValueKind is not (JsonValueKind.True or JsonValueKind.False)
                || !item.TryGetProperty("has_sum", out var hasSum)
                || hasSum.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                throw new HomeAssistantProtocolException(failureMessage);
            }
        }

        var metadata = HomeAssistantJson.DeserializeResponse<HomeAssistantStatisticMetadata[]>(value, failureMessage);
        if (metadata.Any(item => item.MeanType.HasValue && !Enum.IsDefined(typeof(HomeAssistantStatisticMeanType), item.MeanType.Value)))
            throw new HomeAssistantProtocolException(failureMessage);
        return metadata;
    }

    private static void ValidateStatisticRows(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException("A Recorder statistics series could not be decoded.");
        long? previousStart = null;
        long? previousEnd = null;
        foreach (var row in value.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.Object
                || !TryGetUnixMilliseconds(row, "start", required: true, out var start)
                || !TryGetUnixMilliseconds(row, "end", required: true, out var end)
                || !TryGetUnixMilliseconds(row, "last_reset", required: false, out _))
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series contained an invalid timestamp.");
            }

            if (end <= start)
            {
                throw new HomeAssistantProtocolException("A Recorder statistics series contained a non-positive interval.");
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

    private static string? NormalizeOptionalUnit(string? value, string parameterName)
    {
        if (value is null) return null;
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("A supplied unit cannot be empty.", parameterName);
        return value.Trim();
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

    private static string Require(string value, string name)
        => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("A non-empty value is required.", name) : value.Trim();

    private static string[] RequireIds(IReadOnlyCollection<string> values, string name)
    {
        if (values is null || values.Count == 0 || values.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("At least one non-empty identifier is required.", name);
        return values.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static string[] RequireEntityIds(IReadOnlyCollection<string> values, string name)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("At least one entity identifier is required.", name);
        }

        var normalized = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (!HomeAssistantEntityId.TryNormalize(value, out var entityId))
            {
                throw new ArgumentException("Entity identifiers must use the native Home Assistant format.", name);
            }

            if (!normalized.Contains(entityId, StringComparer.Ordinal))
            {
                normalized.Add(entityId);
            }
        }

        return normalized.ToArray();
    }

    private static string[] RequireStatisticIds(IReadOnlyCollection<string> values, string name)
    {
        if (values is null || values.Count == 0) throw new ArgumentException("At least one statistic identifier is required.", name);
        var normalized = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (!HomeAssistantStatisticIdentifier.TryNormalize(value, out var statisticId))
                throw new ArgumentException("Statistic identifiers must use '<domain>.<object>' or '<source>:<name>' with canonical lowercase slug segments.", name);
            if (!normalized.Contains(statisticId, StringComparer.Ordinal)) normalized.Add(statisticId);
        }
        return normalized.ToArray();
    }

    private static string RequireStatisticId(string value, string name)
    {
        if (!HomeAssistantStatisticIdentifier.TryNormalize(value, out var statisticId))
            throw new ArgumentException("Statistic identifiers must use '<domain>.<object>' or '<source>:<name>' with canonical lowercase slug segments.", name);
        return statisticId;
    }

    private static string[] RequireDomains(IReadOnlyCollection<string> values, string name)
    {
        if (values is null || values.Count == 0)
        {
            throw new ArgumentException("At least one domain is required.", name);
        }

        var normalized = new List<string>(values.Count);
        foreach (var value in values)
        {
            if (!HomeAssistantEntityId.TryNormalizeDomain(value, out var domain))
            {
                throw new ArgumentException("Domains must use the native Home Assistant format.", name);
            }

            if (!normalized.Contains(domain, StringComparer.Ordinal))
            {
                normalized.Add(domain);
            }
        }

        return normalized.ToArray();
    }

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
