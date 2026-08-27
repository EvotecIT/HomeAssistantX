using System.Text.Json;
using System.Text.Json.Serialization;
using HomeAssistantX.Models;

namespace HomeAssistantX.Weather;

public enum HomeAssistantWeatherForecastType
{
    Daily,
    Hourly,
    TwiceDaily
}

[Flags]
public enum HomeAssistantWeatherFeature
{
    None = 0,
    DailyForecast = 1,
    HourlyForecast = 2,
    TwiceDailyForecast = 4
}

/// <summary>A typed current observation from a Home Assistant weather entity.</summary>
public sealed class HomeAssistantWeatherObservation
{
    public string EntityId { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Condition { get; set; }
    public double? Temperature { get; set; }
    public double? ApparentTemperature { get; set; }
    public double? DewPoint { get; set; }
    public double? Pressure { get; set; }
    public double? Humidity { get; set; }
    public double? CloudCoverage { get; set; }
    public double? UvIndex { get; set; }
    public double? Visibility { get; set; }
    public double? WindSpeed { get; set; }
    public double? WindGustSpeed { get; set; }
    public string? WindBearing { get; set; }
    public string? TemperatureUnit { get; set; }
    public string? PressureUnit { get; set; }
    public string? VisibilityUnit { get; set; }
    public string? WindSpeedUnit { get; set; }
    public string? PrecipitationUnit { get; set; }
    public HomeAssistantWeatherFeature SupportedFeatures { get; set; }
    public HomeAssistantState RawState { get; set; } = new();

    public bool Supports(HomeAssistantWeatherForecastType type)
    {
        var feature = type switch
        {
            HomeAssistantWeatherForecastType.Daily => HomeAssistantWeatherFeature.DailyForecast,
            HomeAssistantWeatherForecastType.Hourly => HomeAssistantWeatherFeature.HourlyForecast,
            HomeAssistantWeatherForecastType.TwiceDaily => HomeAssistantWeatherFeature.TwiceDailyForecast,
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
        return (SupportedFeatures & feature) != 0;
    }
}

/// <summary>One converted forecast period returned by a Home Assistant weather entity.</summary>
public sealed class HomeAssistantWeatherForecast
{
    [JsonPropertyName("datetime")]
    public DateTimeOffset DateTime { get; set; }

    [JsonPropertyName("condition")]
    public string? Condition { get; set; }

    [JsonPropertyName("is_daytime")]
    public bool? IsDaytime { get; set; }

    [JsonPropertyName("temperature")]
    public double? Temperature { get; set; }

    [JsonPropertyName("templow")]
    public double? LowTemperature { get; set; }

    [JsonPropertyName("apparent_temperature")]
    public double? ApparentTemperature { get; set; }

    [JsonPropertyName("dew_point")]
    public double? DewPoint { get; set; }

    [JsonPropertyName("precipitation")]
    public double? Precipitation { get; set; }

    [JsonPropertyName("precipitation_probability")]
    public double? PrecipitationProbability { get; set; }

    [JsonPropertyName("pressure")]
    public double? Pressure { get; set; }

    [JsonPropertyName("humidity")]
    public double? Humidity { get; set; }

    [JsonPropertyName("cloud_coverage")]
    public double? CloudCoverage { get; set; }

    [JsonPropertyName("uv_index")]
    public double? UvIndex { get; set; }

    [JsonPropertyName("wind_bearing")]
    public JsonElement? WindBearing { get; set; }

    [JsonPropertyName("wind_speed")]
    public double? WindSpeed { get; set; }

    [JsonPropertyName("wind_gust_speed")]
    public double? WindGustSpeed { get; set; }

    [JsonExtensionData]
    public Dictionary<string, JsonElement> AdditionalData { get; set; } = new(StringComparer.Ordinal);
}

/// <summary>A current forecast list and its entity/type identity.</summary>
public sealed class HomeAssistantWeatherForecastUpdate
{
    public string EntityId { get; set; } = string.Empty;
    public HomeAssistantWeatherForecastType Type { get; set; }
    public bool IsAvailable { get; set; }
    public IReadOnlyList<HomeAssistantWeatherForecast> Forecast { get; set; } = Array.Empty<HomeAssistantWeatherForecast>();
    public JsonElement Raw { get; set; }
}
