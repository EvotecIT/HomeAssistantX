#if NET10_0
using System.Text.Json;
using HomeAssistantX.Authentication;
using HomeAssistantX.Energy;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Recorder;
using HomeAssistantX.Tests.Infrastructure;
using HomeAssistantX.Weather;

namespace HomeAssistantX.Tests;

public sealed class EnergyRecorderWeatherContractTests
{
    [Fact]
    public async Task EnergyReadsValidationForecastAndFossilDataWithoutLosingProviderFields()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var preferences = await client.Energy.GetPreferencesAsync();
        Assert.True(preferences.EnergySources[0].GetProperty("provider_extension").GetBoolean());
        var info = await client.Energy.GetInfoAsync();
        Assert.Equal("forecast_solar", Assert.Single(info.SolarForecastDomains));
        Assert.Equal("sensor.grid_cost", info.CostSensors.GetProperty("sensor.grid_energy").GetString());
        var validation = await client.Energy.ValidateAsync();
        Assert.True(validation.GetProperty("future_validation").GetProperty("valid").GetBoolean());
        var forecast = await client.Energy.GetSolarForecastAsync();
        Assert.True(forecast["entry-solar"].GetProperty("future_provider_field").GetBoolean());

        var fossil = await client.Energy.GetFossilEnergyConsumptionAsync(
            new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero),
            new[] { "sensor.grid_energy" },
            "sensor.co2_intensity",
            HomeAssistantEnergyPeriod.Hour);
        Assert.Equal(0.42, fossil[0].EnergyKiloWattHours);
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("energy/fossil_energy_consumption")));
        Assert.Equal("hour", command.RootElement.GetProperty("period").GetString());
    }

    [Fact]
    public async Task EnergyPreferenceUpdateIsPartialAndRequiresJsonArrays()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var document = JsonDocument.Parse("[{\"type\":\"grid\",\"flow_from\":[]}]");

        await client.Energy.SavePreferencesAsync(new HomeAssistantEnergyPreferencesUpdate
        {
            EnergySources = document.RootElement.Clone()
        });
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("energy/save_prefs")));
        Assert.True(command.RootElement.TryGetProperty("energy_sources", out _));
        Assert.False(command.RootElement.TryGetProperty("device_consumption", out _));

        using var invalid = JsonDocument.Parse("{}");
        await Assert.ThrowsAsync<ArgumentException>(() => client.Energy.SavePreferencesAsync(
            new HomeAssistantEnergyPreferencesUpdate { EnergySources = invalid.RootElement.Clone() }));

        foreach (var invalidEntries in new[] { "[null]", "[1]", "[\"sensor.energy\"]" })
        {
            using var invalidArray = JsonDocument.Parse(invalidEntries);
            server.ClearLastWebSocketCommand("energy/save_prefs");
            await Assert.ThrowsAsync<ArgumentException>(() => client.Energy.SavePreferencesAsync(
                new HomeAssistantEnergyPreferencesUpdate { DeviceConsumption = invalidArray.RootElement.Clone() }));
            Assert.Null(server.GetLastWebSocketCommand("energy/save_prefs"));
        }
    }

    [Theory]
    [InlineData("{\"energy_sources\":null,\"device_consumption\":[],\"device_consumption_water\":[]}")]
    [InlineData("{\"energy_sources\":[1],\"device_consumption\":[],\"device_consumption_water\":[]}")]
    [InlineData("{\"energy_sources\":[],\"device_consumption\":{},\"device_consumption_water\":[]}")]
    [InlineData("{\"energy_sources\":[],\"device_consumption\":[],\"device_consumption_water\":[null]}")]
    public async Task EnergyPreferenceResponsesRequireObjectArrays(string response)
    {
        using var server = new TestHomeAssistantServer { EnergyPreferencesResponseJson = response };
        using var client = TestClientFactory.Create(server);
        using var update = JsonDocument.Parse("[]");

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetPreferencesAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.SavePreferencesAsync(
            new HomeAssistantEnergyPreferencesUpdate { EnergySources = update.RootElement.Clone() }));
    }

    [Fact]
    public async Task EnergyPreferenceResponsesAllowTheOptionalWaterCollectionToBeOmitted()
    {
        using var server = new TestHomeAssistantServer
        {
            EnergyPreferencesResponseJson = "{\"energy_sources\":[],\"device_consumption\":[]}"
        };
        using var client = TestClientFactory.Create(server);

        Assert.Equal(JsonValueKind.Undefined, (await client.Energy.GetPreferencesAsync()).DeviceConsumptionWater.ValueKind);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":null}")]
    [InlineData("{\"cost_sensors\":null,\"solar_forecast_domains\":[]}")]
    [InlineData("{\"cost_sensors\":[],\"solar_forecast_domains\":[]}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":[null]}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":[\" \" ]}")]
    public async Task EnergyInfoRequiresBothTypedCapabilityCollections(string response)
    {
        using var server = new TestHomeAssistantServer { EnergyInfoResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetInfoAsync());
    }

    [Fact]
    public async Task WeatherGetterRejectsAValidButDifferentResponseEntity()
    {
        using var server = new TestHomeAssistantServer
        {
            ExactStateResponseJson = "{\"entity_id\":\"weather.office\",\"state\":\"sunny\",\"attributes\":{}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync("weather.home"));
    }

    [Fact]
    public async Task RecorderStatisticsUseTypedMetadataRowsAndExactWireNames()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var catalog = await client.Recorder.ListStatisticsAsync(HomeAssistantStatisticKind.Sum);
        var metadata = Assert.Single(catalog);
        Assert.True(metadata.HasSum);
        Assert.Equal("kept", metadata.AdditionalData["future_metadata"].GetString());
        Assert.Equal("also-kept", metadata.AdditionalData["Future_Metadata"].GetString());
        var query = new HomeAssistantStatisticsQuery(
            new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero),
            HomeAssistantStatisticPeriod.Hour,
            "sensor.grid_energy")
        {
            EndTime = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            Types = new[] { HomeAssistantStatisticType.Change, HomeAssistantStatisticType.Sum },
            Units = new Dictionary<string, string> { ["energy"] = "kWh" }
        };
        var series = Assert.Single(await client.Recorder.GetStatisticsAsync(query));
        var row = Assert.Single(series.Rows);
        Assert.Equal(1.5, row.Change);
        Assert.Equal(10.5, row.Sum);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1787731200), row.Start);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1787734800), row.End);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1787727600), row.LastReset);
        Assert.True(row.AdditionalData["future_row"].GetBoolean());
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/statistics_during_period")));
        Assert.Equal(new[] { "change", "sum" }, command.RootElement.GetProperty("types").EnumerateArray().Select(value => value.GetString()).ToArray());
    }

    [Fact]
    public async Task RecorderAdministrativeOperationsValidateAndSerializeBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Recorder.UpdateStatisticsMetadataAsync("sensor.grid_energy", " energy ", " kWh ");
        using (var metadata = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/update_statistics_metadata"))))
        {
            Assert.Equal("energy", metadata.RootElement.GetProperty("unit_class").GetString());
            Assert.Equal("kWh", metadata.RootElement.GetProperty("unit_of_measurement").GetString());
        }
        await client.Recorder.AdjustSumStatisticsAsync("sensor.grid_energy", DateTimeOffset.UtcNow, 1.25, "kWh");
        await client.Recorder.ClearStatisticsAsync(new[] { "sensor.grid_energy" });
        await client.Recorder.ClearStatisticsAsync(new[] { " external:daily_energy " });
        await client.Recorder.UpdateStatisticsIssuesAsync();
        await client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:daily_energy", Source = "external", Name = "Daily energy",
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None, UnitClass = " energy ", UnitOfMeasurement = " kWh "
            },
            new[] { new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Sum = 1.5 } });
        using (var import = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/import_statistics"))))
        {
            Assert.False(import.RootElement.GetProperty("metadata").GetProperty("has_mean").GetBoolean());
            Assert.Equal("energy", import.RootElement.GetProperty("metadata").GetProperty("unit_class").GetString());
            Assert.Equal("kWh", import.RootElement.GetProperty("metadata").GetProperty("unit_of_measurement").GetString());
        }

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.UpdateStatisticsMetadataAsync(
            "sensor.grid_energy", " ", "kWh"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:blank_unit", Source = "external",
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None,
                UnitOfMeasurement = " "
            },
            new[] { new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Sum = 1.5 } }));

        foreach (var invalidStatisticId in new[]
        {
            "external:Daily Energy",
            "External:daily_energy",
            "_external:daily_energy",
            "external_:daily_energy",
            "external:daily__energy",
            "external:_daily_energy",
            "external:daily_energy_"
        })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
                new HomeAssistantStatisticImportMetadata
                {
                    StatisticId = invalidStatisticId,
                    Source = invalidStatisticId.Substring(0, invalidStatisticId.IndexOf(':')),
                    HasMean = false,
                    HasSum = true,
                    MeanType = HomeAssistantStatisticMeanType.None
                },
                new[] { new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Sum = 1.5 } }));
        }

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:mean", Source = "external",
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None
            },
            new[] { new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Mean = 1.5 } }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:invalid_mean", Source = "external",
                HasMean = true, HasSum = false, MeanType = HomeAssistantStatisticMeanType.None
            },
            new[] { new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Mean = 1.5 } }));
        foreach (var invalidRange in new[]
        {
            new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Mean = 5, Minimum = 10, Maximum = 1 },
            new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Mean = 0, Minimum = 1, Maximum = 10 },
            new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Mean = 11, Minimum = 1, Maximum = 10 }
        })
        {
            await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
                new HomeAssistantStatisticImportMetadata
                {
                    StatisticId = "external:invalid_range", Source = "external",
                    HasMean = true, HasSum = false, MeanType = HomeAssistantStatisticMeanType.Arithmetic
                },
                new[] { invalidRange }));
        }

        new HomeAssistantStatisticImportMetadata
        {
            StatisticId = "external:circular_direction", Source = "external",
            HasMean = true, HasSum = false, MeanType = HomeAssistantStatisticMeanType.Circular
        }.ValidateRows(new[]
        {
            new HomeAssistantStatisticImportRow
            {
                Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
                Mean = 0,
                Minimum = 10,
                Maximum = 350
            }
        });

        server.ClearLastWebSocketCommand("recorder/import_statistics");
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:source_mismatch", Source = "homeassistantx",
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None
            },
            new[] { new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Sum = 1.5 } }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:unaligned", Source = "external",
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None
            },
            new[] { new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 1, 0, TimeSpan.Zero), Sum = 1.5 } }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:offset_unaligned", Source = "external",
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None
            },
            new[] { new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.FromMinutes(330)), Sum = 1.5 } }));
        Assert.Null(server.GetLastWebSocketCommand("recorder/import_statistics"));

        new HomeAssistantStatisticImportMetadata
        {
            StatisticId = "external:offset_aligned", Source = "external",
            HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None
        }.ValidateRows(new[]
        {
            new HomeAssistantStatisticImportRow
            {
                Start = new DateTimeOffset(2026, 8, 26, 10, 30, 0, TimeSpan.FromMinutes(330)),
                Sum = 1.5
            }
        });

        new HomeAssistantStatisticImportMetadata
        {
            StatisticId = "external:sparse", Source = "external",
            HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None
        }.ValidateRows(new[]
        {
            new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 11, 0, 0, TimeSpan.Zero) }
        });

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:unordered", Source = "external",
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None
            },
            new[]
            {
                new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 11, 0, 0, TimeSpan.Zero), Sum = 2 },
                new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Sum = 1 }
            }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:duplicate", Source = "external",
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None
            },
            new[]
            {
                new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero), Sum = 1 },
                new HomeAssistantStatisticImportRow { Start = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.FromHours(2)), Sum = 2 }
            }));
        Assert.Null(server.GetLastWebSocketCommand("recorder/import_statistics"));

        server.ClearLastWebSocketCommand("recorder/clear_statistics");
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ClearStatisticsAsync(Array.Empty<string>()));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ClearStatisticsAsync(new[] { "sensor.Grid_Energy" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ClearStatisticsAsync(new[] { "external:Daily_Energy" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(new[] { "sensor.Kitchen" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(domains: new[] { "SENSOR" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(domains: new[] { "sensor__bad" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { " " }));
        Assert.Null(server.GetLastWebSocketCommand("recorder/clear_statistics"));
        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task WeatherCurrentForecastUnitsAndSubscriptionAreTypedAndPushBased()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[" +
            "{\"entity_id\":\"weather.home\",\"state\":\"partlycloudy\",\"attributes\":{\"friendly_name\":\"Home\",\"temperature\":21.5,\"temperature_unit\":\"°C\",\"humidity\":55,\"wind_bearing\":180,\"supported_features\":3}}," +
            "{\"entity_id\":\"weather.bad\",\"state\":\"unknown\",\"attributes\":{\"supported_features\":4294967297}}]");
        using var client = TestClientFactory.Create(server);

        var observations = await client.Weather.GetAsync();
        var observation = Assert.Single(observations, value => value.EntityId == "weather.home");
        Assert.Equal(21.5, observation.Temperature);
        Assert.Equal("180", observation.WindBearing);
        Assert.True(observation.Supports(HomeAssistantWeatherForecastType.Daily));
        Assert.False(
            Assert.Single(observations, value => value.EntityId == "weather.bad")
                .Supports(HomeAssistantWeatherForecastType.Daily));
        Assert.Equal("weather.home", (await client.Weather.GetAsync(" weather.home ")).EntityId);
        var forecast = await client.Weather.GetForecastAsync(" weather.home ", HomeAssistantWeatherForecastType.Daily);
        var daily = Assert.Single(forecast.Forecast);
        Assert.Equal(24.5, daily.Temperature);
        Assert.Equal("kept", daily.AdditionalData["future_field"].GetString());
        var units = await client.Weather.GetConvertibleUnitsAsync();
        Assert.Contains("°C", units["temperature_unit"]);

        var received = new TaskCompletionSource<HomeAssistantWeatherForecastUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.Weather.SubscribeForecastAsync(
            " weather.home ", HomeAssistantWeatherForecastType.Hourly,
            (update, _) => { received.TrySetResult(update); return Task.CompletedTask; });
        var streamed = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(HomeAssistantWeatherForecastType.Hourly, streamed.Type);
        Assert.Equal(1.2, Assert.Single(streamed.Forecast).Precipitation);
    }

    [Theory]
    [InlineData("\"weather.home.extra\"")]
    [InlineData("null")]
    [InlineData("\" weather.home\"")]
    public async Task WeatherBulkReadRejectsMalformedServerEntityIds(string entityIdJson)
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":" + entityIdJson + ",\"state\":\"sunny\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
    }

    [Fact]
    public async Task WeatherForecastRejectsNullEntriesAsProtocolFailures()
    {
        using var server = new TestHomeAssistantServer
        {
            WeatherForecastResponseJson = "{\"weather.home\":{\"forecast\":[null]}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Weather.GetForecastAsync("weather.home", HomeAssistantWeatherForecastType.Daily));
    }

    [Fact]
    public async Task WeatherConvertibleUnitsRejectMalformedEntriesAsProtocolFailures()
    {
        using var server = new TestHomeAssistantServer
        {
            WeatherConvertibleUnitsResponseJson = "{\"units\":{\"temperature_unit\":[\"°C\",null]}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetConvertibleUnitsAsync());
    }

    [Theory]
    [InlineData("{\"forecast\":[]}")]
    [InlineData("{\"type\":\"daily\",\"forecast\":[]}")]
    public async Task WeatherSubscriptionRejectsMissingOrMismatchedForecastTypes(string eventJson)
    {
        using var server = new TestHomeAssistantServer { WeatherForecastSubscriptionEventJson = eventJson };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Weather.SubscribeForecastAsync(
            "weather.home",
            HomeAssistantWeatherForecastType.Hourly,
            (_, _) => Task.CompletedTask);

        var exception = await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);

        Assert.Contains("unexpected shape", exception.Message);
    }

    [Fact]
    public async Task RecorderStatisticsRejectNullRowsAsProtocolFailures()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.grid_energy\":[null]}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(
                DateTimeOffset.UtcNow.AddHours(-1),
                HomeAssistantStatisticPeriod.Hour,
                "sensor.grid_energy")));
    }

    [Fact]
    public async Task RecorderMetadataRejectsNullEntriesAsProtocolFailures()
    {
        using var server = new TestHomeAssistantServer { RecorderMetadataResponseJson = "[null]" };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.ListStatisticsAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsMetadataAsync());
    }

    [Fact]
    public async Task RecorderCatalogRejectsDuplicateIdentifiersCaseInsensitively()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson = "[{\"statistic_id\":\"sensor.energy\",\"has_mean\":false,\"has_sum\":true},{\"statistic_id\":\"SENSOR.ENERGY\",\"has_mean\":false,\"has_sum\":true}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.ListStatisticsAsync());
    }

    [Fact]
    public async Task EnergyResponsesRejectDuplicateLogicalKeys()
    {
        using var server = new TestHomeAssistantServer
        {
            SolarForecastResponseJson = "{\"entry-a\":{},\"ENTRY-A\":{}}",
            FossilEnergyResponseJson = "{\"2026-08-26T10:00:00Z\":0.4,\"2026-08-26T12:00:00+02:00\":0.5}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetSolarForecastAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            DateTimeOffset.Parse("2026-08-26T00:00:00Z"),
            DateTimeOffset.Parse("2026-08-27T00:00:00Z"),
            new[] { "sensor.grid_energy" },
            "sensor.co2",
            HomeAssistantEnergyPeriod.Hour));
    }

    [Theory]
    [InlineData("{\"weather.home\":{\"forecast\":[{\"datetime\":\"2026-08-27T11:00:00Z\"},{\"datetime\":\"2026-08-27T10:00:00Z\"}]}}")]
    [InlineData("{\"weather.home\":{\"forecast\":[{\"datetime\":\"2026-08-27T10:00:00Z\"},{\"datetime\":\"2026-08-27T12:00:00+02:00\"}]}}")]
    public async Task WeatherForecastRequiresStrictlyIncreasingPeriods(string response)
    {
        using var server = new TestHomeAssistantServer { WeatherForecastResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetForecastAsync(
            "weather.home", HomeAssistantWeatherForecastType.Hourly));
    }

    [Fact]
    public async Task WeatherTypedReadsAndUnitCategoriesRejectAmbiguousResponses()
    {
        using var server = new TestHomeAssistantServer
        {
            ExactStateResponseJson = "{\"entity_id\":\"weather.home\",\"state\":null,\"attributes\":{}}",
            WeatherConvertibleUnitsResponseJson = "{\"units\":{\"temperature_unit\":[\"°C\"],\"TEMPERATURE_UNIT\":[\"°F\"]}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync("weather.home"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetConvertibleUnitsAsync());
    }

    [Theory]
    [InlineData("[{}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"has_mean\":false}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"has_mean\":0,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\" \",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"sensor.Bad\",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"not an id\",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\" sensor.energy\",\"has_mean\":false,\"has_sum\":true}]")]
    public async Task RecorderMetadataRequiresIdentityAndCapabilityFields(string response)
    {
        using var server = new TestHomeAssistantServer { RecorderMetadataResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.ListStatisticsAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsMetadataAsync());
    }

    [Theory]
    [InlineData("{\"sensor.energy\":[{\"end\":1}]}" )]
    [InlineData("{\"sensor.energy\":[{\"start\":1}]}" )]
    [InlineData("{\"sensor.energy\":[{\"start\":\"1\",\"end\":2}]}" )]
    [InlineData("{\"sensor.energy\":[{\"start\":1e300,\"end\":2}]}" )]
    [InlineData("{\"sensor.energy\":[{\"start\":1.9,\"end\":2}]}" )]
    [InlineData("{\"sensor.energy\":[{\"start\":1,\"end\":2,\"last_reset\":1e300}]}" )]
    public async Task RecorderStatisticsRequireRepresentableTimestamps(string response)
    {
        using var server = new TestHomeAssistantServer { RecorderStatisticsResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.energy")));
    }

    [Theory]
    [InlineData("{\"sensor.other\":[{\"start\":1,\"end\":2}]}")]
    [InlineData("{\"sensor.energy\":[{\"start\":2,\"end\":2}]}")]
    [InlineData("{\"sensor.energy\":[{\"start\":3,\"end\":2}]}")]
    public async Task RecorderStatisticsCorrelateIdentifiersAndRequirePositiveIntervals(string response)
    {
        using var server = new TestHomeAssistantServer { RecorderStatisticsResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.energy")));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("[{\"statistic_id\":\"sensor.other\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true},{\"statistic_id\":\"SENSOR.ENERGY\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true}]")]
    public async Task FilteredRecorderMetadataCorrelatesReturnedIdentifiers(string response)
    {
        using var server = new TestHomeAssistantServer { RecorderMetadataResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Recorder.GetStatisticsMetadataAsync(new[] { "sensor.energy" }));
    }

    [Fact]
    public async Task RecorderSelectorsAreFrozenBeforeTokenResolutionAndDispatch()
    {
        using var server = new TestHomeAssistantServer();
        var tokenProvider = new BlockingTokenProvider();
        using var client = TestClientFactory.Create(server, accessTokenProvider: tokenProvider);
        var query = new HomeAssistantStatisticsQuery(
            DateTimeOffset.UtcNow.AddHours(-1),
            HomeAssistantStatisticPeriod.Hour,
            "sensor.grid_energy");

        var request = client.Recorder.GetStatisticsAsync(query);
        await tokenProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.IsType<string[]>(query.StatisticIds)[0] = "sensor.other";
        tokenProvider.Release.TrySetResult(TestHomeAssistantServer.AccessToken);

        Assert.Equal("sensor.grid_energy", Assert.Single(await request).StatisticId);
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/statistics_during_period")));
        Assert.Equal("sensor.grid_energy", command.RootElement.GetProperty("statistic_ids")[0].GetString());
    }

    [Fact]
    public async Task FilteredRecorderMetadataSelectorsAreFrozenBeforeTokenResolutionAndDispatch()
    {
        using var server = new TestHomeAssistantServer();
        var tokenProvider = new BlockingTokenProvider();
        using var client = TestClientFactory.Create(server, accessTokenProvider: tokenProvider);
        var statisticIds = new List<string> { "sensor.grid_energy" };

        var request = client.Recorder.GetStatisticsMetadataAsync(statisticIds);
        await tokenProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        statisticIds[0] = "sensor.other";
        tokenProvider.Release.TrySetResult(TestHomeAssistantServer.AccessToken);

        Assert.Equal("sensor.grid_energy", Assert.Single(await request).StatisticId);
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/get_statistics_metadata")));
        Assert.Equal("sensor.grid_energy", command.RootElement.GetProperty("statistic_ids")[0].GetString());
    }

    [Theory]
    [InlineData("sensor.Bad")]
    [InlineData("sensor.energy.extra")]
    [InlineData("source:Bad")]
    [InlineData("source:")]
    public async Task RecorderMutationsRejectMalformedStatisticIdentifiersBeforeDispatch(string statisticId)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.UpdateStatisticsMetadataAsync(statisticId, null, "kWh"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ChangeStatisticsUnitAsync(statisticId, "Wh", "kWh"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.AdjustSumStatisticsAsync(statisticId, DateTimeOffset.UtcNow, 1, "kWh"));

        Assert.Null(server.GetLastWebSocketCommand("recorder/update_statistics_metadata"));
        Assert.Null(server.GetLastWebSocketCommand("recorder/change_statistics_unit"));
        Assert.Null(server.GetLastWebSocketCommand("recorder/adjust_sum_statistics"));
    }

    [Fact]
    public async Task RecorderStatisticsNormalizeUnitOverridesAndRequireStrictRowOrdering()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":2,\"end\":3},{\"start\":1,\"end\":2}]}"
        };
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.energy")
        {
            Units = new Dictionary<string, string> { [" energy "] = " kWh " }
        };

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(query));
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/statistics_during_period")));
        Assert.Equal("kWh", command.RootElement.GetProperty("units").GetProperty("energy").GetString());
    }

    [Fact]
    public async Task RecorderStatisticsRejectDuplicateNormalizedUnitNamesBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.energy")
        {
            Units = new Dictionary<string, string> { ["energy"] = "kWh", [" ENERGY "] = "Wh" }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.GetStatisticsAsync(query));
        Assert.Null(server.GetLastWebSocketCommand("recorder/statistics_during_period"));
    }

    [Theory]
    [InlineData("2026-08-27T12:00:00")]
    [InlineData("2026-08-27 12:00:00Z")]
    public async Task WeatherForecastRequiresAnExplicitStrictOffset(string timestamp)
    {
        using var server = new TestHomeAssistantServer
        {
            WeatherForecastResponseJson = "{\"weather.home\":{\"forecast\":[{\"datetime\":\"" + timestamp + "\"}]}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Weather.GetForecastAsync("weather.home", HomeAssistantWeatherForecastType.Daily));
    }

    [Fact]
    public async Task RecorderUnitChangesRejectIdenticalEndpointsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ChangeStatisticsUnitAsync("sensor.energy", "kWh", " kWh "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ChangeStatisticsUnitAsync("sensor.energy", null, null));

        Assert.Null(server.GetLastWebSocketCommand("recorder/change_statistics_unit"));
    }

    [Theory]
    [InlineData("12:00")]
    [InlineData("2026/08/27T10:00:00Z")]
    [InlineData("2026-08-27T10:00:00 +00:00")]
    public async Task FossilEnergyRejectsInvalidTimestampKeys(string timestamp)
    {
        using var server = new TestHomeAssistantServer
        {
            FossilEnergyResponseJson = "{\"" + timestamp + "\":0.42}"
        };
        using var client = TestClientFactory.Create(server);
        var now = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            now.AddHours(-1),
            now,
            new[] { "sensor.energy" },
            "sensor.co2",
            HomeAssistantEnergyPeriod.Hour));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("sensor.Kitchen")]
    public async Task LogbookEntityFilterRejectsValuesThatCouldBroadenTheQuery(string entityId)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.GetLogbookAsync(
            new HomeAssistantX.Rest.HomeAssistantLogbookQuery { EntityId = entityId }));
    }

    [Fact]
    public async Task InvalidRangesDomainsAndFiniteNumbersFailBeforeNetworkDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var now = DateTimeOffset.UtcNow;

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            now, now, new[] { "sensor.energy" }, "sensor.co2", HomeAssistantEnergyPeriod.Hour));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Weather.GetAsync("sensor.weather"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Weather.GetAsync("weather."));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Weather.GetForecastAsync(
            "weather.home.extra",
            HomeAssistantWeatherForecastType.Daily));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Recorder.AdjustSumStatisticsAsync(
            "sensor.energy", now, double.NaN, "kWh"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.AdjustSumStatisticsAsync(
            "sensor.energy", now, 1, " "));
        Assert.Null(server.GetLastWebSocketCommand("energy/fossil_energy_consumption"));
        Assert.Null(server.GetLastWebSocketCommand("recorder/adjust_sum_statistics"));
    }

    private sealed class BlockingTokenProvider : IHomeAssistantAccessTokenProvider
    {
        internal TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource<string> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
        {
            Started.TrySetResult(true);
            return await Release.Task.WaitAsync(cancellationToken);
        }
    }
}
#endif
