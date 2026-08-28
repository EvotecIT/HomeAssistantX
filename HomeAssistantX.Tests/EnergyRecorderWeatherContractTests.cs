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

    [Fact]
    public async Task EnergyPreferenceUpdateHonorsCancellationBeforeTraversingCallerJson()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var document = JsonDocument.Parse("[{\"type\":\"grid\"}]");
        var callerOwned = document.RootElement;
        document.Dispose();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Energy.SavePreferencesAsync(
            new HomeAssistantEnergyPreferencesUpdate { EnergySources = callerOwned },
            cancellation.Token));
        Assert.Null(server.GetLastWebSocketCommand("energy/save_prefs"));
    }

    [Theory]
    [InlineData("[{\"type\":\"grid\",\"type\":\"solar\"}]")]
    [InlineData("[{\"type\":\"grid\",\"nested\":{\"value\":1,\"value\":2}}]")]
    public async Task EnergyPreferenceUpdateRejectsDuplicateCallerPropertiesBeforeDispatch(string json)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var document = JsonDocument.Parse(json);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Energy.SavePreferencesAsync(
            new HomeAssistantEnergyPreferencesUpdate { EnergySources = document.RootElement.Clone() }));

        Assert.Null(server.GetLastWebSocketCommand("energy/save_prefs"));
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
    [InlineData("{\"energy_sources\":[],\"energy_sources\":[{}],\"device_consumption\":[]}")]
    [InlineData("{\"energy_sources\":[],\"device_consumption\":[],\"device_consumption\":[]}")]
    public async Task EnergyPreferenceResponsesRejectDuplicateCollectionProperties(string response)
    {
        using var server = new TestHomeAssistantServer { EnergyPreferencesResponseJson = response };
        using var client = TestClientFactory.Create(server);
        using var update = JsonDocument.Parse("[]");

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetPreferencesAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.SavePreferencesAsync(
            new HomeAssistantEnergyPreferencesUpdate { EnergySources = update.RootElement.Clone() }));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":null}")]
    [InlineData("{\"cost_sensors\":null,\"solar_forecast_domains\":[]}")]
    [InlineData("{\"cost_sensors\":[],\"solar_forecast_domains\":[]}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":[null]}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":[\" \" ]}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":[\"Forecast_Solar\"]}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":[\"forecast_solar\",\"forecast_solar\"]}")]
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

    [Theory]
    [InlineData(HomeAssistantStatisticKind.Mean, false, true)]
    [InlineData(HomeAssistantStatisticKind.Sum, true, false)]
    public async Task RecorderCatalogCorrelatesRowsToTheRequestedStatisticKind(
        HomeAssistantStatisticKind kind,
        bool hasMean,
        bool hasSum)
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson = "[{\"statistic_id\":\"sensor.energy\",\"source\":\"recorder\",\"has_mean\":"
                + hasMean.ToString().ToLowerInvariant() + ",\"has_sum\":" + hasSum.ToString().ToLowerInvariant() + "}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.ListStatisticsAsync(kind));
    }

    [Fact]
    public async Task RecorderMeanCatalogAcceptsCircularStatisticsWithoutLegacyArithmeticFlag()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson = "[{\"statistic_id\":\"sensor.direction\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":false,\"mean_type\":2}]"
        };
        using var client = TestClientFactory.Create(server);

        var metadata = Assert.Single(await client.Recorder.ListStatisticsAsync(HomeAssistantStatisticKind.Mean));

        Assert.False(metadata.HasMean);
        Assert.Equal(HomeAssistantStatisticMeanType.Circular, metadata.MeanType);
    }

    [Theory]
    [InlineData("Energy")]
    [InlineData(" energy")]
    [InlineData("energy ")]
    [InlineData("not-a-unit")]
    public async Task RecorderStatisticsRequireCanonicalUnitClassKeysBeforeDispatch(string unitClass)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(
            DateTimeOffset.UtcNow.AddHours(-1),
            HomeAssistantStatisticPeriod.Hour,
            "sensor.grid_energy")
        {
            Units = new Dictionary<string, string> { [unitClass] = "kWh" }
        };

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.GetStatisticsAsync(query));

        Assert.Null(server.GetLastWebSocketCommand("recorder/statistics_during_period"));
    }

    [Fact]
    public async Task RecorderStatisticsRejectNullUnitClassKeysAsArguments()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(
            DateTimeOffset.UtcNow.AddHours(-1),
            HomeAssistantStatisticPeriod.Hour,
            "sensor.grid_energy")
        {
            Units = new NullKeyUnits()
        };

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.GetStatisticsAsync(query));
        Assert.Null(server.GetLastWebSocketCommand("recorder/statistics_during_period"));
    }

    [Theory]
    [InlineData("Energy")]
    [InlineData(" energy")]
    [InlineData("energy ")]
    [InlineData("not-a-unit")]
    public async Task RecorderMetadataMutationsRequireCanonicalUnitClassesBeforeDispatch(string unitClass)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var rows = new[]
        {
            new HomeAssistantStatisticImportRow
            {
                Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
                Sum = 1.5
            }
        };

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Recorder.UpdateStatisticsMetadataAsync("sensor.grid_energy", unitClass, "kWh"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:daily_energy",
                Source = "external",
                HasSum = true,
                MeanType = HomeAssistantStatisticMeanType.None,
                UnitClass = unitClass,
                UnitOfMeasurement = "kWh"
            },
            rows));

        Assert.Null(server.GetLastWebSocketCommand("recorder/update_statistics_metadata"));
        Assert.Null(server.GetLastWebSocketCommand("recorder/import_statistics"));
    }

    [Theory]
    [InlineData("Energy", "kWh")]
    [InlineData(" energy ", "kWh")]
    [InlineData(null, " ")]
    public void RecorderImportMetadataPublicPreflightValidatesUnits(string? unitClass, string? unitOfMeasurement)
    {
        var metadata = new HomeAssistantStatisticImportMetadata
        {
            StatisticId = "external:daily_energy",
            Source = "external",
            HasSum = true,
            MeanType = HomeAssistantStatisticMeanType.None,
            UnitClass = unitClass,
            UnitOfMeasurement = unitOfMeasurement
        };

        Assert.Throws<ArgumentException>(() => metadata.ValidateRows(new[]
        {
            new HomeAssistantStatisticImportRow
            {
                Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
                Sum = 1.5
            }
        }));
    }

    [Fact]
    public async Task RecorderMetadataRejectsNoncanonicalResponseUnitClasses()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson = "[{\"statistic_id\":\"sensor.energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true,\"unit_class\":\"Energy\"}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsMetadataAsync());
    }

    [Theory]
    [InlineData("{\"sensor.grid_energy\":[{\"start\":1,\"end\":2,\"min\":10,\"max\":1}]}")]
    [InlineData("{\"sensor.grid_energy\":[{\"start\":1,\"end\":2,\"min\":\"bad\",\"max\":1}]}")]
    public async Task RecorderStatisticsRejectInvalidDecodedRanges(string response)
    {
        using var server = new TestHomeAssistantServer { RecorderStatisticsResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.grid_energy")));
    }

    [Fact]
    public async Task RecorderAdministrativeOperationsValidateAndSerializeBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Recorder.UpdateStatisticsMetadataAsync("sensor.grid_energy", "energy", " kWh ");
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
                HasMean = false, HasSum = true, MeanType = HomeAssistantStatisticMeanType.None, UnitClass = "energy", UnitOfMeasurement = " kWh "
            },
            new[]
            {
                new HomeAssistantStatisticImportRow
                {
                    Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
                    LastReset = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero),
                    Sum = 1.5
                }
            });
        using (var import = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/import_statistics"))))
        {
            Assert.False(import.RootElement.GetProperty("metadata").GetProperty("has_mean").GetBoolean());
            Assert.Equal("energy", import.RootElement.GetProperty("metadata").GetProperty("unit_class").GetString());
            Assert.Equal("kWh", import.RootElement.GetProperty("metadata").GetProperty("unit_of_measurement").GetString());
            var row = Assert.Single(import.RootElement.GetProperty("stats").EnumerateArray());
            Assert.Equal(
                new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                row.GetProperty("start").GetString());
            Assert.Equal(
                new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero).ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                row.GetProperty("last_reset").GetString());
        }

        await client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { " sensor.* ", "binary_sensor.door_?", "sensor.room_[0-9]", "sensor.*" });
        using (var purge = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody)))
        {
            Assert.Equal(
                new[] { "sensor.*", "binary_sensor.door_?", "sensor.room_[0-9]" },
                purge.RootElement.GetProperty("service_data").GetProperty("entity_globs").EnumerateArray().Select(value => value.GetString()).ToArray());
        }
        server.ClearLastServiceCall();

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

        var circularMetadata = new HomeAssistantStatisticImportMetadata
        {
            StatisticId = "external:circular_direction", Source = "external",
            HasMean = false, HasSum = false, MeanType = HomeAssistantStatisticMeanType.Circular
        };
        var circularRows = new[]
        {
            new HomeAssistantStatisticImportRow
            {
                Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
                Mean = 0,
                Minimum = 10,
                Maximum = 350
            }
        };
        await client.Recorder.ImportStatisticsAsync(circularMetadata, circularRows);
        using (var circular = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/import_statistics"))))
        {
            Assert.False(circular.RootElement.GetProperty("metadata").GetProperty("has_mean").GetBoolean());
            Assert.Equal(2, circular.RootElement.GetProperty("metadata").GetProperty("mean_type").GetInt32());
        }

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
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { "Sensor.*" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { "sensor*" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { "sensor.[bad" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { "sensor__bad.*" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { "sensor_.kitchen" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { "sensor._kitchen" }));
        Assert.Null(server.GetLastWebSocketCommand("recorder/clear_statistics"));
        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task RecorderMetadataMutationsPreserveAnEmptyUnitSentinel()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Recorder.UpdateStatisticsMetadataAsync("sensor.grid_energy", "energy", string.Empty);

        using var command = JsonDocument.Parse(Assert.IsType<string>(
            server.GetLastWebSocketCommand("recorder/update_statistics_metadata")));
        Assert.Equal(string.Empty, command.RootElement.GetProperty("unit_of_measurement").GetString());
    }

    [Fact]
    public async Task RecorderImportObservesCancellationDuringRowPreparation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        var rows = new CancellingStatisticRows(cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Recorder.ImportStatisticsAsync(
            new HomeAssistantStatisticImportMetadata
            {
                StatisticId = "external:daily_energy",
                Source = "external",
                HasSum = true,
                MeanType = HomeAssistantStatisticMeanType.None,
                UnitClass = "energy",
                UnitOfMeasurement = "kWh"
            },
            rows,
            cancellation.Token));

        Assert.Null(server.GetLastWebSocketCommand("recorder/import_statistics"));
    }

    [Fact]
    public async Task RecorderImportSnapshotsCallerRowsOnceBeforeValidation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var rows = new SingleEnumerationStatisticRows();

        await client.Recorder.ImportStatisticsAsync(
            CreateSumImportMetadata(),
            rows);

        Assert.Equal(1, rows.EnumerationCount);
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/import_statistics")));
        Assert.Equal(2, command.RootElement.GetProperty("stats").GetArrayLength());
    }

    [Fact]
    public void RecorderImportValidationObservesCancellationAfterEnumerationCompletes()
    {
        using var cancellation = new CancellationTokenSource();

        Assert.ThrowsAny<OperationCanceledException>(() => CreateSumImportMetadata().ValidateRows(
            new CancelAtEndStatisticRows(cancellation),
            cancellation.Token));
    }

    [Fact]
    public async Task WeatherCurrentForecastUnitsAndSubscriptionAreTypedAndPushBased()
    {
        using var server = new TestHomeAssistantServer
        {
            WeatherConvertibleUnitsResponseJson = "{\"units\":{\"temperature_unit\":[\"°C\",\"°F\"],\"wind_speed_unit\":[\"km/h\",\"m/s\"]},\"future_weather_contract\":{\"enabled\":true}}"
        };
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
        var unitsResponse = await client.Weather.GetConvertibleUnitsResponseAsync();
        Assert.Contains("°C", unitsResponse.Units["temperature_unit"]);
        Assert.True(unitsResponse.AdditionalData["future_weather_contract"].GetProperty("enabled").GetBoolean());
        Assert.Equal(JsonValueKind.Object, unitsResponse.Raw.ValueKind);
        Assert.Contains("°C", (await client.Weather.GetConvertibleUnitsAsync())["temperature_unit"]);

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

    [Fact]
    public void WeatherConvertibleUnitProjectionObservesCancellation()
    {
        using var document = JsonDocument.Parse("{\"temperature_unit\":[\"°C\",\"°F\"]}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantWeatherClient.ParseConvertibleUnits(document.RootElement, cancellation.Token));
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

    [Theory]
    [InlineData("SENSOR.GRID_ENERGY")]
    [InlineData(" sensor.grid_energy ")]
    [InlineData("sensor.grid_energy.extra")]
    public async Task RecorderStatisticsRejectNoncanonicalResponseIdentifiers(string responseIdentifier)
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"" + responseIdentifier + "\":[{\"start\":1787731200000,\"end\":1787734800000}]}"
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
    [InlineData("{\"\":{}}")]
    [InlineData("{\" \":{}}")]
    [InlineData("{\" entry-a \":{}}")]
    public async Task SolarForecastRejectsBlankOrPaddedConfigurationEntryIdentifiers(string response)
    {
        using var server = new TestHomeAssistantServer { SolarForecastResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetSolarForecastAsync());
    }

    [Theory]
    [InlineData("{\"entry-a\":null}")]
    [InlineData("{\"entry-a\":[]}")]
    [InlineData("{\"entry-a\":1}")]
    [InlineData("{\"entry-a\":\"forecast\"}")]
    public async Task SolarForecastRequiresObjectEntries(string response)
    {
        using var server = new TestHomeAssistantServer { SolarForecastResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetSolarForecastAsync());
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

    [Theory]
    [InlineData("{\"weather.home\":{\"forecast\":[]},\"weather.garden\":{\"forecast\":[]}}")]
    [InlineData("{\"weather.home\":{\"forecast\":[]},\"weather.home\":{\"forecast\":[]}}")]
    public async Task WeatherForecastRequiresExactlyOneCanonicalResponseEntity(string response)
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
    [InlineData("{\"units\":{\"temperature_unit\":[\"°C\",\"\"]}}")]
    [InlineData("{\"units\":{\"temperature_unit\":[\"°C\",\"°C\"]}}")]
    [InlineData("{\"units\":{\"temperature_unit\":[\" °C \"]}}")]
    [InlineData("{\"units\":{\" \":[\"°C\"]}}")]
    [InlineData("{\"units\":{\"TEMPERATURE_UNIT\":[\"°C\"]}}")]
    [InlineData("{\"units\":{\"not a unit\":[\"°C\"]}}")]
    public async Task WeatherConvertibleUnitsRejectBlankAndDuplicateValues(string response)
    {
        using var server = new TestHomeAssistantServer { WeatherConvertibleUnitsResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetConvertibleUnitsAsync());
    }

    [Fact]
    public async Task WeatherBulkReadsRejectDuplicateEntities()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":{}},{\"entity_id\":\"weather.home\",\"state\":\"rainy\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
    }

    [Theory]
    [InlineData("[{}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"has_mean\":false}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"has_mean\":0,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\" \",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"sensor.Bad\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"not an id\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\" sensor.energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"source\":\" \",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"source\":\"beta\",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"alpha:energy\",\"source\":\"beta\",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"alpha:energy\",\"source\":\" Alpha \",\"has_mean\":false,\"has_sum\":true}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true,\"mean_type\":1}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"source\":\"recorder\",\"has_mean\":true,\"has_sum\":true,\"mean_type\":0}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"source\":\"recorder\",\"has_mean\":true,\"has_sum\":true,\"mean_type\":2}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":false}]")]
    [InlineData("[{\"statistic_id\":\"sensor.energy\",\"statistic_id\":\"sensor.other\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true}]")]
    public async Task RecorderMetadataRequiresIdentityAndCapabilityFields(string response)
    {
        using var server = new TestHomeAssistantServer { RecorderMetadataResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.ListStatisticsAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsMetadataAsync());
    }

    [Fact]
    public async Task RecorderMetadataPreservesCircularMeanWithoutLegacyArithmeticFlag()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson = "[{\"statistic_id\":\"sensor.direction\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":false,\"mean_type\":2}]"
        };
        using var client = TestClientFactory.Create(server);

        var metadata = Assert.Single(await client.Recorder.GetStatisticsMetadataAsync());
        Assert.False(metadata.HasMean);
        Assert.Equal(HomeAssistantStatisticMeanType.Circular, metadata.MeanType);
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
    [InlineData("mean")]
    [InlineData("min")]
    [InlineData("max")]
    [InlineData("state")]
    [InlineData("sum")]
    [InlineData("change")]
    public async Task RecorderStatisticsRejectNonFiniteNumericValues(string propertyName)
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":1,\"end\":2,\""
                + propertyName
                + "\":1e400}]}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.energy")));
    }

    [Theory]
    [InlineData("{\"sensor.other\":[{\"start\":1,\"end\":2}]}")]
    [InlineData("{\"sensor.energy\":[{\"start\":2,\"end\":2}]}")]
    [InlineData("{\"sensor.energy\":[{\"start\":3,\"end\":2}]}")]
    [InlineData("{\"sensor.energy\":[{\"start\":1,\"end\":2,\"last_reset\":3}]}")]
    [InlineData("{\"sensor.energy\":[{\"start\":1,\"start\":2,\"end\":3}]}")]
    public async Task RecorderStatisticsCorrelateIdentifiersAndRequirePositiveIntervals(string response)
    {
        using var server = new TestHomeAssistantServer { RecorderStatisticsResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.energy")));
    }

    [Theory]
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
    public async Task UnfilteredRecorderMetadataRejectsDuplicateStatisticIdentifiers()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson = "[{\"statistic_id\":\"sensor.grid_energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true},{\"statistic_id\":\"sensor.grid_energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsMetadataAsync());
    }

    [Theory]
    [InlineData("unit_of_measurement", " ")]
    [InlineData("unit_of_measurement", " kWh ")]
    [InlineData("statistics_unit_of_measurement", " ")]
    [InlineData("statistics_unit_of_measurement", " kWh ")]
    public async Task RecorderMetadataRejectsMalformedReturnedUnits(string propertyName, string unit)
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson = "[{\"statistic_id\":\"sensor.grid_energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true,\"" + propertyName + "\":\"" + unit + "\"}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsMetadataAsync());
    }

    [Fact]
    public async Task RecorderMetadataPreservesHomeAssistantEmptyUnitSentinels()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson =
                "[{\"statistic_id\":\"sensor.grid_energy\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":true,\"unit_of_measurement\":\"\",\"statistics_unit_of_measurement\":\"\"}]"
        };
        using var client = TestClientFactory.Create(server);

        var listed = Assert.Single(await client.Recorder.ListStatisticsAsync());
        Assert.Equal(string.Empty, listed.UnitOfMeasurement);
        Assert.Equal(string.Empty, listed.StatisticsUnitOfMeasurement);

        var metadata = Assert.Single(await client.Recorder.GetStatisticsMetadataAsync());
        Assert.Equal(string.Empty, metadata.UnitOfMeasurement);
        Assert.Equal(string.Empty, metadata.StatisticsUnitOfMeasurement);
    }

    [Fact]
    public async Task FilteredRecorderMetadataAllowsMissingRequestedRows()
    {
        using var server = new TestHomeAssistantServer { RecorderMetadataResponseJson = "[]" };
        using var client = TestClientFactory.Create(server);

        Assert.Empty(await client.Recorder.GetStatisticsMetadataAsync(new[] { "sensor.missing" }));
    }

    [Theory]
    [InlineData("sensor.Bad")]
    [InlineData("source:Bad")]
    [InlineData("not an id")]
    public async Task RecorderReadsRejectMalformedStatisticIdentifiersBeforeDispatch(string statisticId)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.GetStatisticsMetadataAsync(new[] { statisticId }));
        Assert.Throws<ArgumentException>(() => new HomeAssistantStatisticsQuery(
            DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, statisticId));
        Assert.Null(server.GetLastWebSocketCommand("recorder/get_statistics_metadata"));
        Assert.Null(server.GetLastWebSocketCommand("recorder/statistics_during_period"));
    }

    [Fact]
    public async Task RecorderStatisticsRevalidateExposedSelectorsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(
            DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.grid_energy");
        Assert.IsType<string[]>(query.StatisticIds)[0] = "sensor.Bad";

        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.GetStatisticsAsync(query));

        Assert.Null(server.GetLastWebSocketCommand("recorder/statistics_during_period"));
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

    [Fact]
    public async Task RecorderStatisticSelectorNormalizationStopsWhenCancellationArrives()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var readCancellation = new CancellationTokenSource();
        using var clearCancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Recorder.GetStatisticsMetadataAsync(
                new CancellingStatisticIds(readCancellation),
                readCancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Recorder.ClearStatisticsAsync(
                new CancellingStatisticIds(clearCancellation),
                clearCancellation.Token));

        Assert.Null(server.GetLastWebSocketCommand("recorder/get_statistics_metadata"));
        Assert.Null(server.GetLastWebSocketCommand("recorder/clear_statistics"));
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
    public async Task RecorderStatisticsNormalizeUnitValuesAndRequireStrictRowOrdering()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":2,\"end\":3},{\"start\":1,\"end\":2}]}"
        };
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(DateTimeOffset.UtcNow.AddHours(-1), HomeAssistantStatisticPeriod.Hour, "sensor.energy")
        {
            Units = new Dictionary<string, string> { ["energy"] = " kWh " }
        };

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(query));
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/statistics_during_period")));
        Assert.Equal("kWh", command.RootElement.GetProperty("units").GetProperty("energy").GetString());
    }

    [Fact]
    public async Task RecorderStatisticsRejectOverlappingIntervals()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":1,\"end\":3},{\"start\":2,\"end\":4}]}"
        };
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(
            DateTimeOffset.UtcNow.AddHours(-1),
            HomeAssistantStatisticPeriod.Hour,
            "sensor.energy");

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(query));
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

    [Theory]
    [InlineData("temperature")]
    [InlineData("wind_gust_speed")]
    [InlineData("wind_bearing")]
    public async Task WeatherForecastRejectsNonFiniteNumericValues(string propertyName)
    {
        using var server = new TestHomeAssistantServer
        {
            WeatherForecastResponseJson = "{\"weather.home\":{\"forecast\":[{\"datetime\":\"2026-08-28T10:00:00+00:00\",\"" + propertyName + "\":1e400}]}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Weather.GetForecastAsync("weather.home", HomeAssistantWeatherForecastType.Daily));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("false")]
    public async Task WeatherForecastRejectsUnsupportedWindBearingShapes(string value)
    {
        using var server = new TestHomeAssistantServer
        {
            WeatherForecastResponseJson = "{\"weather.home\":{\"forecast\":[{\"datetime\":\"2026-08-28T10:00:00+00:00\",\"wind_bearing\":" + value + "}]}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Weather.GetForecastAsync("weather.home", HomeAssistantWeatherForecastType.Daily));
    }

    [Fact]
    public void WeatherSortComparerObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var comparer = new HomeAssistantX.Protocol.CancellationAwareStringComparer(
            StringComparer.OrdinalIgnoreCase,
            cancellation.Token);

        Assert.ThrowsAny<OperationCanceledException>(() => comparer.Compare("weather.a", "weather.b"));
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
    [InlineData("2026-08-26T07:59:59+00:00")]
    [InlineData("2026-08-26T12:00:00+00:00")]
    public async Task FossilEnergyRejectsPeriodsOutsideTheRequestedWindow(string timestamp)
    {
        using var server = new TestHomeAssistantServer
        {
            FossilEnergyResponseJson = "{\"" + timestamp + "\":0.42}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero),
            new[] { "sensor.energy" },
            "sensor.co2",
            HomeAssistantEnergyPeriod.Hour));
    }

    [Theory]
    [InlineData(HomeAssistantEnergyPeriod.FiveMinute, "2026-10-01T00:00:00+02:00")]
    [InlineData(HomeAssistantEnergyPeriod.Day, "2026-10-24T23:00:00+00:00")]
    [InlineData(HomeAssistantEnergyPeriod.Month, "2026-10-01T00:00:00+02:00")]
    public async Task FossilEnergyAcceptsHomeAssistantCalendarBucketStarts(
        HomeAssistantEnergyPeriod period,
        string timestamp)
    {
        using var server = new TestHomeAssistantServer
        {
            FossilEnergyResponseJson = "{\"" + timestamp + "\":0.42}"
        };
        using var client = TestClientFactory.Create(server);

        var result = await client.Energy.GetFossilEnergyConsumptionAsync(
            new DateTimeOffset(2026, 10, 26, 1, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 26, 3, 30, 0, TimeSpan.Zero),
            new[] { "sensor.energy" },
            "sensor.co2",
            period);

        Assert.Single(result);
    }

    [Theory]
    [InlineData(HomeAssistantEnergyPeriod.FiveMinute, "2026-09-22T00:00:00+00:00")]
    [InlineData(HomeAssistantEnergyPeriod.Day, "2026-10-24T21:00:00+00:00")]
    [InlineData(HomeAssistantEnergyPeriod.Month, "2026-09-22T00:00:00+00:00")]
    public async Task FossilEnergyRejectsCalendarBucketsBeyondTheCorrelationMargin(
        HomeAssistantEnergyPeriod period,
        string timestamp)
    {
        using var server = new TestHomeAssistantServer
        {
            FossilEnergyResponseJson = "{\"" + timestamp + "\":0.42}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            new DateTimeOffset(2026, 10, 26, 1, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 26, 3, 30, 0, TimeSpan.Zero),
            new[] { "sensor.energy" },
            "sensor.co2",
            period));
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
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Recorder.AdjustSumStatisticsAsync(
            "sensor.energy", now, 0, "kWh"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Recorder.AdjustSumStatisticsAsync(
            "sensor.energy", now, 1, " "));
        Assert.Null(server.GetLastWebSocketCommand("energy/fossil_energy_consumption"));
        Assert.Null(server.GetLastWebSocketCommand("recorder/adjust_sum_statistics"));
    }

    [Theory]
    [InlineData("sensor.Energy", "sensor.co2")]
    [InlineData("external-source", "sensor.co2")]
    [InlineData("sensor.energy", "source:Carbon")]
    public async Task FossilEnergyRejectsNoncanonicalStatisticIdentifiersBeforeDispatch(string energyStatisticId, string co2StatisticId)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            new[] { energyStatisticId },
            co2StatisticId,
            HomeAssistantEnergyPeriod.Hour));

        Assert.Null(server.GetLastWebSocketCommand("energy/fossil_energy_consumption"));
    }

    [Fact]
    public async Task FossilEnergyRejectsDuplicateStatisticIdentifiersBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            DateTimeOffset.UtcNow.AddHours(-1),
            DateTimeOffset.UtcNow,
            new[] { "sensor.energy", " sensor.energy " },
            "sensor.co2",
            HomeAssistantEnergyPeriod.Hour));

        Assert.Null(server.GetLastWebSocketCommand("energy/fossil_energy_consumption"));
    }

    [Fact]
    public async Task FossilEnergyNormalizesValidStatisticIdentifiersBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Energy.GetFossilEnergyConsumptionAsync(
            new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero),
            new[] { " sensor.energy ", " source:grid_energy " },
            " sensor.co2 ",
            HomeAssistantEnergyPeriod.Hour);

        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("energy/fossil_energy_consumption")));
        Assert.Equal(new[] { "sensor.energy", "source:grid_energy" }, command.RootElement.GetProperty("energy_statistic_ids").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal("sensor.co2", command.RootElement.GetProperty("co2_statistic_id").GetString());
    }

    private sealed class NullKeyUnits : IReadOnlyDictionary<string, string>
    {
        public int Count => 1;
        public IEnumerable<string> Keys => new string[] { null! };
        public IEnumerable<string> Values => new[] { "kWh" };
        public string this[string key] => throw new KeyNotFoundException();
        public bool ContainsKey(string key) => false;
        public bool TryGetValue(string key, out string value)
        {
            value = string.Empty;
            return false;
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
            => new[] { new KeyValuePair<string, string>(null!, "kWh") }.AsEnumerable().GetEnumerator();

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CancellingStatisticIds : IReadOnlyCollection<string>
    {
        private readonly CancellationTokenSource _cancellation;

        internal CancellingStatisticIds(CancellationTokenSource cancellation) => _cancellation = cancellation;

        public int Count => 2;

        public IEnumerator<string> GetEnumerator()
        {
            yield return "sensor.grid_energy";
            _cancellation.Cancel();
            yield return "sensor.solar_energy";
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CancellingStatisticRows : IReadOnlyCollection<HomeAssistantStatisticImportRow>
    {
        private readonly CancellationTokenSource _cancellation;

        internal CancellingStatisticRows(CancellationTokenSource cancellation) => _cancellation = cancellation;

        public int Count => 2;

        public IEnumerator<HomeAssistantStatisticImportRow> GetEnumerator()
        {
            yield return new HomeAssistantStatisticImportRow
            {
                Start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero),
                Sum = 1.5
            };
            _cancellation.Cancel();
            yield return new HomeAssistantStatisticImportRow
            {
                Start = new DateTimeOffset(2026, 8, 26, 11, 0, 0, TimeSpan.Zero),
                Sum = 2.5
            };
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CancelAtEndStatisticRows : IReadOnlyCollection<HomeAssistantStatisticImportRow>
    {
        private readonly CancellationTokenSource _cancellation;

        internal CancelAtEndStatisticRows(CancellationTokenSource cancellation) => _cancellation = cancellation;

        public int Count => 1;

        public IEnumerator<HomeAssistantStatisticImportRow> GetEnumerator()
        {
            yield return CreateSumRow(10, 1.5);
            _cancellation.Cancel();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class SingleEnumerationStatisticRows : IReadOnlyCollection<HomeAssistantStatisticImportRow>
    {
        internal int EnumerationCount { get; private set; }

        public int Count => 2;

        public IEnumerator<HomeAssistantStatisticImportRow> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount != 1)
                throw new InvalidOperationException("The caller-owned row collection was enumerated more than once.");
            yield return CreateSumRow(10, 1.5);
            yield return CreateSumRow(11, 2.5);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static HomeAssistantStatisticImportMetadata CreateSumImportMetadata() => new()
    {
        StatisticId = "external:daily_energy",
        Source = "external",
        HasSum = true,
        MeanType = HomeAssistantStatisticMeanType.None,
        UnitClass = "energy",
        UnitOfMeasurement = "kWh"
    };

    private static HomeAssistantStatisticImportRow CreateSumRow(int hour, double sum) => new()
    {
        Start = new DateTimeOffset(2026, 8, 26, hour, 0, 0, TimeSpan.Zero),
        Sum = sum
    };

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
