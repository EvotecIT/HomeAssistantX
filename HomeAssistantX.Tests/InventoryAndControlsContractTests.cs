#if NET10_0
using System.Text.Json;
using HomeAssistantX.Controls;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Inventory;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class InventoryAndControlsContractTests
{
    [Theory]
    [InlineData("{\"light\":null}")]
    [InlineData("{\"light\":{\"turn_on\":null}}")]
    public async Task TypedActionCatalogRejectsMalformedDomainAndActionDefinitions(string catalogJson)
    {
        using var server = new TestHomeAssistantServer { ActionCatalogResponseJson = catalogJson };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Services.GetActionsAsync());
    }

    [Fact]
    public async Task ExtendedEntityRegistryKeepsDocumentedNullFallbackEntries()
    {
        using var server = new TestHomeAssistantServer
        {
            ExtendedEntityRegistryResponseJson = "{\"sensor.kitchen_temperature\":null}"
        };
        using var client = TestClientFactory.Create(server);

        var snapshot = await client.Inventory.GetSnapshotAsync();
        var temperature = Assert.Single(snapshot.Entities, item => item.EntityId == "sensor.kitchen_temperature");

        Assert.NotNull(temperature.RegistryEntry);
    }

    [Fact]
    public async Task InventoryJoinsInheritedAreaFloorDeviceStateAndActions()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var snapshot = await client.Inventory.GetSnapshotAsync();

        var floor = Assert.Single(snapshot.Floors);
        var area = Assert.Single(snapshot.Areas);
        var device = Assert.Single(snapshot.Devices);
        var light = Assert.Single(snapshot.Entities, x => x.EntityId == "light.kitchen");
        var disabledTemperature = Assert.Single(snapshot.Entities, x => x.EntityId == "sensor.disabled_temperature");
        var legacyDisabled = Assert.Single(snapshot.Entities, x => x.EntityId == "sensor.legacy_disabled");
        var temperature = Assert.Single(snapshot.Entities, x => x.EntityId == "sensor.kitchen_temperature");
        var action = Assert.Single(snapshot.Actions, x => x.Domain == "light" && x.Action == "turn_on");
        var field = Assert.Single(action.Fields);

        Assert.Equal("Ground", floor.Name);
        Assert.Equal("Kitchen", area.Name);
        Assert.Equal("Kitchen Sensor", device.Name);
        Assert.Equal("Kitchen", light.AreaName);
        Assert.Equal("Ground", light.FloorName);
        Assert.Equal("Kitchen Sensor", light.DeviceName);
        Assert.Equal("Kitchen light", light.Name);
        Assert.Contains("Light", light.Aliases);
        Assert.Contains("Island fixture", light.Aliases);
        Assert.Equal("Kitchen Sensor Temperature", disabledTemperature.Name);
        Assert.Contains(disabledTemperature.Name, disabledTemperature.Aliases);
        Assert.Equal("Kitchen legacy temperature", legacyDisabled.Name);
        Assert.Contains(legacyDisabled.Name, legacyDisabled.Aliases);
        Assert.True(light.RegistryEntry!.AdditionalData.ContainsKey("list_only"));
        Assert.True(light.RegistryEntry.AdditionalData.ContainsKey("extended_only"));
        Assert.Equal("temperature", temperature.RegistryEntry!.DeviceClass);
        Assert.Equal("test", light.IntegrationDomain);
        Assert.Equal("Test integration", light.IntegrationTitle);
        Assert.Equal("off", light.State);
        Assert.True(light.IsAvailable);
        Assert.Equal("Turn on", Assert.Single(light.DomainActions, value => value.Action == "turn_on").Name);
        Assert.Equal("brightness_pct", field.Field);
        Assert.Equal(45, field.Example!.Value.GetInt32());
        Assert.Equal(100, field.Selector!.Value.GetProperty("number").GetProperty("max").GetInt32());
    }

    [Fact]
    public async Task EntityQueriesResolveNamesAndRejectAmbiguityOrWrongTargets()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var snapshot = await client.Inventory.GetSnapshotAsync();

        var kitchen = client.Inventory.ResolveArea(snapshot, "Kitchen");
        var kitchenByAlias = client.Inventory.ResolveArea(snapshot, "Cooking");
        var floorByAlias = client.Inventory.ResolveFloor(snapshot, "Downstairs");
        var entities = await client.Inventory.GetEntitiesAsync(new HomeAssistantEntityQuery
        {
            Area = "kitchen",
            Domain = "light"
        });

        Assert.Equal("kitchen", kitchen.AreaId);
        Assert.Equal(kitchen.AreaId, kitchenByAlias.AreaId);
        Assert.Equal("ground", floorByAlias.FloorId);
        Assert.Equal("light.kitchen", Assert.Single(entities).EntityId);
        Assert.Equal("light.kitchen", Assert.Single(await client.Inventory.GetEntitiesAsync(new HomeAssistantEntityQuery
        {
            Entity = new[] { "Island fixture" }
        })).EntityId);
        Assert.Throws<HomeAssistantLookupException>(() => client.Inventory.ResolveEntity(snapshot, "Missing light"));
    }

    [Fact]
    public async Task FriendlyNameResolutionRejectsAmbiguousEntities()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates(
            "[" + TestHomeAssistantServer.KitchenLightOffStateJson
            + ", {\"entity_id\":\"switch.kitchen\",\"state\":\"off\",\"attributes\":{\"friendly_name\":\"Kitchen light\"}}]");
        using var client = TestClientFactory.Create(server);

        var snapshot = await client.Inventory.GetSnapshotAsync();
        var exception = Assert.Throws<HomeAssistantLookupException>(
            () => client.Inventory.ResolveEntity(snapshot, "Kitchen light"));

        Assert.Contains("light.kitchen", exception.Message);
        Assert.Contains("switch.kitchen", exception.Message);
        await Assert.ThrowsAsync<HomeAssistantLookupException>(() => client.Inventory.GetEntitiesAsync(new HomeAssistantEntityQuery
        {
            Entity = new[] { "Kitchen light" }
        }));
        await Assert.ThrowsAsync<HomeAssistantLookupException>(() => client.Inventory.GetEntitiesAsync(new HomeAssistantEntityQuery
        {
            Entity = new[] { "light.kitchen", "Missing light" }
        }));
    }

    [Theory]
    [InlineData("camera.FRONT")]
    [InlineData("CAMERA.front")]
    [InlineData("camera.front.extra")]
    public async Task InventoryRejectsNoncanonicalStateEntityIds(string entityId)
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":\"" + entityId + "\",\"state\":\"idle\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Inventory.GetSnapshotAsync());
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\" camera.front\"")]
    public async Task InventoryClassifiesNullAndWhitespacePrefixedStateIds(string entityIdJson)
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[{\"entity_id\":" + entityIdJson + ",\"state\":\"idle\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Inventory.GetSnapshotAsync());
    }

    [Fact]
    public async Task TypedLightControlProducesTheNativeValidatedPayload()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Controls.Lights.TurnOnAsync(
            HomeAssistantTarget.ForArea("kitchen"),
            new HomeAssistantLightOptions
            {
                BrightnessPercent = 45,
                RgbColor = new[] { 10, 20, 30 },
                Transition = TimeSpan.FromSeconds(1.5)
            });

        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        var root = body.RootElement;
        Assert.Equal("light", root.GetProperty("domain").GetString());
        Assert.Equal("turn_on", root.GetProperty("service").GetString());
        Assert.Equal("kitchen", root.GetProperty("target").GetProperty("area_id")[0].GetString());
        Assert.Equal(45, root.GetProperty("service_data").GetProperty("brightness_pct").GetDouble());
        Assert.Equal(30, root.GetProperty("service_data").GetProperty("rgb_color")[2].GetInt32());
        Assert.Equal(1.5, root.GetProperty("service_data").GetProperty("transition").GetDouble());
    }

    [Fact]
    public async Task TypedDomainControlsMapValuesWithoutRawDictionaries()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Controls.Climate.SetAsync(
            HomeAssistantTarget.ForEntity("climate.kitchen"),
            new HomeAssistantClimateOptions { Temperature = 21.5, HvacMode = "heat" });
        using (var climate = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody)))
        {
            Assert.Equal("set_temperature", climate.RootElement.GetProperty("service").GetString());
            Assert.Equal(21.5, climate.RootElement.GetProperty("service_data").GetProperty("temperature").GetDouble());
            Assert.Equal("heat", climate.RootElement.GetProperty("service_data").GetProperty("hvac_mode").GetString());
        }

        await client.Controls.Covers.SetPositionAsync(HomeAssistantTarget.ForEntity("cover.kitchen"), 60);
        using (var cover = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody)))
        {
            Assert.Equal("set_cover_position", cover.RootElement.GetProperty("service").GetString());
            Assert.Equal(60, cover.RootElement.GetProperty("service_data").GetProperty("position").GetDouble());
        }

        await client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions { VolumePercent = 35, Muted = false });
        using (var media = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody)))
        {
            Assert.Equal("volume_mute", media.RootElement.GetProperty("service").GetString());
            Assert.False(media.RootElement.GetProperty("service_data").GetProperty("is_volume_muted").GetBoolean());
        }

        await client.Controls.Locks.ActAsync(HomeAssistantTarget.ForEntity("lock.front_door"), HomeAssistantLockAction.Unlock, "1234");
        using var lockCall = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.Equal("unlock", lockCall.RootElement.GetProperty("service").GetString());
        Assert.Equal("1234", lockCall.RootElement.GetProperty("service_data").GetProperty("code").GetString());
    }

    [Fact]
    public async Task MediaContentValidationFailsBeforeAnyServiceCall()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions
            {
                Power = HomeAssistantPowerAction.On,
                MediaContentId = "media-source://example"
            }));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task InvalidEnumValuesFailBeforeAnyServiceCall()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("lock.front_door");

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Controls.Locks.ActAsync(
            target,
            (HomeAssistantLockAction)99));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Controls.Covers.ActAsync(
            HomeAssistantTarget.ForEntity("cover.kitchen"),
            (HomeAssistantCoverAction)99));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions { Power = (HomeAssistantPowerAction)99 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions { Playback = (HomeAssistantMediaPlaybackAction)99 }));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task ClimateShapeValidationFailsBeforeAnyServiceCall()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("climate.kitchen");

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Climate.SetAsync(
            target,
            new HomeAssistantClimateOptions { TargetTemperatureLow = 18 }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Climate.SetAsync(
            target,
            new HomeAssistantClimateOptions { Temperature = 21, TargetTemperatureLow = 18, TargetTemperatureHigh = 24 }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Climate.SetAsync(
            target,
            new HomeAssistantClimateOptions { TargetTemperatureLow = 24, TargetTemperatureHigh = 18 }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Controls.Climate.SetAsync(
            target,
            new HomeAssistantClimateOptions { Temperature = double.NaN }));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task ContradictoryMediaOperationsFailBeforeAnyServiceCall()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions
            {
                Power = HomeAssistantPowerAction.Off,
                Playback = HomeAssistantMediaPlaybackAction.Play
            }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions
            {
                MediaContentId = "media-source://example",
                MediaContentType = "music",
                Playback = HomeAssistantMediaPlaybackAction.Play
            }));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task InventoryRemainsReadableWithoutConfigEntryPrivileges()
    {
        using var server = new TestHomeAssistantServer { ConfigEntriesErrorCode = "unauthorized" };
        using var client = TestClientFactory.Create(server);

        var snapshot = await client.Inventory.GetSnapshotAsync();
        var light = Assert.Single(snapshot.Entities, entity => entity.EntityId == "light.kitchen");

        Assert.Equal("Kitchen light", light.Name);
        Assert.Equal("Kitchen", light.AreaName);
        Assert.Null(light.IntegrationDomain);
        Assert.Empty(snapshot.Registries.ConfigEntries);
        Assert.False(snapshot.Registries.IsConfigEntryEnrichmentAvailable);
    }

    [Fact]
    public async Task InventoryDoesNotHideUnrelatedConfigEntryFailures()
    {
        using var server = new TestHomeAssistantServer { ConfigEntriesErrorCode = "temporary_failure" };
        using var client = TestClientFactory.Create(server);

        var exception = await Assert.ThrowsAsync<HomeAssistantCommandException>(
            () => client.Inventory.GetSnapshotAsync());

        Assert.Equal("temporary_failure", exception.Code);
    }

    [Fact]
    public async Task MediaPlayerAppliesSettingsBeforePlayback()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions
            {
                Power = HomeAssistantPowerAction.On,
                VolumePercent = 25,
                Muted = false,
                Source = "HDMI",
                Playback = HomeAssistantMediaPlaybackAction.Play
            });

        var services = server.ServiceCallBodies.Select(body =>
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.GetProperty("service").GetString();
        }).ToArray();

        Assert.Equal(new[] { "turn_on", "volume_set", "volume_mute", "select_source", "media_play" }, services);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void TypedPercentValuesRejectInvalidInput(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeAssistantLightOptions { BrightnessPercent = value });
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeAssistantMediaPlayerOptions { VolumePercent = value });
    }

    [Fact]
    public void TypedPercentValuesRejectNonFiniteInput()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeAssistantLightOptions { BrightnessPercent = double.NaN });
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeAssistantMediaPlayerOptions { VolumePercent = double.PositiveInfinity });
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeAssistantClimateOptions { Humidity = double.NegativeInfinity });
    }

    [Fact]
    public void LightColorRepresentationsAreMutuallyExclusiveInEitherAssignmentOrder()
    {
        Assert.Throws<ArgumentException>(() => new HomeAssistantLightOptions
        {
            ColorTemperatureKelvin = 3000,
            RgbColor = new[] { 10, 20, 30 }
        });
        Assert.Throws<ArgumentException>(() => new HomeAssistantLightOptions
        {
            RgbColor = new[] { 10, 20, 30 },
            ColorTemperatureKelvin = 3000
        });
    }
}
#endif
