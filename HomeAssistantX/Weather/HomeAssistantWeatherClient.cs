using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.Subscriptions;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Weather;

/// <summary>Provides current observations, forecasts, convertible units, and reconnect-safe forecast subscriptions.</summary>
public sealed class HomeAssistantWeatherClient
{
    private readonly HomeAssistantStateClient _states;
    private readonly HomeAssistantServiceClient _services;
    private readonly HomeAssistantWebSocketClient _webSocket;

    internal HomeAssistantWeatherClient(HomeAssistantStateClient states, HomeAssistantServiceClient services, HomeAssistantWebSocketClient webSocket)
    {
        _states = states;
        _services = services;
        _webSocket = webSocket;
    }

    public async Task<IReadOnlyList<HomeAssistantWeatherObservation>> GetAsync(CancellationToken cancellationToken = default)
    {
        var states = await _states.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var weatherStates = HomeAssistantEntityId.RequireResponseDomainStates(states, "weather", cancellationToken).ToArray();
        var observations = new List<HomeAssistantWeatherObservation>(weatherStates.Length);
        foreach (var state in weatherStates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            observations.Add(ToObservation(state, cancellationToken));
        }
        cancellationToken.ThrowIfCancellationRequested();
        SortObservations(observations, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return observations;
    }

    public async Task<HomeAssistantWeatherObservation> GetAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId, cancellationToken);
        var state = await _states.GetAsync(normalizedEntityId, cancellationToken).ConfigureAwait(false);
        return ToObservation(
            HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId, cancellationToken),
            cancellationToken);
    }

    public async Task<HomeAssistantWeatherForecastUpdate> GetForecastAsync(
        string entityId,
        HomeAssistantWeatherForecastType type,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId, cancellationToken);
        var result = await _services.CallAsync(
            new HomeAssistantServiceCall("weather", "get_forecasts")
                .ForEntity(normalizedEntityId)
                .WithData("type", TypeName(type))
                .WithResponse(),
            cancellationToken).ConfigureAwait(false);
        if (!result.Response.HasValue
            || result.Response.Value.ValueKind != JsonValueKind.Object
            || HomeAssistantJson.HasDuplicateProperties(result.Response.Value, cancellationToken))
        {
            throw new HomeAssistantProtocolException("The weather forecast response did not contain the requested entity.");
        }

        var entity = RequireSingleForecastEntity(result.Response.Value, cancellationToken);
        HomeAssistantJson.ThrowIfStringTraversalCanceled(entity.Name, cancellationToken);
        if (!string.Equals(entity.Name, normalizedEntityId, StringComparison.Ordinal)
            || entity.Value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("The weather forecast response did not contain exactly the requested entity.");
        }

        var entityResult = entity.Value;
        if (HomeAssistantJson.HasDuplicateProperties(entityResult, cancellationToken))
            throw new HomeAssistantProtocolException("The weather forecast response contained duplicate JSON properties.");
        if (!entityResult.TryGetProperty("forecast", out var forecast))
        {
            throw new HomeAssistantProtocolException("The weather forecast response did not contain a forecast.");
        }

        return ParseUpdate(normalizedEntityId, type, forecast, entityResult, cancellationToken);
    }

    internal static JsonProperty RequireSingleForecastEntity(JsonElement response, CancellationToken cancellationToken)
    {
        JsonProperty? entity = null;
        foreach (var property in response.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            HomeAssistantJson.ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
            if (entity.HasValue)
                throw new HomeAssistantProtocolException("The weather forecast response did not contain exactly the requested entity.");
            entity = property;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return entity ?? throw new HomeAssistantProtocolException("The weather forecast response did not contain exactly the requested entity.");
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetConvertibleUnitsAsync(CancellationToken cancellationToken = default)
        => (await GetConvertibleUnitsResponseAsync(cancellationToken).ConfigureAwait(false)).Units;

    /// <summary>Returns typed convertible units while retaining response-level extension data.</summary>
    public async Task<HomeAssistantWeatherConvertibleUnitsResponse> GetConvertibleUnitsResponseAsync(
        CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("weather/convertible_units", null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("units", out var units) || units.ValueKind != JsonValueKind.Object)
            throw new HomeAssistantProtocolException("The weather convertible-unit response had an unexpected shape.");
        if (HomeAssistantJson.HasDuplicateProperties(value, cancellationToken))
            throw new HomeAssistantProtocolException("The weather convertible-unit response contained duplicate JSON properties.");
        var additionalData = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            HomeAssistantJson.ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
            if (!string.Equals(property.Name, "units", StringComparison.Ordinal))
                additionalData.Add(property.Name, property.Value);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return new HomeAssistantWeatherConvertibleUnitsResponse
        {
            Units = ParseConvertibleUnits(units, cancellationToken),
            AdditionalData = additionalData,
            Raw = value
        };
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseConvertibleUnits(
        JsonElement units,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in units.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            HomeAssistantJson.ThrowIfStringTraversalCanceled(property.Name, cancellationToken);
            if (!HomeAssistantEntityId.TryNormalizeDomain(property.Name, cancellationToken, out var normalizedCategory)
                || !string.Equals(property.Name, normalizedCategory, StringComparison.Ordinal)
                || result.ContainsKey(property.Name))
                throw new HomeAssistantProtocolException("The weather convertible-unit response contained a noncanonical or duplicate unit category.");
            if (property.Value.ValueKind != JsonValueKind.Array)
                throw new HomeAssistantProtocolException("A weather convertible-unit list was not an array.");
            var names = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in property.Value.EnumerateArray())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var name = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                HomeAssistantJson.ThrowIfStringTraversalCanceled(name, cancellationToken);
                if (name is null
                    || string.IsNullOrWhiteSpace(name)
                    || !string.Equals(name, name.Trim(), StringComparison.Ordinal))
                    throw new HomeAssistantProtocolException("A weather convertible-unit list contained a noncanonical value.");
                if (!seen.Add(name))
                    throw new HomeAssistantProtocolException("A weather convertible-unit list contained a duplicate value.");
                names.Add(name);
            }
            result[property.Name] = names;
        }
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    public Task<IHomeAssistantSubscription> SubscribeForecastAsync(
        string entityId,
        HomeAssistantWeatherForecastType type,
        Func<HomeAssistantWeatherForecastUpdate, CancellationToken, Task> handler,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId, cancellationToken);
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        var payload = new Dictionary<string, object?> { ["entity_id"] = normalizedEntityId, ["forecast_type"] = TypeName(type) };
        return _webSocket.SubscribeAsync("weather/subscribe_forecast", payload, async (value, token) =>
        {
            var update = HomeAssistantSubscriptionProjectionException.Capture(() =>
            {
                if (value.ValueKind != JsonValueKind.Object
                    || HomeAssistantJson.HasDuplicateProperties(value, token)
                    || !value.TryGetProperty("type", out var responseType)
                    || responseType.ValueKind != JsonValueKind.String
                    || !IsExpectedForecastType(responseType.GetString(), TypeName(type), token)
                    || !value.TryGetProperty("forecast", out var forecast))
                    throw new HomeAssistantProtocolException("The weather forecast subscription had an unexpected shape.");
                return ParseUpdate(normalizedEntityId, type, forecast, value, token);
            });
            await handler(update, token).ConfigureAwait(false);
        }, cancellationToken);
    }

    internal static void SortObservations(
        List<HomeAssistantWeatherObservation> observations,
        CancellationToken cancellationToken)
    {
        var comparer = new CancellationAwareStringComparer(StringComparer.OrdinalIgnoreCase, cancellationToken);
        CancellationAwareSort.Sort(observations, (left, right) => comparer.Compare(left.EntityId, right.EntityId));
    }

    internal static HomeAssistantWeatherObservation ToObservation(
        HomeAssistantState state,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!string.Equals(state.Domain, "weather", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The entity is not a weather entity.", nameof(state));
        if (!HasNonWhitespace(state.State, cancellationToken))
            throw new HomeAssistantProtocolException("The Home Assistant weather state omitted its required state value.");
        var humidity = ReadCurrentPercentage(state.Attributes, "humidity", cancellationToken);
        var cloudCoverage = ReadCurrentPercentage(state.Attributes, "cloud_coverage", cancellationToken);
        var windBearing = ReadCurrentWindBearing(state.Attributes, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var observation = new HomeAssistantWeatherObservation
        {
            EntityId = state.EntityId,
            Name = HomeAssistantAttributeReader.GetString(state.Attributes, "friendly_name", cancellationToken),
            Condition = state.State,
            Temperature = ReadCurrentNumber(state.Attributes, "temperature", cancellationToken),
            ApparentTemperature = ReadCurrentNumber(state.Attributes, "apparent_temperature", cancellationToken),
            DewPoint = ReadCurrentNumber(state.Attributes, "dew_point", cancellationToken),
            Pressure = ReadCurrentNumber(state.Attributes, "pressure", cancellationToken),
            Humidity = humidity,
            CloudCoverage = cloudCoverage,
            UvIndex = ReadCurrentNumber(state.Attributes, "uv_index", cancellationToken),
            Visibility = ReadCurrentNumber(state.Attributes, "visibility", cancellationToken),
            WindSpeed = ReadCurrentNumber(state.Attributes, "wind_speed", cancellationToken),
            WindGustSpeed = ReadCurrentNumber(state.Attributes, "wind_gust_speed", cancellationToken),
            WindBearing = windBearing,
            TemperatureUnit = ReadCurrentUnit(state.Attributes, "temperature_unit", cancellationToken),
            PressureUnit = ReadCurrentUnit(state.Attributes, "pressure_unit", cancellationToken),
            VisibilityUnit = ReadCurrentUnit(state.Attributes, "visibility_unit", cancellationToken),
            WindSpeedUnit = ReadCurrentUnit(state.Attributes, "wind_speed_unit", cancellationToken),
            PrecipitationUnit = ReadCurrentUnit(state.Attributes, "precipitation_unit", cancellationToken),
            SupportedFeatures = ReadSupportedFeatures(state.Attributes, cancellationToken),
            RawState = state
        };
        cancellationToken.ThrowIfCancellationRequested();
        return observation;
    }

    private static double? ReadCurrentPercentage(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!HomeAssistantAttributeReader.TryGetValue(attributes, name, out var raw, cancellationToken)
            || raw.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (raw.ValueKind != JsonValueKind.Number
            || !raw.TryGetDouble(out var value)
            || double.IsNaN(value)
            || double.IsInfinity(value)
            || value < 0d
            || value > 100d)
        {
            throw new HomeAssistantProtocolException(
                "The Home Assistant weather state contained an invalid percentage attribute.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    private static double? ReadCurrentNumber(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!HomeAssistantAttributeReader.TryGetValue(attributes, name, out var raw, cancellationToken)
            || raw.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (raw.ValueKind != JsonValueKind.Number
            || !raw.TryGetDouble(out var value)
            || double.IsNaN(value)
            || double.IsInfinity(value))
        {
            throw new HomeAssistantProtocolException(
                "The Home Assistant weather state contained an invalid numeric attribute.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return value;
    }

    private static string? ReadCurrentUnit(
        IReadOnlyDictionary<string, JsonElement> attributes,
        string name,
        CancellationToken cancellationToken)
    {
        if (!HomeAssistantAttributeReader.TryGetValue(attributes, name, out var raw, cancellationToken)
            || raw.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (raw.ValueKind != JsonValueKind.String
            || raw.GetString() is not string value
            || !IsCanonicalProviderText(value, cancellationToken))
        {
            throw new HomeAssistantProtocolException(
                "The Home Assistant weather state contained an invalid unit attribute.");
        }

        return value;
    }

    private static string? ReadCurrentWindBearing(
        IReadOnlyDictionary<string, JsonElement> attributes,
        CancellationToken cancellationToken)
    {
        if (!HomeAssistantAttributeReader.TryGetValue(attributes, "wind_bearing", out var raw, cancellationToken)
            || raw.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (raw.ValueKind == JsonValueKind.String
            && raw.GetString() is string text
            && HasNonWhitespace(text, cancellationToken))
        {
            return text;
        }

        if (raw.ValueKind == JsonValueKind.Number
            && raw.TryGetDouble(out var number)
            && !double.IsNaN(number)
            && !double.IsInfinity(number))
        {
            cancellationToken.ThrowIfCancellationRequested();
            return raw.GetRawText();
        }

        throw new HomeAssistantProtocolException(
            "The Home Assistant weather state contained an invalid wind-bearing attribute.");
    }

    private static bool IsCanonicalProviderText(string value, CancellationToken cancellationToken)
    {
        if (!HasNonWhitespace(value, cancellationToken)) return false;
        cancellationToken.ThrowIfCancellationRequested();
        return !char.IsWhiteSpace(value[0]) && !char.IsWhiteSpace(value[value.Length - 1]);
    }

    private static bool HasNonWhitespace(string? value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value is null) return false;
        var found = false;
        for (var index = 0; index < value.Length; index++)
        {
            if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
            found |= !char.IsWhiteSpace(value[index]);
        }
        cancellationToken.ThrowIfCancellationRequested();
        return found;
    }

    private static HomeAssistantWeatherFeature ReadSupportedFeatures(
        IReadOnlyDictionary<string, JsonElement> attributes,
        CancellationToken cancellationToken)
    {
        if (!HomeAssistantAttributeReader.TryGetValue(
                attributes,
                "supported_features",
                out var raw,
                cancellationToken)
            || raw.ValueKind == JsonValueKind.Null)
        {
            return HomeAssistantWeatherFeature.None;
        }

        if (raw.ValueKind != JsonValueKind.Number
            || !raw.TryGetInt64(out var value)
            || value < 0
            || value > int.MaxValue)
        {
            throw new HomeAssistantProtocolException(
                "The Home Assistant weather state contained invalid supported_features.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        return (HomeAssistantWeatherFeature)(int)value;
    }

    private static bool IsExpectedForecastType(
        string? actual,
        string expected,
        CancellationToken cancellationToken)
    {
        HomeAssistantJson.ThrowIfStringTraversalCanceled(actual, cancellationToken);
        return string.Equals(actual, expected, StringComparison.Ordinal);
    }

    internal static HomeAssistantWeatherForecastUpdate ParseUpdate(
        string entityId,
        HomeAssistantWeatherForecastType type,
        JsonElement forecast,
        JsonElement raw,
        CancellationToken cancellationToken)
    {
        if (forecast.ValueKind == JsonValueKind.Null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new HomeAssistantWeatherForecastUpdate { EntityId = entityId, Type = type, IsAvailable = false, Raw = raw };
        }
        if (forecast.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException("The weather forecast was not an array.");
        foreach (var value in forecast.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.ValueKind != JsonValueKind.Object
                || HomeAssistantJson.HasDuplicateProperties(value, cancellationToken)
                || !value.TryGetProperty("datetime", out var timestamp)
                || timestamp.ValueKind != JsonValueKind.String
                || !HasValidTimestamp(timestamp.GetString(), cancellationToken)
                || !HasFiniteForecastNumbers(value, cancellationToken)
                || !HasValidForecastPercentages(value, cancellationToken))
            {
                throw new HomeAssistantProtocolException("The weather forecast contained an invalid period value.");
            }
        }
        var items = HomeAssistantJson.DeserializeResponse<HomeAssistantWeatherForecast[]>(
            forecast,
            "The weather forecast could not be decoded.",
            cancellationToken: cancellationToken);
        DateTimeOffset? previous = null;
        foreach (var item in items)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (item is null || item.DateTime == default
                || (type == HomeAssistantWeatherForecastType.TwiceDaily && !item.IsDaytime.HasValue))
                throw new HomeAssistantProtocolException("The weather forecast omitted a required period field.");
            if (previous.HasValue && item.DateTime <= previous.Value)
                throw new HomeAssistantProtocolException("The weather forecast periods were not strictly increasing.");
            previous = item.DateTime;
        }
        return new HomeAssistantWeatherForecastUpdate
        {
            EntityId = entityId,
            Type = type,
            IsAvailable = true,
            Forecast = items,
            Raw = raw
        };
    }

    private static bool HasFiniteForecastNumbers(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        foreach (var propertyName in new[]
        {
            "temperature",
            "templow",
            "apparent_temperature",
            "dew_point",
            "precipitation",
            "precipitation_probability",
            "pressure",
            "humidity",
            "cloud_coverage",
            "uv_index",
            "wind_speed",
            "wind_gust_speed"
        })
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HasFiniteOptionalNumber(value, propertyName, cancellationToken))
            {
                return false;
            }
        }

        if (value.TryGetProperty("wind_bearing", out var windBearing)
            && !IsValidWindBearing(windBearing, cancellationToken))
        {
            return false;
        }

        return true;
    }

    private static bool HasValidForecastPercentages(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        foreach (var propertyName in new[] { "humidity", "cloud_coverage", "precipitation_probability" })
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!value.TryGetProperty(propertyName, out var number) || number.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            if (number.ValueKind != JsonValueKind.Number
                || !number.TryGetDouble(out var parsed)
                || parsed < 0d
                || parsed > 100d)
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsValidWindBearing(
        JsonElement value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (value.ValueKind == JsonValueKind.Null) return true;
        if (value.ValueKind == JsonValueKind.String)
            return HasNonWhitespace(value.GetString(), cancellationToken);
        return value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var bearing)
            && !double.IsNaN(bearing)
            && !double.IsInfinity(bearing);
    }

    private static bool HasFiniteOptionalNumber(
        JsonElement value,
        string propertyName,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!value.TryGetProperty(propertyName, out var number) || number.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return number.ValueKind == JsonValueKind.Number
            && number.TryGetDouble(out var parsed)
            && !double.IsNaN(parsed)
            && !double.IsInfinity(parsed);
    }

    private static bool HasValidTimestamp(
        string? value,
        CancellationToken cancellationToken)
    {
        HomeAssistantJson.ThrowIfStringTraversalCanceled(value, cancellationToken);
        return HomeAssistantTimestamp.TryParse(value, out _);
    }

    private static string NormalizeEntityId(
        string entityId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "weather", cancellationToken, out var normalized))
            throw new ArgumentException("A weather entity identifier is required.", nameof(entityId));
        cancellationToken.ThrowIfCancellationRequested();
        return normalized;
    }

    private static string TypeName(HomeAssistantWeatherForecastType value) => value switch
    {
        HomeAssistantWeatherForecastType.Daily => "daily",
        HomeAssistantWeatherForecastType.Hourly => "hourly",
        HomeAssistantWeatherForecastType.TwiceDaily => "twice_daily",
        _ => throw new ArgumentOutOfRangeException(nameof(value))
    };
}
