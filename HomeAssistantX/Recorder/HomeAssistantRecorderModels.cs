using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeAssistantX.Recorder;

public enum HomeAssistantStatisticType
{
    Change,
    LastReset,
    Maximum,
    Mean,
    Minimum,
    State,
    Sum
}

public enum HomeAssistantStatisticPeriod
{
    FiveMinute,
    Hour,
    Day,
    Week,
    Month
}

public enum HomeAssistantStatisticKind
{
    Any,
    Mean,
    Sum
}

/// <summary>The algorithm used to aggregate mean values in imported statistics.</summary>
public enum HomeAssistantStatisticMeanType
{
    None = 0,
    Arithmetic = 1,
    Circular = 2
}

/// <summary>A long-term statistic registered with Home Assistant Recorder.</summary>
public sealed class HomeAssistantStatisticMetadata
{
    [JsonPropertyName("statistic_id")]
    public string StatisticId { get; set; } = string.Empty;

    [JsonPropertyName("source")]
    public string? Source { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("unit_of_measurement")]
    public string? UnitOfMeasurement { get; set; }

    [JsonPropertyName("statistics_unit_of_measurement")]
    public string? StatisticsUnitOfMeasurement { get; set; }

    [JsonPropertyName("unit_class")]
    public string? UnitClass { get; set; }

    [JsonPropertyName("has_mean")]
    public bool HasMean { get; set; }

    [JsonPropertyName("has_sum")]
    public bool HasSum { get; set; }

    [JsonPropertyName("mean_type")]
    public HomeAssistantStatisticMeanType? MeanType { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>One aggregated Recorder statistics period.</summary>
public sealed class HomeAssistantStatisticRow
{
    [JsonPropertyName("start")]
    public double StartSeconds { get; set; }

    [JsonPropertyName("end")]
    public double EndSeconds { get; set; }

    [JsonPropertyName("last_reset")]
    public double? LastResetSeconds { get; set; }

    [JsonPropertyName("change")]
    public double? Change { get; set; }

    [JsonPropertyName("max")]
    public double? Maximum { get; set; }

    [JsonPropertyName("mean")]
    public double? Mean { get; set; }

    [JsonPropertyName("min")]
    public double? Minimum { get; set; }

    [JsonPropertyName("state")]
    public double? State { get; set; }

    [JsonPropertyName("sum")]
    public double? Sum { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);

    [JsonIgnore]
    public DateTimeOffset Start => FromUnixSeconds(StartSeconds);

    [JsonIgnore]
    public DateTimeOffset End => FromUnixSeconds(EndSeconds);

    [JsonIgnore]
    public DateTimeOffset? LastReset => LastResetSeconds.HasValue
        ? FromUnixSeconds(LastResetSeconds.Value)
        : null;

    private static DateTimeOffset FromUnixSeconds(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new InvalidOperationException("Recorder returned a non-finite Unix timestamp.");
        return DateTimeOffset.FromUnixTimeMilliseconds(checked((long)(value * 1000d)));
    }
}

/// <summary>A statistics series and its Recorder identifier.</summary>
public sealed class HomeAssistantStatisticSeries
{
    public string StatisticId { get; set; } = string.Empty;

    public IReadOnlyList<HomeAssistantStatisticRow> Rows { get; set; } = Array.Empty<HomeAssistantStatisticRow>();
}

/// <summary>Filters a Recorder statistics query.</summary>
public sealed class HomeAssistantStatisticsQuery
{
    public HomeAssistantStatisticsQuery(DateTimeOffset startTime, HomeAssistantStatisticPeriod period, params string[] statisticIds)
    {
        if (statisticIds is null || statisticIds.Length == 0 || statisticIds.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one statistic identifier is required.", nameof(statisticIds));
        StartTime = startTime;
        Period = period;
        StatisticIds = statisticIds.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public DateTimeOffset StartTime { get; }
    public DateTimeOffset? EndTime { get; set; }
    public HomeAssistantStatisticPeriod Period { get; }
    public IReadOnlyList<string> StatisticIds { get; }
    public IReadOnlyCollection<HomeAssistantStatisticType>? Types { get; set; }
    public IReadOnlyDictionary<string, string>? Units { get; set; }
}

/// <summary>Metadata used when importing external Recorder statistics.</summary>
public sealed class HomeAssistantStatisticImportMetadata
{
    public string StatisticId { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string? Name { get; set; }
    public bool HasMean { get; set; }
    public bool HasSum { get; set; }
    public HomeAssistantStatisticMeanType MeanType { get; set; }
    public string? UnitClass { get; set; }
    public string? UnitOfMeasurement { get; set; }

    /// <summary>Validates metadata and rows as one Recorder import before dispatch.</summary>
    public void ValidateRows(IReadOnlyCollection<HomeAssistantStatisticImportRow> rows)
    {
        if (string.IsNullOrWhiteSpace(StatisticId)) throw new ArgumentException("Import metadata requires StatisticId.", nameof(StatisticId));
        if (string.IsNullOrWhiteSpace(Source)) throw new ArgumentException("Import metadata requires Source.", nameof(Source));
        var statisticId = StatisticId.Trim();
        var source = Source.Trim();
        var separator = statisticId.IndexOf(':');
        if (separator <= 0 || separator == statisticId.Length - 1 || statisticId.IndexOf(':', separator + 1) >= 0)
            throw new ArgumentException("External StatisticId must use the '<source>:<name>' format.", nameof(StatisticId));
        var statisticSource = statisticId.Substring(0, separator);
        var statisticName = statisticId.Substring(separator + 1);
        if (!IsSlug(statisticSource) || !IsSlug(statisticName) || !IsSlug(source))
            throw new ArgumentException("External StatisticId and Source must use lowercase Home Assistant slug segments.", nameof(StatisticId));
        if (!string.Equals(statisticSource, source, StringComparison.Ordinal))
            throw new ArgumentException("Source must exactly match the prefix before ':' in StatisticId.", nameof(Source));
        if (!Enum.IsDefined(typeof(HomeAssistantStatisticMeanType), MeanType)) throw new ArgumentOutOfRangeException(nameof(MeanType));
        if (!HasMean && !HasSum) throw new ArgumentException("Import metadata must enable mean or sum statistics.");
        if (HasMean != (MeanType != HomeAssistantStatisticMeanType.None))
            throw new ArgumentException("MeanType must be non-None exactly when HasMean is enabled.", nameof(MeanType));
        if (rows is null || rows.Count == 0) throw new ArgumentException("At least one statistics row is required.", nameof(rows));

        DateTimeOffset? previousStart = null;
        foreach (var row in rows)
        {
            if (row is null) throw new ArgumentException("Statistics rows cannot contain null values.", nameof(rows));
            if (row.Start.UtcDateTime.Ticks % TimeSpan.TicksPerHour != 0)
                throw new ArgumentException("Imported statistics must start at the top of an hour.", nameof(rows));
            if (previousStart.HasValue && row.Start.UtcDateTime.Ticks <= previousStart.Value.UtcDateTime.Ticks)
                throw new ArgumentException("Imported statistics rows must be strictly ordered from oldest to newest by Start.", nameof(rows));
            previousStart = row.Start;
            if (!HasMean && (row.Mean.HasValue || row.Minimum.HasValue || row.Maximum.HasValue))
                throw new ArgumentException("Mean, Minimum, and Maximum require HasMean metadata.", nameof(rows));
            if (!HasSum && (row.State.HasValue || row.Sum.HasValue || row.LastReset.HasValue))
                throw new ArgumentException("State, Sum, and LastReset require HasSum metadata.", nameof(rows));
            ValidateFinite(row.Mean, nameof(row.Mean));
            ValidateFinite(row.Minimum, nameof(row.Minimum));
            ValidateFinite(row.Maximum, nameof(row.Maximum));
            ValidateFinite(row.State, nameof(row.State));
            ValidateFinite(row.Sum, nameof(row.Sum));
            if (row.Minimum.HasValue && row.Maximum.HasValue && row.Minimum.Value > row.Maximum.Value)
                throw new ArgumentException("Imported statistics Minimum cannot exceed Maximum.", nameof(rows));
            if (MeanType == HomeAssistantStatisticMeanType.Arithmetic)
            {
                if (row.Mean.HasValue && row.Minimum.HasValue && row.Mean.Value < row.Minimum.Value)
                    throw new ArgumentException("Imported arithmetic statistics Mean cannot be below Minimum.", nameof(rows));
                if (row.Mean.HasValue && row.Maximum.HasValue && row.Mean.Value > row.Maximum.Value)
                    throw new ArgumentException("Imported arithmetic statistics Mean cannot exceed Maximum.", nameof(rows));
            }
        }
    }

    private static void ValidateFinite(double? value, string name)
    {
        if (value.HasValue && (double.IsNaN(value.Value) || double.IsInfinity(value.Value)))
            throw new ArgumentOutOfRangeException(name, "Imported statistics require finite numeric values.");
    }

    private static bool IsSlug(string value)
    {
        if (value.Length == 0
            || value[0] == '_'
            || value[value.Length - 1] == '_'
            || value.Contains("__"))
        {
            return false;
        }

        return value.All(character =>
            (character >= 'a' && character <= 'z')
            || (character >= '0' && character <= '9')
            || character == '_');
    }
}

/// <summary>One row imported into Recorder statistics.</summary>
public sealed class HomeAssistantStatisticImportRow
{
    public DateTimeOffset Start { get; set; }
    public double? Mean { get; set; }
    public double? Minimum { get; set; }
    public double? Maximum { get; set; }
    public DateTimeOffset? LastReset { get; set; }
    public double? State { get; set; }
    public double? Sum { get; set; }
}
