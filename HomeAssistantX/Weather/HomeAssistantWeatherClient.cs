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
            observations.Add(ToObservation(state));
        }
        cancellationToken.ThrowIfCancellationRequested();
        var comparer = new CancellationAwareStringComparer(StringComparer.OrdinalIgnoreCase, cancellationToken);
        observations.Sort((left, right) => comparer.Compare(left.EntityId, right.EntityId));
        cancellationToken.ThrowIfCancellationRequested();
        return observations;
    }

    public async Task<HomeAssistantWeatherObservation> GetAsync(string entityId, CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        var state = await _states.GetAsync(normalizedEntityId, cancellationToken).ConfigureAwait(false);
        return ToObservation(HomeAssistantEntityId.RequireResponseEntity(state, normalizedEntityId));
    }

    public async Task<HomeAssistantWeatherForecastUpdate> GetForecastAsync(
        string entityId,
        HomeAssistantWeatherForecastType type,
        CancellationToken cancellationToken = default)
    {
        var normalizedEntityId = NormalizeEntityId(entityId);
        var result = await _services.CallAsync(
            new HomeAssistantServiceCall("weather", "get_forecasts")
                .ForEntity(normalizedEntityId)
                .WithData("type", TypeName(type))
                .WithResponse(),
            cancellationToken).ConfigureAwait(false);
        if (!result.Response.HasValue || result.Response.Value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("The weather forecast response did not contain the requested entity.");
        }

        var entities = result.Response.Value.EnumerateObject().ToArray();
        if (entities.Length != 1
            || !string.Equals(entities[0].Name, normalizedEntityId, StringComparison.Ordinal)
            || entities[0].Value.ValueKind != JsonValueKind.Object)
        {
            throw new HomeAssistantProtocolException("The weather forecast response did not contain exactly the requested entity.");
        }

        var entityResult = entities[0].Value;
        if (!entityResult.TryGetProperty("forecast", out var forecast))
        {
            throw new HomeAssistantProtocolException("The weather forecast response did not contain a forecast.");
        }

        return ParseUpdate(normalizedEntityId, type, forecast, entityResult, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetConvertibleUnitsAsync(CancellationToken cancellationToken = default)
    {
        var value = await _webSocket.RequestAsync("weather/convertible_units", null, cancellationToken).ConfigureAwait(false);
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("units", out var units) || units.ValueKind != JsonValueKind.Object)
            throw new HomeAssistantProtocolException("The weather convertible-unit response had an unexpected shape.");
        return ParseConvertibleUnits(units, cancellationToken);
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> ParseConvertibleUnits(
        JsonElement units,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in units.EnumerateObject())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!HomeAssistantEntityId.TryNormalizeDomain(property.Name, out var normalizedCategory)
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
                if (item.ValueKind != JsonValueKind.String
                    || item.GetString() is not string name
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
        var normalizedEntityId = NormalizeEntityId(entityId);
        if (handler is null) throw new ArgumentNullException(nameof(handler));
        var payload = new Dictionary<string, object?> { ["entity_id"] = normalizedEntityId, ["forecast_type"] = TypeName(type) };
        return _webSocket.SubscribeAsync("weather/subscribe_forecast", payload, async (value, token) =>
        {
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty("type", out var responseType)
                || responseType.ValueKind != JsonValueKind.String
                || !string.Equals(responseType.GetString(), TypeName(type), StringComparison.Ordinal)
                || !value.TryGetProperty("forecast", out var forecast))
                throw new HomeAssistantProtocolException("The weather forecast subscription had an unexpected shape.");
            await handler(ParseUpdate(normalizedEntityId, type, forecast, value, token), token).ConfigureAwait(false);
        }, cancellationToken);
    }

    private static HomeAssistantWeatherObservation ToObservation(HomeAssistantState state)
    {
        if (!string.Equals(state.Domain, "weather", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("The entity is not a weather entity.", nameof(state));
        if (string.IsNullOrWhiteSpace(state.State))
            throw new HomeAssistantProtocolException("The Home Assistant weather state omitted its required state value.");
        return new HomeAssistantWeatherObservation
        {
            EntityId = state.EntityId,
            Name = HomeAssistantAttributeReader.GetString(state.Attributes, "friendly_name"),
            Condition = state.State,
            Temperature = HomeAssistantAttributeReader.GetDouble(state.Attributes, "temperature"),
            ApparentTemperature = HomeAssistantAttributeReader.GetDouble(state.Attributes, "apparent_temperature"),
            DewPoint = HomeAssistantAttributeReader.GetDouble(state.Attributes, "dew_point"),
            Pressure = HomeAssistantAttributeReader.GetDouble(state.Attributes, "pressure"),
            Humidity = HomeAssistantAttributeReader.GetDouble(state.Attributes, "humidity"),
            CloudCoverage = HomeAssistantAttributeReader.GetDouble(state.Attributes, "cloud_coverage"),
            UvIndex = HomeAssistantAttributeReader.GetDouble(state.Attributes, "uv_index"),
            Visibility = HomeAssistantAttributeReader.GetDouble(state.Attributes, "visibility"),
            WindSpeed = HomeAssistantAttributeReader.GetDouble(state.Attributes, "wind_speed"),
            WindGustSpeed = HomeAssistantAttributeReader.GetDouble(state.Attributes, "wind_gust_speed"),
            WindBearing = HomeAssistantAttributeReader.GetString(state.Attributes, "wind_bearing"),
            TemperatureUnit = HomeAssistantAttributeReader.GetString(state.Attributes, "temperature_unit"),
            PressureUnit = HomeAssistantAttributeReader.GetString(state.Attributes, "pressure_unit"),
            VisibilityUnit = HomeAssistantAttributeReader.GetString(state.Attributes, "visibility_unit"),
            WindSpeedUnit = HomeAssistantAttributeReader.GetString(state.Attributes, "wind_speed_unit"),
            PrecipitationUnit = HomeAssistantAttributeReader.GetString(state.Attributes, "precipitation_unit"),
            SupportedFeatures = (HomeAssistantWeatherFeature)(HomeAssistantAttributeReader.GetNonNegativeInt32(state.Attributes, "supported_features") ?? 0),
            RawState = state
        };
    }

    private static HomeAssistantWeatherForecastUpdate ParseUpdate(
        string entityId,
        HomeAssistantWeatherForecastType type,
        JsonElement forecast,
        JsonElement raw,
        CancellationToken cancellationToken)
    {
        if (forecast.ValueKind == JsonValueKind.Null)
            return new HomeAssistantWeatherForecastUpdate { EntityId = entityId, Type = type, IsAvailable = false, Raw = raw.Clone() };
        if (forecast.ValueKind != JsonValueKind.Array)
            throw new HomeAssistantProtocolException("The weather forecast was not an array.");
        foreach (var value in forecast.EnumerateArray())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (value.ValueKind != JsonValueKind.Object
                || !value.TryGetProperty("datetime", out var timestamp)
                || timestamp.ValueKind != JsonValueKind.String
                || !HomeAssistantTimestamp.TryParse(timestamp.GetString(), out _)
                || !HasFiniteForecastNumbers(value))
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
            Raw = raw.Clone()
        };
    }

    private static bool HasFiniteForecastNumbers(JsonElement value)
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
            if (!HasFiniteOptionalNumber(value, propertyName))
            {
                return false;
            }
        }

        if (value.TryGetProperty("wind_bearing", out var windBearing)
            && !IsValidWindBearing(windBearing))
        {
            return false;
        }

        return true;
    }

    private static bool IsValidWindBearing(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Null) return true;
        if (value.ValueKind == JsonValueKind.String)
            return !string.IsNullOrWhiteSpace(value.GetString());
        return value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var bearing)
            && !double.IsNaN(bearing)
            && !double.IsInfinity(bearing);
    }

    private static bool HasFiniteOptionalNumber(JsonElement value, string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var number) || number.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        return number.ValueKind == JsonValueKind.Number
            && number.TryGetDouble(out var parsed)
            && !double.IsNaN(parsed)
            && !double.IsInfinity(parsed);
    }

    private static string NormalizeEntityId(string entityId)
    {
        if (!HomeAssistantEntityId.TryNormalizeForDomain(entityId, "weather", out var normalized))
            throw new ArgumentException("A weather entity identifier is required.", nameof(entityId));
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
