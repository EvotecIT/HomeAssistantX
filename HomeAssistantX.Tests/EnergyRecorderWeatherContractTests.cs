#if NET10_0
using System.Globalization;
using System.Text.Json;
using HomeAssistantX.Authentication;
using HomeAssistantX.Energy;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Recorder;
using HomeAssistantX.Tests.Infrastructure;
using HomeAssistantX.Weather;

namespace HomeAssistantX.Tests;

public sealed class EnergyRecorderWeatherContractTests
{
    [Fact]
    public void FossilEnergySortingPreservesCancellationFromTheComparison()
    {
        var periods = new List<HomeAssistantFossilEnergyPeriod>
        {
            new() { Start = DateTimeOffset.UtcNow.AddHours(1) },
            new() { Start = DateTimeOffset.UtcNow }
        };

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantEnergyClient.SortFossilEnergyPeriods(
                periods,
                (_, _) => throw new OperationCanceledException()));
    }

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
    [InlineData("energy", "[{}]")]
    [InlineData("energy", "[{\"type\":\" \"}]")]
    [InlineData("energy", "[{\"type\":\" solar \"}]")]
    [InlineData("consumption", "[{}]")]
    [InlineData("consumption", "[{\"stat_consumption\":\" sensor.ev_energy \"}]")]
    [InlineData("consumption", "[{\"stat_consumption\":true}]")]
    public async Task EnergyPreferenceUpdateRequiresCanonicalEntryIdentitiesBeforeDispatch(
        string collection,
        string json)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var document = JsonDocument.Parse(json);
        var update = new HomeAssistantEnergyPreferencesUpdate();
        if (collection == "energy") update.EnergySources = document.RootElement.Clone();
        else update.DeviceConsumption = document.RootElement.Clone();

        await Assert.ThrowsAsync<ArgumentException>(() => client.Energy.SavePreferencesAsync(update));

        Assert.Null(server.GetLastWebSocketCommand("energy/save_prefs"));
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

    [Theory]
    [InlineData("{\"energy_sources\":[{}],\"device_consumption\":[]}")]
    [InlineData("{\"energy_sources\":[],\"device_consumption\":[{\"stat_consumption\":\" sensor.ev_energy \"}]}")]
    public async Task EnergyPreferenceResponsesRequireCanonicalEntryIdentities(string response)
    {
        using var server = new TestHomeAssistantServer { EnergyPreferencesResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetPreferencesAsync());
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
    [InlineData("{\"cost_sensors\":{},\"cost_sensors\":{},\"solar_forecast_domains\":[]}")]
    [InlineData("{\"cost_sensors\":{},\"solar_forecast_domains\":[],\"solar_forecast_domains\":[]}")]
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
    public async Task RecorderStatisticsRequireEveryExplicitlyRequestedValue()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson =
                "{\"sensor.grid_energy\":[{\"start\":1787731200000,\"end\":1787734800000,\"mean\":3.5}]}"
        };
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(
            new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero),
            HomeAssistantStatisticPeriod.Hour,
            "sensor.grid_energy")
        {
            EndTime = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            Types = new[] { HomeAssistantStatisticType.Sum }
        };

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Recorder.GetStatisticsAsync(query));
    }

    [Fact]
    public async Task RecorderStatisticsTreatRequestedNullFieldsAsPresent()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson =
                "{\"sensor.grid_energy\":[{\"start\":1787731200000,\"end\":1787734800000,\"change\":null,\"last_reset\":null,\"max\":null,\"mean\":null,\"min\":null,\"state\":null,\"sum\":null}]}"
        };
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(
            new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero),
            HomeAssistantStatisticPeriod.Hour,
            "sensor.grid_energy")
        {
            EndTime = new DateTimeOffset(2026, 8, 27, 0, 0, 0, TimeSpan.Zero),
            Types = Enum.GetValues<HomeAssistantStatisticType>()
        };

        var row = Assert.Single(Assert.Single(await client.Recorder.GetStatisticsAsync(query)).Rows);

        Assert.Null(row.Change);
        Assert.Null(row.LastReset);
        Assert.Null(row.Maximum);
        Assert.Null(row.Mean);
        Assert.Null(row.Minimum);
        Assert.Null(row.State);
        Assert.Null(row.Sum);
    }

    [Fact]
    public void EnergyEntryIdentityValidationHonorsCancellation()
    {
        using var document = JsonDocument.Parse(
            "{\"stat_consumption\":\"sensor." + new string('x', 1_000_000) + "\"}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantEnergyPreferencesUpdate.HasRequiredIdentity(
                document.RootElement,
                "device_consumption",
                cancellation.Token));
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

        await client.Recorder.PurgeEntitiesAsync(entityGlobs: new[] { " sensor.* ", " ", "", "*", "sensor*", "binary_sensor.door_?", "sensor.room_[0-9]", "sensor.*" });
        using (var purge = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody)))
        {
            Assert.Equal(
                new[] { " sensor.* ", " ", "", "*", "sensor*", "binary_sensor.door_?", "sensor.room_[0-9]", "sensor.*" },
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
    public async Task RecorderImportSnapshotsMutableRowsByValueDuringEnumeration()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Recorder.ImportStatisticsAsync(
            CreateSumImportMetadata(),
            new ReusedMutableStatisticRows());

        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/import_statistics")));
        var rows = command.RootElement.GetProperty("stats");
        Assert.Equal("2026-08-26T10:00:00.0000000+00:00", rows[0].GetProperty("start").GetString());
        Assert.Equal(1.5, rows[0].GetProperty("sum").GetDouble());
        Assert.Equal("2026-08-26T11:00:00.0000000+00:00", rows[1].GetProperty("start").GetString());
        Assert.Equal(2.5, rows[1].GetProperty("sum").GetDouble());
    }

    [Fact]
    public async Task RecorderImportSnapshotsMutableMetadataBeforeEnumeratingRows()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var metadata = CreateSumImportMetadata();

        await client.Recorder.ImportStatisticsAsync(
            metadata,
            new MetadataMutatingStatisticRows(metadata));

        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/import_statistics")));
        var serialized = command.RootElement.GetProperty("metadata");
        Assert.Equal("external:daily_energy", serialized.GetProperty("statistic_id").GetString());
        Assert.Equal("external", serialized.GetProperty("source").GetString());
        Assert.True(serialized.GetProperty("has_sum").GetBoolean());
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
    public void RecorderImportMetadataNormalizationHonorsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var metadata = CreateSumImportMetadata();
        metadata.StatisticId = "external:" + new string('a', 1_000_000);

        Assert.ThrowsAny<OperationCanceledException>(() => metadata.ValidateRows(
            new[] { new HomeAssistantStatisticImportRow { Start = DateTimeOffset.UtcNow.Date, Sum = 1 } },
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
            "{\"entity_id\":\"weather.bad\",\"state\":\"unknown\",\"attributes\":{\"supported_features\":0}}]");
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

    [Theory]
    [InlineData("[]")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("\"invalid\"")]
    public async Task WeatherReadsRejectNonObjectStateAttributes(string attributesJson)
    {
        var state = "{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":" + attributesJson + "}";
        using var server = new TestHomeAssistantServer { ExactStateResponseJson = state };
        server.SetStates("[" + state + "]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync("weather.home"));
    }

    [Theory]
    [InlineData("\"humidity\":-1")]
    [InlineData("\"humidity\":101")]
    [InlineData("\"cloud_coverage\":101")]
    [InlineData("\"wind_bearing\":true")]
    [InlineData("\"wind_bearing\":{}")]
    [InlineData("\"wind_bearing\":1e400")]
    [InlineData("\"wind_bearing\":-1")]
    [InlineData("\"wind_bearing\":361")]
    [InlineData("\"wind_bearing\":\" north \"")]
    public async Task WeatherCurrentReadsRejectImpossiblePercentagesAndWindBearingShapes(string attribute)
    {
        var state = "{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":{" + attribute + "}}";
        using var server = new TestHomeAssistantServer { ExactStateResponseJson = state };
        server.SetStates("[" + state + "]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync("weather.home"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" sunny ")]
    public async Task WeatherCurrentReadsRejectNoncanonicalConditions(string condition)
    {
        var state = "{\"entity_id\":\"weather.home\",\"state\":\"" + condition + "\",\"attributes\":{}}";
        using var server = new TestHomeAssistantServer { ExactStateResponseJson = state };
        server.SetStates("[" + state + "]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync("weather.home"));
    }

    [Theory]
    [InlineData("\"temperature\":\"21.5\"")]
    [InlineData("\"humidity\":\"55\"")]
    [InlineData("\"cloud_coverage\":\"40\"")]
    public async Task WeatherCurrentReadsRejectStringShapedNumericAttributes(string attribute)
    {
        var state = "{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":{" + attribute + "}}";
        using var server = new TestHomeAssistantServer { ExactStateResponseJson = state };
        server.SetStates("[" + state + "]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync("weather.home"));
    }

    [Theory]
    [InlineData("\"3\"")]
    [InlineData("true")]
    [InlineData("1.5")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    public async Task WeatherCurrentReadsRejectMalformedSupportedFeatures(string value)
    {
        var state = "{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":{" +
            "\"supported_features\":" + value + "}}";
        using var server = new TestHomeAssistantServer { ExactStateResponseJson = state };
        server.SetStates("[" + state + "]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync("weather.home"));
    }

    [Fact]
    public void WeatherCurrentProjectionHonorsCancellationAcrossProviderText()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var state = new HomeAssistantState
        {
            EntityId = "weather.home",
            State = new string('x', 1_000_000),
            Attributes = new Dictionary<string, JsonElement>
            {
                ["friendly_name"] = JsonDocument.Parse("\"" + new string('x', 1_000_000) + "\"").RootElement.Clone()
            }
        };

        Assert.ThrowsAny<OperationCanceledException>(() =>
        {
            _ = HomeAssistantWeatherClient.ToObservation(state, cancellation.Token);
        });
    }

    [Theory]
    [InlineData("true")]
    [InlineData("42")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"   \"")]
    public async Task WeatherCurrentReadsRejectMalformedDisplayNames(string friendlyName)
    {
        var state = "{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":{"
            + "\"friendly_name\":" + friendlyName + "}}";
        using var server = new TestHomeAssistantServer { ExactStateResponseJson = state };
        server.SetStates("[" + state + "]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync("weather.home"));
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
    public async Task WeatherForecastAcceptsUnavailableConditions()
    {
        using var server = new TestHomeAssistantServer
        {
            WeatherForecastResponseJson =
                "{\"weather.home\":{\"forecast\":[{\"datetime\":\"2026-08-28T10:00:00+00:00\",\"condition\":null}]}}"
        };
        using var client = TestClientFactory.Create(server);

        var update = await client.Weather.GetForecastAsync(
            "weather.home",
            HomeAssistantWeatherForecastType.Daily);

        Assert.Null(Assert.Single(update.Forecast).Condition);
    }

    [Theory]
    [InlineData("{\"datetime\":\"2026-08-26T10:00:00Z\",\"temperature\":20,\"temperature\":21}")]
    [InlineData("{\"datetime\":\"2026-08-26T10:00:00Z\",\"humidity\":-1}")]
    [InlineData("{\"datetime\":\"2026-08-26T10:00:00Z\",\"cloud_coverage\":101}")]
    [InlineData("{\"datetime\":\"2026-08-26T10:00:00Z\",\"precipitation_probability\":101}")]
    [InlineData("{\"datetime\":\"2026-08-26T10:00:00Z\",\"condition\":\" rainy \"}")]
    [InlineData("{\"datetime\":\"2026-08-26T10:00:00Z\",\"condition\":\" \"}")]
    public async Task WeatherForecastRejectsAmbiguousOrImpossiblePeriods(string period)
    {
        using var server = new TestHomeAssistantServer
        {
            WeatherForecastResponseJson = "{\"weather.home\":{\"forecast\":[" + period + "]}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Weather.GetForecastAsync("weather.home", HomeAssistantWeatherForecastType.Hourly));
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

    [Theory]
    [InlineData("{\"type\":\"hourly\",\"type\":\"hourly\",\"forecast\":[]}")]
    [InlineData("{\"type\":\"hourly\",\"forecast\":[],\"forecast\":[]}")]
    public async Task WeatherSubscriptionRejectsDuplicateWrapperProperties(string eventJson)
    {
        using var server = new TestHomeAssistantServer { WeatherForecastSubscriptionEventJson = eventJson };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Weather.SubscribeForecastAsync(
            "weather.home",
            HomeAssistantWeatherForecastType.Hourly,
            (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
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

    [Fact]
    public async Task SolarForecastRejectsNestedDuplicateFieldsAndReturnsAStableDictionary()
    {
        using var server = new TestHomeAssistantServer
        {
            SolarForecastResponseJson = "{\"entry-a\":{\"watts\":1,\"watts\":2}}"
        };
        using var client = TestClientFactory.Create(server);
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetSolarForecastAsync());

        server.SolarForecastResponseJson = "{\"entry-a\":{\"watts\":1}}";
        using var cancellation = new CancellationTokenSource();
        var forecast = await client.Energy.GetSolarForecastAsync(cancellation.Token);
        cancellation.Cancel();
        Assert.True(forecast.ContainsKey("ENTRY-A"));
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
    [InlineData("{\"weather.home\":{\"forecast\":[],\"forecast\":[]}}")]
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
    public async Task WeatherConvertibleUnitsReturnAStableDictionaryAfterCancellation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();

        var units = await client.Weather.GetConvertibleUnitsAsync(cancellation.Token);
        cancellation.Cancel();

        Assert.True(units.ContainsKey("TEMPERATURE_UNIT"));
        Assert.Contains("°C", units["temperature_unit"]);
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

    [Fact]
    public async Task RecorderMetadataPreservesFutureMeanTypes()
    {
        using var server = new TestHomeAssistantServer
        {
            RecorderMetadataResponseJson = "[{\"statistic_id\":\"sensor.future\",\"source\":\"recorder\",\"has_mean\":false,\"has_sum\":false,\"mean_type\":99}]"
        };
        using var client = TestClientFactory.Create(server);

        var metadata = Assert.Single(await client.Recorder.GetStatisticsMetadataAsync());
        Assert.Equal((HomeAssistantStatisticMeanType)99, metadata.MeanType);
        var listed = Assert.Single(await client.Recorder.ListStatisticsAsync(HomeAssistantStatisticKind.Mean));
        Assert.Equal((HomeAssistantStatisticMeanType)99, listed.MeanType);
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
    public void RecorderStatisticsQueryDeduplicatesManyIdentifiersWithoutChangingOrder()
    {
        var identifiers = Enumerable.Range(0, 10_000)
            .SelectMany(index => new[] { $"sensor.value_{index}", $"sensor.value_{index}" })
            .ToArray();

        var query = new HomeAssistantStatisticsQuery(
            DateTimeOffset.UtcNow.AddHours(-1),
            HomeAssistantStatisticPeriod.Hour,
            identifiers);

        Assert.Equal(10_000, query.StatisticIds.Count);
        Assert.Equal("sensor.value_0", query.StatisticIds[0]);
        Assert.Equal("sensor.value_9999", query.StatisticIds[9_999]);
    }

    [Fact]
    public void HomeTimeZoneResolutionPrioritizesAPreCanceledToken()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            HomeAssistantCalendarTime.RequireTimeZone(
                new string('A', 1_000_000),
                "calendar statistics",
                cancellation.Token));
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
            DateTimeOffset.FromUnixTimeSeconds(1787731200),
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
    public async Task RecorderStatisticsEndTimeIsFrozenBeforeTokenResolutionAndResponseValidation()
    {
        using var server = new TestHomeAssistantServer();
        var tokenProvider = new BlockingTokenProvider();
        using var client = TestClientFactory.Create(server, accessTokenProvider: tokenProvider);
        var start = DateTimeOffset.FromUnixTimeSeconds(1787731200);
        var originalEnd = start.AddHours(2);
        var query = new HomeAssistantStatisticsQuery(
            start,
            HomeAssistantStatisticPeriod.Hour,
            "sensor.grid_energy")
        {
            EndTime = originalEnd
        };

        var request = client.Recorder.GetStatisticsAsync(query);
        await tokenProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        query.EndTime = start.AddHours(-1);
        tokenProvider.Release.TrySetResult(TestHomeAssistantServer.AccessToken);

        Assert.Single(await request);
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("recorder/statistics_during_period")));
        Assert.Equal(originalEnd.ToString("O", CultureInfo.InvariantCulture), command.RootElement.GetProperty("end_time").GetString());
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
    public async Task RecorderUnitConversionUsesTheNativeOldUnitField()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Recorder.ChangeStatisticsUnitAsync("sensor.grid_energy", "Wh", "kWh");

        using var command = JsonDocument.Parse(Assert.IsType<string>(
            server.GetLastWebSocketCommand("recorder/change_statistics_unit")));
        Assert.Equal("Wh", command.RootElement.GetProperty("old_unit_of_measurement").GetString());
        Assert.Equal("kWh", command.RootElement.GetProperty("new_unit_of_measurement").GetString());
        Assert.False(command.RootElement.TryGetProperty("statistic_unit_of_measurement", out _));
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
    public async Task RecorderStatisticsRejectRowsOutsideTheRequestedWindow()
    {
        var start = new DateTimeOffset(2026, 8, 26, 10, 30, 0, TimeSpan.Zero);
        var end = start.AddHours(2);
        foreach (var row in new[]
        {
            new { Start = start.AddHours(-2), End = start.AddHours(-1) },
            new { Start = end, End = end.AddHours(1) }
        })
        {
            using var server = new TestHomeAssistantServer
            {
                RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":"
                    + row.Start.ToUnixTimeMilliseconds() + ",\"end\":"
                    + row.End.ToUnixTimeMilliseconds() + "}]}"
            };
            using var client = TestClientFactory.Create(server);
            var query = new HomeAssistantStatisticsQuery(start, HomeAssistantStatisticPeriod.Hour, "sensor.energy")
            {
                EndTime = end
            };

            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(query));
        }
    }

    [Fact]
    public async Task RecorderStatisticsPreservesSubMillisecondExclusiveEndTime()
    {
        var start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);
        var rowStart = start.AddHours(1);
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":"
                + rowStart.ToUnixTimeMilliseconds() + ",\"end\":"
                + rowStart.AddHours(1).ToUnixTimeMilliseconds() + "}]}"
        };
        using var client = TestClientFactory.Create(server);
        var query = new HomeAssistantStatisticsQuery(start, HomeAssistantStatisticPeriod.Hour, "sensor.energy")
        {
            EndTime = rowStart.AddTicks(5)
        };

        var result = await client.Recorder.GetStatisticsAsync(query);

        Assert.Single(Assert.Single(result).Rows);
    }

    [Fact]
    public async Task FossilEnergyRejectsUndefinedPeriodBeforeConfigurationIo()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var start = new DateTimeOffset(2026, 8, 26, 10, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            start,
            start.AddHours(1),
            new[] { "sensor.energy" },
            "sensor.co2",
            (HomeAssistantEnergyPeriod)99));

        Assert.Equal(0, server.AuthenticatedRequestCount);
        Assert.Null(server.GetLastWebSocketCommand("energy/fossil_energy_consumption"));
    }

    [Fact]
    public void UnavailableWeatherForecastObservesCancellationBeforeProjection()
    {
        using var document = JsonDocument.Parse("{\"forecast\":null,\"provider_payload\":[" + string.Join(",", Enumerable.Repeat("0", 10000)) + "]}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => HomeAssistantWeatherClient.ParseUpdate(
            "weather.home",
            HomeAssistantWeatherForecastType.Hourly,
            document.RootElement.GetProperty("forecast"),
            document.RootElement,
            cancellation.Token));
    }

    [Fact]
    public void RecorderStatisticsSortingPreservesCancellationExceptions()
    {
        var series = new List<HomeAssistantStatisticSeries>
        {
            new() { StatisticId = "sensor.second" },
            new() { StatisticId = "sensor.first" }
        };

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRecorderClient.SortSeries(series, new CancelingStringComparer()));
    }

    [Fact]
    public async Task RecorderPurgeSelectorNormalizationObservesCancellation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Recorder.PurgeEntitiesAsync(
                entityIds: new CancellingStatisticIds(cancellation),
                cancellationToken: cancellation.Token));
        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public void SharedRecorderPurgeNormalizationObservesCancellationDuringPreflight()
    {
        using var cancellation = new CancellationTokenSource();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRecorderClient.NormalizePurgeEntityIds(
                new CancellingStatisticIds(cancellation),
                "EntityId",
                cancellation.Token));
    }

    [Fact]
    public void SharedStatisticIdentifierNormalizationIsDuplicateSafeAndCancellable()
    {
        Assert.Equal(
            new[] { "sensor.energy", "external:energy" },
            HomeAssistantRecorderClient.NormalizeStatisticIds(
                new[] { "sensor.energy", "sensor.energy", "external:energy" },
                "StatisticId",
                default));

        using var cancellation = new CancellationTokenSource();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRecorderClient.NormalizeStatisticIds(
                new CancellingStatisticIds(cancellation),
                "StatisticId",
                cancellation.Token));
    }

    [Fact]
    public void RecorderEntityGlobNormalizationIsOrdinalDuplicateSafeAndCancellable()
    {
        Assert.Equal(
            new[] { "sensor.*", "Sensor.*" },
            HomeAssistantRecorderClient.NormalizePurgeEntityGlobs(
                new[] { "sensor.*", "sensor.*", "Sensor.*" },
                "EntityGlob",
                default));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRecorderClient.NormalizePurgeEntityGlobs(
                new[] { new string('x', 1_000_000) },
                "EntityGlob",
                cancellation.Token));
    }

    [Theory]
    [InlineData("temperature_unit")]
    [InlineData("pressure_unit")]
    [InlineData("visibility_unit")]
    [InlineData("wind_speed_unit")]
    [InlineData("precipitation_unit")]
    public async Task CurrentWeatherRejectsMalformedUnitAttributes(string attributeName)
    {
        foreach (var value in new[] { "true", "{}", "[]", "\" \"", "\" °C \"" })
        {
            using var server = new TestHomeAssistantServer();
            server.SetStates(
                "[{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":{\""
                + attributeName + "\":" + value + "}}]");
            using var client = TestClientFactory.Create(server);

            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
        }
    }

    [Theory]
    [InlineData("temperature")]
    [InlineData("apparent_temperature")]
    [InlineData("dew_point")]
    [InlineData("pressure")]
    [InlineData("uv_index")]
    [InlineData("visibility")]
    [InlineData("wind_speed")]
    [InlineData("wind_gust_speed")]
    public async Task CurrentWeatherRejectsMalformedNumericAttributes(string attributeName)
    {
        foreach (var value in new[] { "true", "{}", "[]", "1e400" })
        {
            using var server = new TestHomeAssistantServer();
            server.SetStates(
                "[{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":{\""
                + attributeName + "\":" + value + "}}]");
            using var client = TestClientFactory.Create(server);

            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
        }
    }

    [Fact]
    public async Task CurrentWeatherRejectsDuplicateAttributeProperties()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates(
            "[{\"entity_id\":\"weather.home\",\"state\":\"sunny\",\"attributes\":{"
            + "\"temperature\":20,\"temperature\":21}}]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Weather.GetAsync());
    }

    [Fact]
    public async Task RecorderStatisticsRejectRowsThatDoNotMatchTheRequestedBucket()
    {
        var start = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        var rowStart = start.ToUnixTimeMilliseconds();
        var rowEnd = start.AddMinutes(2).ToUnixTimeMilliseconds();
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":"
                + rowStart + ",\"end\":" + rowEnd + "}]}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(start, HomeAssistantStatisticPeriod.Hour, "sensor.energy")));
    }

    [Fact]
    public async Task RecorderFixedStatisticsUseUtcBucketOriginsForFractionalOffsets()
    {
        var queryStart = new DateTimeOffset(2026, 8, 26, 10, 30, 0, TimeSpan.FromHours(5.5));
        var rowStart = new DateTimeOffset(2026, 8, 26, 5, 0, 0, TimeSpan.Zero);
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":"
                + rowStart.ToUnixTimeMilliseconds() + ",\"end\":"
                + rowStart.AddHours(1).ToUnixTimeMilliseconds() + "}]}"
        };
        using var client = TestClientFactory.Create(server);

        Assert.Single(await client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(queryStart, HomeAssistantStatisticPeriod.Hour, "sensor.energy")));
    }

    [Fact]
    public async Task RecorderDailyStatisticsRejectNoonToNoonIntervals()
    {
        var start = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        var rowStart = start.AddHours(12).ToUnixTimeMilliseconds();
        var rowEnd = start.AddDays(1).AddHours(12).ToUnixTimeMilliseconds();
        using var server = new TestHomeAssistantServer
        {
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":"
                + rowStart + ",\"end\":" + rowEnd + "}]}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(start, HomeAssistantStatisticPeriod.Day, "sensor.energy")));
    }

    [Fact]
    public async Task RecorderCalendarStatisticsUseTheHomeAssistantTimeZone()
    {
        var queryStart = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        var rowStart = new DateTimeOffset(2026, 8, 26, 15, 0, 0, TimeSpan.Zero);
        var rowEnd = rowStart.AddDays(1);
        using var server = new TestHomeAssistantServer
        {
            ConfigurationResponseJson = "{\"time_zone\":\"Asia/Tokyo\",\"components\":[]}",
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":"
                + rowStart.ToUnixTimeMilliseconds() + ",\"end\":"
                + rowEnd.ToUnixTimeMilliseconds() + "}]}"
        };
        using var client = TestClientFactory.Create(server);

        Assert.Single(await client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(queryStart, HomeAssistantStatisticPeriod.Day, "sensor.energy")));
    }

    [Fact]
    public async Task RecorderMonthlyStatisticsResolveTheBucketOffsetAtTheBoundary()
    {
        var queryStart = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.FromHours(-5));
        var rowStart = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(-4));
        var rowEnd = new DateTimeOffset(2026, 12, 1, 0, 0, 0, TimeSpan.FromHours(-5));
        using var server = new TestHomeAssistantServer
        {
            ConfigurationResponseJson = "{\"time_zone\":\"America/New_York\",\"components\":[]}",
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":"
                + rowStart.ToUnixTimeMilliseconds() + ",\"end\":"
                + rowEnd.ToUnixTimeMilliseconds() + "}]}"
        };
        using var client = TestClientFactory.Create(server);

        Assert.Single(await client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(queryStart, HomeAssistantStatisticPeriod.Month, "sensor.energy")));
    }

    [Theory]
    [InlineData("America/Havana", "2026-03-08T05:00:00Z", "2026-03-09T04:00:00Z")]
    [InlineData("Antarctica/Troll", "2026-03-29T00:00:00Z", "2026-03-29T22:00:00Z")]
    [InlineData("Antarctica/Troll", "2026-10-24T22:00:00Z", "2026-10-26T00:00:00Z")]
    public async Task RecorderDailyStatisticsAcceptHomeAssistantMidnightTransitionBuckets(
        string timeZone,
        string rowStartText,
        string rowEndText)
    {
        var rowStart = DateTimeOffset.Parse(rowStartText, CultureInfo.InvariantCulture);
        var rowEnd = DateTimeOffset.Parse(rowEndText, CultureInfo.InvariantCulture);
        using var server = new TestHomeAssistantServer
        {
            ConfigurationResponseJson = "{\"time_zone\":\"" + timeZone + "\",\"components\":[]}",
            RecorderStatisticsResponseJson = "{\"sensor.energy\":[{\"start\":"
                + rowStart.ToUnixTimeMilliseconds() + ",\"end\":"
                + rowEnd.ToUnixTimeMilliseconds() + "}]}"
        };
        using var client = TestClientFactory.Create(server);

        Assert.Single(await client.Recorder.GetStatisticsAsync(
            new HomeAssistantStatisticsQuery(rowStart, HomeAssistantStatisticPeriod.Day, "sensor.energy")));
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
    [InlineData("-1")]
    [InlineData("361")]
    [InlineData("\" north \"")]
    [InlineData("\" \"")]
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
            StringComparison.OrdinalIgnoreCase,
            cancellation.Token);

        Assert.ThrowsAny<OperationCanceledException>(() => comparer.Compare("weather.a", "weather.b"));

        var observations = new List<HomeAssistantWeatherObservation>
        {
            new() { EntityId = "weather.b" },
            new() { EntityId = "weather.a" }
        };
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantWeatherClient.SortObservations(observations, cancellation.Token));
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

    [Fact]
    public async Task FossilEnergyRejectsNonHourlyKeysForHourlyRequests()
    {
        using var server = new TestHomeAssistantServer
        {
            FossilEnergyResponseJson = "{\"2026-08-26T10:17:00Z\":0.42}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero),
            new[] { "sensor.energy" },
            "sensor.co2",
            HomeAssistantEnergyPeriod.Hour));
    }

    [Fact]
    public async Task FossilEnergyRejectsThePrecedingBucketAtAnExactHourlyBoundary()
    {
        using var server = new TestHomeAssistantServer
        {
            FossilEnergyResponseJson = "{\"2026-08-26T08:00:00Z\":0.42}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Energy.GetFossilEnergyConsumptionAsync(
                new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero),
                new[] { "sensor.energy" },
                "sensor.co2",
                HomeAssistantEnergyPeriod.Hour));
    }

    [Theory]
    [InlineData(HomeAssistantEnergyPeriod.FiveMinute, "2026-10-01T00:00:00+02:00")]
    [InlineData(HomeAssistantEnergyPeriod.Day, "2026-10-25T23:00:00+00:00")]
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

    [Fact]
    public async Task FossilEnergyAcceptsAHistoricFortySevenHourLocalDayBucket()
    {
        using var server = new TestHomeAssistantServer
        {
            ConfigurationResponseJson = "{\"time_zone\":\"Pacific/Kwajalein\",\"components\":[]}",
            FossilEnergyResponseJson = "{\"1969-09-29T13:00:00Z\":0.42}"
        };
        using var client = TestClientFactory.Create(server);

        var result = await client.Energy.GetFossilEnergyConsumptionAsync(
            new DateTimeOffset(1969, 10, 1, 11, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(1969, 10, 1, 13, 0, 0, TimeSpan.Zero),
            new[] { "sensor.energy" },
            "sensor.co2",
            HomeAssistantEnergyPeriod.Day);

        Assert.Single(result);
    }

    [Theory]
    [InlineData(HomeAssistantEnergyPeriod.FiveMinute, "2026-10-01T00:00:00.0000001+02:00")]
    [InlineData(HomeAssistantEnergyPeriod.FiveMinute, "2026-10-02T00:00:00+02:00")]
    [InlineData(HomeAssistantEnergyPeriod.Day, "2026-10-25T10:17:00+01:00")]
    [InlineData(HomeAssistantEnergyPeriod.Month, "2026-10-02T00:00:00+02:00")]
    public async Task FossilEnergyRejectsNonBoundaryCalendarBuckets(
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
    [InlineData("{\"components\":[]}")]
    [InlineData("{\"time_zone\":\"Not/A-Time-Zone\",\"components\":[]}")]
    public async Task FossilEnergyRequiresSupportedHomeTimeZoneForCalendarBuckets(string configuration)
    {
        using var server = new TestHomeAssistantServer { ConfigurationResponseJson = configuration };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Energy.GetFossilEnergyConsumptionAsync(
            new DateTimeOffset(2026, 10, 26, 1, 30, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 10, 26, 3, 30, 0, TimeSpan.Zero),
            new[] { "sensor.energy" },
            "sensor.co2",
            HomeAssistantEnergyPeriod.Day));

        Assert.Null(server.GetLastWebSocketCommand("energy/fossil_energy_consumption"));
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

    private sealed class CancelingStringComparer : IComparer<string>
    {
        public int Compare(string? left, string? right)
            => throw new OperationCanceledException();
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

    private sealed class ReusedMutableStatisticRows : IReadOnlyCollection<HomeAssistantStatisticImportRow>
    {
        public int Count => 2;

        public IEnumerator<HomeAssistantStatisticImportRow> GetEnumerator()
        {
            var row = CreateSumRow(10, 1.5);
            yield return row;
            row.Start = row.Start.AddHours(1);
            row.Sum = 2.5;
            yield return row;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class MetadataMutatingStatisticRows : IReadOnlyCollection<HomeAssistantStatisticImportRow>
    {
        private readonly HomeAssistantStatisticImportMetadata _metadata;

        internal MetadataMutatingStatisticRows(HomeAssistantStatisticImportMetadata metadata) => _metadata = metadata;

        public int Count => 1;

        public IEnumerator<HomeAssistantStatisticImportRow> GetEnumerator()
        {
            _metadata.StatisticId = "external:changed";
            _metadata.Source = "changed";
            _metadata.HasSum = false;
            yield return CreateSumRow(10, 1.5);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    [Fact]
    public void RecorderImportRejectsLastResetAfterItsHourlyInterval()
    {
        var metadata = CreateSumImportMetadata();
        var row = CreateSumRow(10, 1.5);
        row.LastReset = row.Start.AddHours(1).AddTicks(1);

        Assert.Throws<ArgumentException>(() => metadata.ValidateRows(new[] { row }));

        row.LastReset = row.Start.AddHours(1);
        metadata.ValidateRows(new[] { row });
    }

    [Fact]
    public void WeatherForecastEntitySelectionHonorsCancellationAndCardinality()
    {
        using var response = JsonDocument.Parse("{\"weather.home\":{},\"weather.garden\":{},\"weather.third\":{}}");
        Assert.Throws<HomeAssistantProtocolException>(() =>
            HomeAssistantWeatherClient.RequireSingleForecastEntity(response.RootElement, CancellationToken.None));

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantWeatherClient.RequireSingleForecastEntity(response.RootElement, cancellation.Token));
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
