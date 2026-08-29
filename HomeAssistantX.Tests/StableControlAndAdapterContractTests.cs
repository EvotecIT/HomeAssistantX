#if NET10_0
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HomeAssistantX.Authentication;
using HomeAssistantX.Controls;
using HomeAssistantX.Discovery;
using HomeAssistantX.Exceptions;
using HomeAssistantX.MobileApp;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class StableControlAndAdapterContractTests
{
    [Fact]
    public async Task CommonControlDomainsProduceTheirNativePayloads()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Controls.Fans.SetDirectionAsync(HomeAssistantTarget.ForEntity("fan.office"), HomeAssistantFanDirection.Reverse);
        AssertCall(server, "fan", "set_direction", "direction", "reverse");

        await client.Controls.Valves.SetPositionAsync(HomeAssistantTarget.ForEntity("valve.water"), 35);
        AssertCall(server, "valve", "set_valve_position", "position", 35d);

        await client.Controls.Vacuums.CleanAreaAsync(HomeAssistantTarget.ForEntity("vacuum.downstairs"), new[] { "kitchen", " hall " });
        using (var call = LastCall(server))
        {
            Assert.Equal("clean_area", call.RootElement.GetProperty("service").GetString());
            Assert.Equal(" hall ", call.RootElement.GetProperty("service_data").GetProperty("cleaning_area_id")[1].GetString());
        }

        await client.Controls.LawnMowers.ActAsync(HomeAssistantTarget.ForEntity("lawn_mower.garden"), HomeAssistantLawnMowerAction.Dock);
        AssertCall(server, "lawn_mower", "dock");

        await client.Controls.Alarms.ActAsync(HomeAssistantTarget.ForEntity("alarm_control_panel.home"), HomeAssistantAlarmAction.ArmNight, "1234");
        AssertCall(server, "alarm_control_panel", "alarm_arm_night", "code", "1234");

        await client.Controls.Sirens.ActAsync(HomeAssistantTarget.ForEntity("siren.house"), HomeAssistantSirenAction.TurnOn, new HomeAssistantSirenOptions { Tone = "alarm", VolumePercent = 40, Duration = TimeSpan.FromSeconds(5) });
        using (var call = LastCall(server))
        {
            var data = call.RootElement.GetProperty("service_data");
            Assert.Equal(0.4, data.GetProperty("volume_level").GetDouble(), 3);
            Assert.Equal(5, data.GetProperty("duration").GetDouble());
        }

        await client.Controls.Humidifiers.SetHumidityAsync(HomeAssistantTarget.ForEntity("humidifier.bedroom"), 55);
        AssertCall(server, "humidifier", "set_humidity", "humidity", 55d);

        await client.Controls.WaterHeaters.SetTemperatureAsync(HomeAssistantTarget.ForEntity("water_heater.tank"), 52.5, "eco");
        using (var call = LastCall(server))
        {
            var data = call.RootElement.GetProperty("service_data");
            Assert.Equal(52.5, data.GetProperty("temperature").GetDouble());
            Assert.Equal("eco", data.GetProperty("operation_mode").GetString());
        }

        await client.Controls.WaterHeaters.SetTemperatureAsync(HomeAssistantTarget.ForEntity("water_heater.tank"), 53, " comfort ");
        using (var call = LastCall(server))
        {
            Assert.Equal(" comfort ", call.RootElement.GetProperty("service_data").GetProperty("operation_mode").GetString());
        }
    }

    [Fact]
    public async Task IntegrationDefinedControlOptionsPreserveExactText()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Controls.Fans.SetPresetModeAsync(HomeAssistantTarget.ForEntity("fan.office"), " Breeze ");
        AssertCall(server, "fan", "set_preset_mode", "preset_mode", " Breeze ");

        await client.Controls.Humidifiers.SetModeAsync(HomeAssistantTarget.ForEntity("humidifier.bedroom"), " Quiet ");
        AssertCall(server, "humidifier", "set_mode", "mode", " Quiet ");

        await client.Controls.Vacuums.SetFanSpeedAsync(HomeAssistantTarget.ForEntity("vacuum.downstairs"), " Max+ ");
        AssertCall(server, "vacuum", "set_fan_speed", "fan_speed", " Max+ ");

        await client.Controls.Sirens.ActAsync(
            HomeAssistantTarget.ForEntity("siren.house"),
            HomeAssistantSirenAction.TurnOn,
            new HomeAssistantSirenOptions { Tone = " Alert 2 " });
        AssertCall(server, "siren", "turn_on", "tone", " Alert 2 ");

        await client.Controls.Lights.TurnOnAsync(
            HomeAssistantTarget.ForEntity("light.office"),
            new HomeAssistantLightOptions { Effect = " Aurora + " });
        AssertCall(server, "light", "turn_on", "effect", " Aurora + ");

        await client.Controls.Vacuums.SendCommandAsync(
            HomeAssistantTarget.ForEntity("vacuum.downstairs"),
            " Provider Command ");
        AssertCall(server, "vacuum", "send_command", "command", " Provider Command ");
    }

    [Fact]
    public void IntegrationDefinedLightEffectsRejectBlanksAtOptionConstruction()
    {
        Assert.Throws<ArgumentException>(() => new HomeAssistantLightOptions { Effect = " " });
    }

    [Fact]
    public async Task AlarmCodesRejectExplicitBlanksBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Alarms.ActAsync(
            HomeAssistantTarget.ForEntity("alarm_control_panel.home"),
            HomeAssistantAlarmAction.Disarm,
            " "));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task RoutineAndHelperControlsKeepDomainsAndValueShapesTyped()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Controls.Routines.ActivateSceneAsync(HomeAssistantTarget.ForEntity("scene.evening"), TimeSpan.FromSeconds(2));
        AssertCall(server, "scene", "turn_on", "transition", 2d);

        await client.Controls.Routines.PressButtonAsync(HomeAssistantTarget.ForEntity("input_button.reset"), HomeAssistantButtonDomain.InputButton);
        AssertCall(server, "input_button", "press");

        await client.Controls.Helpers.SetNumberAsync(HomeAssistantHelperDomain.InputNumber, HomeAssistantTarget.ForEntity("input_number.volume"), 12.5);
        AssertCall(server, "input_number", "set_value", "value", 12.5d);

        await client.Controls.Helpers.SetDateTimeAsync(HomeAssistantHelperDomain.InputDateTime, HomeAssistantTarget.ForEntity("input_datetime.visit"), new DateTimeOffset(2026, 8, 26, 12, 30, 0, TimeSpan.FromHours(2)));
        using var call = LastCall(server);
        Assert.Equal("set_datetime", call.RootElement.GetProperty("service").GetString());
        Assert.Equal("2026-08-26T12:30:00.0000000+02:00", call.RootElement.GetProperty("service_data").GetProperty("datetime").GetString());
    }

    [Fact]
    public async Task HelperSelectOptionsPreserveExactConfiguredText()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("input_select.mode");

        await client.Controls.Helpers.SelectOptionAsync(
            HomeAssistantHelperDomain.InputSelect,
            target,
            " Away ");
        using (var selectCall = LastCall(server))
        {
            Assert.Equal(" Away ", selectCall.RootElement.GetProperty("service_data").GetProperty("option").GetString());
        }

        await client.Controls.Helpers.SetSelectOptionsAsync(target, new[] { " Home ", "Away" });
        using var optionsCall = LastCall(server);
        Assert.Equal(
            new[] { " Home ", "Away" },
            optionsCall.RootElement.GetProperty("service_data").GetProperty("options")
                .EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task InvalidControlShapesFailBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var fan = HomeAssistantTarget.ForEntity("fan.office");

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Fans.ActAsync(fan, HomeAssistantFanAction.TurnOn, 10));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Controls.Valves.SetPositionAsync(HomeAssistantTarget.ForEntity("valve.water"), 101));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.WaterHeaters.SetTemperatureAsync(
            HomeAssistantTarget.ForEntity("water_heater.tank"),
            52,
            " "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Helpers.SetTextAsync(HomeAssistantHelperDomain.Select, HomeAssistantTarget.ForEntity("select.mode"), "eco"));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Controls.Sirens.ActAsync(HomeAssistantTarget.ForEntity("siren.house"), (HomeAssistantSirenAction)99));
        Assert.Throws<ArgumentException>(() => new HomeAssistantSirenOptions { Tone = "alarm", ToneId = 2 });
        Assert.Throws<ArgumentException>(() => new HomeAssistantSirenOptions { Tone = " " });
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeAssistantSirenOptions { Duration = TimeSpan.FromMilliseconds(500) });
        var cyclicVariables = new Dictionary<string, object?>();
        cyclicVariables["self"] = cyclicVariables;
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Routines.RunScriptAsync(
            HomeAssistantTarget.ForEntity("script.evening"),
            cyclicVariables));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Controls.Fans.SetDirectionAsync(
            HomeAssistantTarget.ForEntity("light.office"),
            HomeAssistantFanDirection.Forward,
            canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Controls.Helpers.SetNumberAsync(
            HomeAssistantHelperDomain.InputNumber,
            HomeAssistantTarget.ForEntity("number.volume"),
            12.5,
            canceled.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Controls.Routines.RunScriptAsync(
            HomeAssistantTarget.ForEntity("script.evening"),
            cyclicVariables,
            canceled.Token));
        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task RoutineHelperAndMobileCameraRejectWrongDomainTargetsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Routines.ActivateSceneAsync(
            HomeAssistantTarget.ForEntity("script.evening")));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Helpers.SetNumberAsync(
            HomeAssistantHelperDomain.InputNumber,
            HomeAssistantTarget.ForEntity("number.volume"),
            12.5));
        using var webhook = client.MobileApp.CreateWebhookClient(new HomeAssistantMobileAppRegistration
        {
            WebhookId = "test-webhook"
        });
        await Assert.ThrowsAsync<ArgumentException>(() => webhook.GetCameraStreamAsync("sensor.front"));
        await Assert.ThrowsAsync<ArgumentException>(() => webhook.GetCameraStreamAsync("camera.front.extra"));

        Assert.Null(server.LastServiceCallBody);
        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public void DnsSdParserReadsCompressedHomeAssistantAdvertisementAndRejectsUntrustedUris()
    {
        var aggregate = new DnsDiscoveryAggregate();
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), aggregate);

        var instance = Assert.Single(aggregate.Build());
        Assert.Equal("My Home", instance.Name);
        Assert.Equal("test-uuid", instance.InstanceId);
        Assert.Equal("2026.8.3", instance.Version);
        Assert.Equal("ha.local", instance.HostName);
        Assert.Equal(8123, instance.Port);
        Assert.Equal(new Uri("http://ha.local:8123/"), instance.InternalUri);
        Assert.Null(instance.ExternalUri);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), Assert.Single(instance.Addresses));

        var query = DnsDiscoveryPacket.CreateQuery();
        Assert.Equal(0x80, query[^2]);
        Assert.Equal(0x01, query[^1]);
        DnsDiscoveryPacket.ReadInto(new byte[] { 0, 1, 2 }, aggregate);
    }

    [Fact]
    public void DnsSdParserHonorsGoodbyeTtlAndCacheFlushRecordSets()
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), aggregate);

        now += TimeSpan.FromSeconds(2);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(recordClass: 0x8001, addressLastOctet: 20), aggregate);
        Assert.Equal(2, Assert.Single(aggregate.Build()).Addresses.Count);
        now += TimeSpan.FromMilliseconds(1100);
        Assert.Equal(IPAddress.Parse("192.0.2.20"), Assert.Single(Assert.Single(aggregate.Build()).Addresses));

        now += TimeSpan.FromSeconds(2);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(ttl: 0, addressLastOctet: 20), aggregate);
        Assert.Single(aggregate.Build());
        now += TimeSpan.FromMilliseconds(1100);
        Assert.Empty(aggregate.Build());
        Assert.Equal(0, aggregate.ServiceCount);
        Assert.Equal(0, aggregate.TextOwnerCount);
        Assert.Equal(0, aggregate.AddressHostCount);
    }

    [Fact]
    public void DnsSdCacheFlushMakesRoomForAuthoritativeRecordsAtPerOwnerLimits()
    {
        var now = TimeSpan.Zero;
        var services = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(host: "service-0.local"), services);
        for (var index = 1; index < 8; index++)
        {
            now += TimeSpan.FromMilliseconds(10);
            DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, index, 0, $"service-{index}.local", 8123), services);
        }
        now += TimeSpan.FromSeconds(2);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 9, 0, "authoritative.local", 8123, cacheFlush: true), services);
        now += TimeSpan.FromMilliseconds(100);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 0, 0, "service-0.local", 8123), services);
        now += TimeSpan.FromMilliseconds(1000);
        Assert.Equal("service-0.local", Assert.Single(services.Build()).HostName);

        now = TimeSpan.Zero;
        var text = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(textItems: new[] { "location_name=Name 0" }), text);
        for (var index = 1; index < 8; index++)
        {
            now += TimeSpan.FromMilliseconds(10);
            DnsDiscoveryPacket.ReadInto(CreateTxtOnlyPacket(120, new[] { $"location_name=Name {index}" }), text);
        }
        now += TimeSpan.FromSeconds(2);
        DnsDiscoveryPacket.ReadInto(CreateTxtOnlyPacket(120, new[] { "location_name=Authoritative" }, cacheFlush: true), text);
        now += TimeSpan.FromMilliseconds(100);
        DnsDiscoveryPacket.ReadInto(CreateTxtOnlyPacket(120, new[] { "location_name=Name 0" }), text);
        now += TimeSpan.FromMilliseconds(1000);
        Assert.Equal("Name 0", Assert.Single(text.Build()).Name);

        now = TimeSpan.Zero;
        var addresses = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(addressLastOctet: 1), addresses);
        for (byte lastOctet = 2; lastOctet <= 16; lastOctet++)
        {
            now += TimeSpan.FromMilliseconds(10);
            DnsDiscoveryPacket.ReadInto(CreateAddressOnlyPacket(120, IPAddress.Parse($"192.0.2.{lastOctet}")), addresses);
        }
        now += TimeSpan.FromSeconds(2);
        var authoritativeAddress = IPAddress.Parse("192.0.2.99");
        DnsDiscoveryPacket.ReadInto(CreateAddressOnlyPacket(120, authoritativeAddress, cacheFlush: true), addresses);
        Assert.Equal(17, Assert.Single(addresses.Build()).Addresses.Count);
        now += TimeSpan.FromMilliseconds(100);
        var rescuedAddress = IPAddress.Parse("192.0.2.1");
        DnsDiscoveryPacket.ReadInto(CreateAddressOnlyPacket(120, rescuedAddress), addresses);
        now += TimeSpan.FromMilliseconds(1000);
        Assert.Equal(new[] { rescuedAddress, authoritativeAddress }, Assert.Single(addresses.Build()).Addresses);
    }

    [Fact]
    public void DnsSdCacheExpiresRefreshesAndMatchesGoodbyesByRdata()
    {
        var now = TimeSpan.Zero;
        var expiring = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(ttl: 1), expiring);
        now += TimeSpan.FromMilliseconds(1100);
        Assert.Empty(expiring.Build());

        now = TimeSpan.Zero;
        var rescued = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), rescued);
        now += TimeSpan.FromSeconds(2);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(ttl: 0), rescued);
        now += TimeSpan.FromMilliseconds(500);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), rescued);
        now += TimeSpan.FromSeconds(1);
        Assert.Single(rescued.Build());

        now += TimeSpan.FromSeconds(2);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(recordClass: 0x8001, host: "new.local", textItems: new[] { "location_name=New Home", "internal_url=http://new.local:8123/" }), rescued);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(0, 0, 0, "ha.local", 8123), rescued);
        DnsDiscoveryPacket.ReadInto(CreateTxtOnlyPacket(0, new[] { "location_name=My Home", "uuid=test-uuid", "version=2026.8.3", "internal_url=http://ha.local:8123/", "external_url=file:///unsafe" }), rescued);
        now += TimeSpan.FromMilliseconds(1100);
        var current = Assert.Single(rescued.Build());
        Assert.Equal("new.local", current.HostName);
        Assert.Equal("New Home", current.Name);
    }

    [Fact]
    public void DnsSdRecentGoodbyeExpiresEveryMatchingRecordAfterOneSecond()
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(ttl: 0), aggregate);
        Assert.Single(aggregate.Build());
        now += TimeSpan.FromMilliseconds(1100);
        Assert.Empty(aggregate.Build());
        Assert.Equal(0, aggregate.ServiceCount);
        Assert.Equal(0, aggregate.TextOwnerCount);
        Assert.Equal(0, aggregate.AddressHostCount);
    }

    [Fact]
    public void DnsSdSrvGoodbyeIncludesPriorityAndWeightInRdataIdentity()
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), aggregate);
        now += TimeSpan.FromSeconds(2);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 2, "ha.local", 8123, cacheFlush: true), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(0, 0, 0, "ha.local", 8123), aggregate);
        now += TimeSpan.FromMilliseconds(1100);
        Assert.Equal("ha.local", Assert.Single(aggregate.Build()).HostName);
    }

    [Fact]
    public void DnsSdSelectsLowestPriorityAndUsesWeightWithinThatPriority()
    {
        var aggregate = new DnsDiscoveryAggregate(weightedSelector: maximum => maximum - 1);
        aggregate.AddInstance("Test._home-assistant._tcp.local");
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 10, 100, "fallback.local", 8123), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 1, "primary-a.local", 8123), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 9, "primary-b.local", 8123), aggregate);

        var instance = Assert.Single(aggregate.Build());

        Assert.Equal("primary-b.local", instance.HostName);
    }

    [Fact]
    public void DnsSdWeightedSelectionIncludesZeroWeightRecordsAtTheInclusiveZeroDraw()
    {
        var observedMaximum = 0;
        var zeroDraw = new DnsDiscoveryAggregate(weightedSelector: maximum =>
        {
            observedMaximum = maximum;
            return 0;
        });
        zeroDraw.AddInstance("Test._home-assistant._tcp.local");
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 0, "zero.local", 8123), zeroDraw);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 9, "positive.local", 8123), zeroDraw);

        Assert.Equal("zero.local", Assert.Single(zeroDraw.Build()).HostName);
        Assert.Equal(10, observedMaximum);

        var positiveDraw = new DnsDiscoveryAggregate(weightedSelector: _ => 1);
        positiveDraw.AddInstance("Test._home-assistant._tcp.local");
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 0, "zero.local", 8123), positiveDraw);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 9, "positive.local", 8123), positiveDraw);
        Assert.Equal("positive.local", Assert.Single(positiveDraw.Build()).HostName);
    }

    [Fact]
    public void DnsSdWeightedSelectionCanReachEveryZeroWeightRecord()
    {
        var draws = new Queue<int>(new[] { 1, 0 });
        var aggregate = new DnsDiscoveryAggregate(weightedSelector: maximum =>
        {
            var draw = draws.Dequeue();
            Assert.InRange(draw, 0, maximum - 1);
            return draw;
        });
        aggregate.AddInstance("Test._home-assistant._tcp.local");
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 0, "zero-a.local", 8123), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 0, "zero-b.local", 8123), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 1, 9, "positive.local", 8123), aggregate);

        Assert.Equal("zero-b.local", Assert.Single(aggregate.Build()).HostName);
        Assert.Empty(draws);
    }

    [Fact]
    public void DnsSdNoServiceSrvSuppressesTheAdvertisedInstance()
    {
        var aggregate = new DnsDiscoveryAggregate(clock: () => TimeSpan.Zero);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), aggregate);
        Assert.Single(aggregate.Build());

        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 0, 0, ".", 0), aggregate);
        Assert.Empty(aggregate.Build());
        Assert.Equal(0, aggregate.ServiceCount);
        Assert.Equal(0, aggregate.TextOwnerCount);
        Assert.Equal(0, aggregate.AddressHostCount);

        DnsDiscoveryPacket.ReadInto(CreatePtrOnlyPacket(120), aggregate);
        Assert.Empty(aggregate.Build());
    }

    [Fact]
    public void DnsSdParserAppliesOnlyCompleteValidResponseDatagrams()
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), aggregate);
        now += TimeSpan.FromSeconds(2);

        var malformedGoodbye = CreateDiscoveryPacket(ttl: 0).Concat(new byte[] { 0 }).ToArray();
        malformedGoodbye[7] = 5;
        DnsDiscoveryPacket.ReadInto(malformedGoodbye, aggregate);
        now += TimeSpan.FromSeconds(2);
        Assert.Single(aggregate.Build());

        var query = CreateDiscoveryPacket();
        query[2] = 0;
        query[3] = 0;
        var wrongOpcode = CreateDiscoveryPacket();
        wrongOpcode[2] = 0x88;
        var wrongRcode = CreateDiscoveryPacket();
        wrongRcode[3] = 1;
        var invalid = new DnsDiscoveryAggregate();
        DnsDiscoveryPacket.ReadInto(query, invalid);
        DnsDiscoveryPacket.ReadInto(wrongOpcode, invalid);
        DnsDiscoveryPacket.ReadInto(wrongRcode, invalid);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(recordClass: 2), invalid);
        Assert.Empty(invalid.Build());
    }

    [Fact]
    public void DnsSdParserRejectsQuotaPoisoningAndInvalidTxtKeys()
    {
        var unrelated = new DnsDiscoveryAggregate();
        DnsDiscoveryPacket.ReadInto(CreateUnrelatedDiscoveryPacket(), unrelated);
        Assert.Equal(0, unrelated.ServiceCount);
        Assert.Equal(0, unrelated.TextOwnerCount);
        Assert.Equal(0, unrelated.AddressHostCount);

        var aggregate = new DnsDiscoveryAggregate();
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(textItems: new[]
        {
            "location_name=Home",
            "internal_url=http://good.local:8123/",
            "INTERNAL_URL=http://bad.local:8123/",
            "=empty",
            "\u0001control=value"
        }), aggregate);
        var instance = Assert.Single(aggregate.Build());
        Assert.Equal(new Uri("http://good.local:8123/"), instance.InternalUri);
        Assert.Equal(2, instance.Properties.Count);

        var boundedText = new DnsDiscoveryAggregate();
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(textItems: Enumerable.Range(0, 80).Select(index => $"key{index}=value").ToArray()), boundedText);
        Assert.Equal(64, Assert.Single(boundedText.Build()).Properties.Count);

        var goodbyeOnly = new DnsDiscoveryAggregate();
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(ttl: 0, additionalTtl: 120), goodbyeOnly);
        Assert.Empty(goodbyeOnly.Build());
        Assert.Equal(0, goodbyeOnly.ServiceCount);
        Assert.Equal(0, goodbyeOnly.TextOwnerCount);
        Assert.Equal(0, goodbyeOnly.AddressHostCount);

        var retainedOnly = new DnsDiscoveryAggregate();
        for (var index = 0; index < 9; index++)
            DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(host: $"host-{index}.local", srvPriority: index), retainedOnly);
        Assert.Equal(8, retainedOnly.AddressHostCount);
    }

    [Theory]
    [InlineData(33, 5)]
    [InlineData(1, 3)]
    [InlineData(28, 15)]
    public void DnsSdMalformedKnownRecordPreventsEarlierDestructiveUpdates(int recordType, int dataLength)
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(), aggregate);
        now += TimeSpan.FromSeconds(2);
        DnsDiscoveryPacket.ReadInto(AppendMalformedRecord(CreateDiscoveryPacket(ttl: 0), recordType, dataLength), aggregate);
        now += TimeSpan.FromMilliseconds(1100);
        Assert.Single(aggregate.Build());
    }

    [Fact]
    public void DnsSdAggregationRequiresTargetPtrAndBoundsUntrustedRecords()
    {
        var unrelated = new DnsDiscoveryAggregate();
        unrelated.AddService("Printer._ipp._tcp.local", "printer.local", 631);
        unrelated.AddText("Printer._ipp._tcp.local", "note", "not Home Assistant");
        Assert.Empty(unrelated.Build());

        var aggregate = new DnsDiscoveryAggregate();
        for (var index = 0; index < 500; index++)
        {
            var instance = $"Instance-{index}._home-assistant._tcp.local";
            aggregate.AddInstance(instance);
            aggregate.AddService(instance, $"host-{index}.local", 8123);
            aggregate.AddText(instance, $"key-{index}", new string('x', 200));
            aggregate.AddAddress($"host-{index}.local", IPAddress.Parse($"192.0.2.{index % 254 + 1}"));
        }

        Assert.Equal(64, aggregate.InstanceCount);
        Assert.Equal(64, aggregate.ServiceCount);
        Assert.Equal(64, aggregate.TextOwnerCount);
        Assert.Equal(64, aggregate.AddressHostCount);
        Assert.Equal(64, aggregate.Build().Count);

        var perOwner = new DnsDiscoveryAggregate();
        const string target = "Bounded._home-assistant._tcp.local";
        perOwner.AddInstance(target);
        perOwner.AddService(target, "bounded.local", 8123);
        for (var index = 0; index < 100; index++)
        {
            perOwner.AddText(target, $"key-{index}", "value");
            perOwner.AddAddress("bounded.local", IPAddress.Parse($"192.0.{index / 254}.{index % 254 + 1}"));
        }

        var bounded = Assert.Single(perOwner.Build());
        Assert.Equal(64, bounded.Properties.Count);
        Assert.Equal(16, bounded.Addresses.Count);
        for (var index = 0; index < 256; index++) Assert.True(perOwner.TryConsumeDatagram());
        Assert.False(perOwner.TryConsumeDatagram());
        Assert.Equal(256, perOwner.DatagramCount);

        DnsDiscoveryPacket.ReadInto(CreateOversizedDiscoveryNamePacket(), perOwner);
        Assert.Single(perOwner.Build());
    }

    [Fact]
    public void DnsSdAggregationUsesTheNativeInstanceAsAStableNameTieBreaker()
    {
        var aggregate = new DnsDiscoveryAggregate();
        const string second = "Second._home-assistant._tcp.local";
        const string first = "First._home-assistant._tcp.local";
        aggregate.AddInstance(second);
        aggregate.AddText(second, "location_name", "Home");
        aggregate.AddInstance(first);
        aggregate.AddText(first, "location_name", "Home");

        Assert.Equal(new[] { first, second }, aggregate.Build().Select(value => value.ServiceInstanceName));
    }

    [Fact]
    public async Task DnsSdDiscoveryKeepsIdenticalResponsesSeparatedAcrossInterfaces()
    {
        var addresses = new[] { IPAddress.Parse("192.0.2.10"), IPAddress.Parse("198.51.100.20") };
        var factory = new TestDiscoveryTransportFactory(addresses, CreateDiscoveryPacket());
        var client = new HomeAssistantDiscoveryClient(factory);

        var instances = await client.DiscoverAsync(TimeSpan.FromMilliseconds(100));

        Assert.Equal(2, instances.Count);
        Assert.Equal(addresses.OrderBy(value => value.ToString()), factory.CreatedAddresses.OrderBy(value => value.ToString()));
        Assert.Equal(addresses.OrderBy(value => value.ToString()), factory.SentAddresses.OrderBy(value => value.ToString()));
    }

    [Fact]
    public async Task DnsSdDiscoveryRetransmitsAHomeAssistantQueryOnEachEligibleAddress()
    {
        var address = IPAddress.Parse("192.0.2.10");
        var factory = new TestDiscoveryTransportFactory(new[] { address }, CreateDiscoveryPacket());
        var client = new HomeAssistantDiscoveryClient(factory);

        Assert.Single(await client.DiscoverAsync(TimeSpan.FromMilliseconds(1100)));

        Assert.Equal(2, factory.SentAddresses.Count(value => value.Equals(address)));
    }

    [Fact]
    public void DnsSdExpiredPtrReleasesLongLivedChildRecordBudgets()
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateDiscoveryPacket(ttl: 1, additionalTtl: 120), aggregate);
        Assert.Equal(1, aggregate.ServiceCount);
        Assert.Equal(1, aggregate.TextOwnerCount);
        Assert.Equal(1, aggregate.AddressHostCount);

        now += TimeSpan.FromMilliseconds(1100);

        Assert.Empty(aggregate.Build());
        Assert.Equal(0, aggregate.ServiceCount);
        Assert.Equal(0, aggregate.TextOwnerCount);
        Assert.Equal(0, aggregate.AddressHostCount);
    }

    [Fact]
    public async Task DnsSdDiscoveryRetainsValidResponsesBeforeLateInterfaceFailures()
    {
        var address = IPAddress.Parse("192.0.2.10");
        var client = new HomeAssistantDiscoveryClient(
            new TestDiscoveryTransportFactory(new[] { address }, CreateDiscoveryPacket(), failAfterPacket: true));

        Assert.Single(await client.DiscoverAsync(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void Ipv4DnsSdDiscoveryDoesNotExposeUnscopedAaaaRecords()
    {
        var aggregate = new DnsDiscoveryAggregate();
        DnsDiscoveryPacket.ReadInto(
            AppendAaaaRecord(CreateDiscoveryPacket(), "ha.local", IPAddress.Parse("fe80::1234")),
            aggregate);

        var instance = Assert.Single(aggregate.Build());
        Assert.All(instance.Addresses, address => Assert.Equal(AddressFamily.InterNetwork, address.AddressFamily));
    }

    [Fact]
    public async Task DnsSdDiscoveryRetainsHealthyInterfacesWhenOneTransportCannotBeCreated()
    {
        var failed = IPAddress.Parse("192.0.2.10");
        var healthy = IPAddress.Parse("198.51.100.20");
        var client = new HomeAssistantDiscoveryClient(
            new TestDiscoveryTransportFactory(new[] { failed, healthy }, CreateDiscoveryPacket(), failCreateAddress: failed));

        Assert.Single(await client.DiscoverAsync(TimeSpan.FromMilliseconds(100)));
    }

    [Fact]
    public void DnsSdMergeKeepsDifferentHostsSeparateAndReappliesPublicBounds()
    {
        var properties = Enumerable.Range(0, 70).ToDictionary(value => "key" + value, value => (string?)value.ToString());
        var addresses = Enumerable.Range(1, 20).Select(value => IPAddress.Parse("192.0.2." + value)).ToArray();
        var first = new HomeAssistantDiscoveredInstance
        {
            ServiceInstanceName = "Home._home-assistant._tcp.local",
            HostName = "first.local",
            Port = 8123,
            InstanceId = "same-claimed-id",
            Addresses = addresses,
            Properties = properties
        };
        var second = new HomeAssistantDiscoveredInstance
        {
            ServiceInstanceName = first.ServiceInstanceName,
            HostName = "second.local",
            Port = 8123,
            InstanceId = first.InstanceId,
            Addresses = new[] { IPAddress.Parse("198.51.100.20") }
        };

        var merged = HomeAssistantDiscoveryClient.MergeInstances(new[] { first, first, second });

        Assert.Equal(2, merged.Count);
        var bounded = Assert.Single(merged, value => value.HostName == "first.local");
        Assert.Equal(64, bounded.Properties.Count);
        Assert.Equal(16, bounded.Addresses.Count);

        var fallback = new HomeAssistantDiscoveredInstance
        {
            ServiceInstanceName = "Partial._home-assistant._tcp.local",
            Name = "Partial",
            HostName = "partial.local",
            Port = 8123
        };
        var advertised = new HomeAssistantDiscoveredInstance
        {
            ServiceInstanceName = fallback.ServiceInstanceName,
            Name = "Advertised Home",
            HostName = fallback.HostName,
            Port = fallback.Port,
            Properties = new Dictionary<string, string?> { ["location_name"] = "Advertised Home" }
        };
        Assert.Equal("Advertised Home", Assert.Single(HomeAssistantDiscoveryClient.MergeInstances(new[] { fallback, advertised })).Name);
    }


    [Fact]
    public void DnsSdTunnelEligibilityDependsOnStateAndMulticastSupport()
    {
        Assert.True(UdpHomeAssistantDiscoveryTransportFactory.IsEligible(
            OperationalStatus.Up, supportsMulticast: true, NetworkInterfaceType.Tunnel));
        Assert.False(UdpHomeAssistantDiscoveryTransportFactory.IsEligible(
            OperationalStatus.Down, supportsMulticast: true, NetworkInterfaceType.Tunnel));
        Assert.False(UdpHomeAssistantDiscoveryTransportFactory.IsEligible(
            OperationalStatus.Up, supportsMulticast: false, NetworkInterfaceType.Tunnel));
        Assert.False(UdpHomeAssistantDiscoveryTransportFactory.IsEligible(
            OperationalStatus.Up, supportsMulticast: true, NetworkInterfaceType.Loopback));
    }

    [Fact]
    public void DnsSdInterfaceQuotasRemainGloballyBoundedAndDeterministic()
    {
        var limits = Enumerable.Range(0, 32).Select(index => DnsDiscoveryLimits.ForInterface(index, 32)).ToArray();

        Assert.Equal(64, limits.Sum(limit => limit.Instances));
        Assert.Equal(128, limits.Sum(limit => limit.Services));
        Assert.Equal(128, limits.Sum(limit => limit.TextOwners));
        Assert.Equal(128, limits.Sum(limit => limit.AddressHosts));
        Assert.Equal(256, limits.Sum(limit => limit.Datagrams));
        Assert.All(limits, limit => Assert.Equal(8, limit.Datagrams));
    }

    [Fact]
    public void DnsSdTransportConfiguresItsOutboundIpv4Interface()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        var address = IPAddress.Parse("127.0.0.1");

        UdpHomeAssistantDiscoveryTransport.ConfigureOutboundInterface(socket, address);

        Assert.Equal(
            address.GetAddressBytes(),
            Assert.IsType<byte[]>(socket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, 4)));
    }

    [Fact]
    public void DnsSdTransportConfiguresRequiredMulticastTimeToLive()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

        UdpHomeAssistantDiscoveryTransport.ConfigureMulticastTimeToLive(socket);

        Assert.Equal(255, Convert.ToInt32(
            socket.GetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive),
            System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void DnsSdTransportListensOnTheMulticastServicePort()
    {
        var endpoint = UdpHomeAssistantDiscoveryTransport.CreateMulticastListenerEndpoint(
            IPAddress.Parse("192.0.2.10"),
            5353);

        Assert.Equal(IPAddress.Any, endpoint.Address);
        Assert.Equal(5353, endpoint.Port);
        Assert.Throws<ArgumentException>(() =>
            UdpHomeAssistantDiscoveryTransport.CreateMulticastListenerEndpoint(IPAddress.IPv6Any, 5353));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            UdpHomeAssistantDiscoveryTransport.CreateMulticastListenerEndpoint(IPAddress.Loopback, 0));
        Assert.True(UdpHomeAssistantDiscoveryTransport.IsExpectedInterface(7, 7));
        Assert.False(UdpHomeAssistantDiscoveryTransport.IsExpectedInterface(7, 8));
        Assert.False(UdpHomeAssistantDiscoveryTransport.IsExpectedInterface(0, 0));
    }

    [Fact]
    public void DnsSdTransportRetainsQuerySocketsWhenListenerAllocationFails()
    {
        UdpClient? queryClient = null;
        Socket? querySocket = null;
        var allocations = 0;

        using (var transport = new UdpHomeAssistantDiscoveryTransport(
            new[] { IPAddress.Loopback },
            1,
            IPAddress.Parse("224.0.0.251"),
            5353,
            () =>
            {
                allocations++;
                if (allocations == 2) throw new InvalidOperationException("Listener allocation failed.");
                queryClient = new UdpClient(AddressFamily.InterNetwork);
                querySocket = queryClient.Client;
                return queryClient;
            }))
        {
            Assert.False(transport.IsMulticastAvailable);
            Assert.NotNull(queryClient);
            Assert.NotNull(querySocket);
            Assert.False(querySocket.SafeHandle.IsClosed);
        }

        Assert.True(querySocket!.SafeHandle.IsClosed);
    }

    [Fact]
    public async Task DnsSdTransportContinuesSendingWhenAReceivePathDisposesOneAlias()
    {
        var allocations = 0;
        var sends = 0;
        using var transport = new UdpHomeAssistantDiscoveryTransport(
            new[] { IPAddress.Loopback, IPAddress.Parse("127.0.0.2") },
            1,
            IPAddress.Parse("224.0.0.251"),
            5353,
            () =>
            {
                allocations++;
                if (allocations == 3) throw new InvalidOperationException("Listener allocation failed.");
                return new UdpClient(AddressFamily.InterNetwork);
            },
            (_, payload, _) =>
            {
                var current = Interlocked.Increment(ref sends);
                return current == 1
                    ? Task.FromException<int>(new ObjectDisposedException("receive-side socket"))
                    : Task.FromResult(payload.Length);
            });

        await transport.SendAsync(DnsDiscoveryPacket.CreateQuery(), CancellationToken.None);

        Assert.Equal(2, sends);
    }

    [Fact]
    public void DnsSdRetainsBoundedChildRecordsUntilTheirPtrArrives()
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 0, 0, "ha.local", 8123), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateTxtOnlyPacket(120, new[] { "location_name=My Home", "uuid=test-uuid" }), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateAddressOnlyPacket(120, IPAddress.Parse("192.0.2.10")), aggregate);

        Assert.Empty(aggregate.Build());
        Assert.Equal(1, aggregate.ServiceCount);
        Assert.Equal(1, aggregate.TextOwnerCount);
        Assert.Equal(1, aggregate.AddressHostCount);

        DnsDiscoveryPacket.ReadInto(CreatePtrOnlyPacket(120), aggregate);

        var instance = Assert.Single(aggregate.Build());
        Assert.Equal("ha.local", instance.HostName);
        Assert.Equal(8123, instance.Port);
        Assert.Equal("My Home", instance.Name);
        Assert.Equal("test-uuid", instance.InstanceId);
        Assert.Equal(IPAddress.Parse("192.0.2.10"), Assert.Single(instance.Addresses));

        now += TimeSpan.FromMilliseconds(5100);
        instance = Assert.Single(aggregate.Build());
        Assert.Equal("ha.local", instance.HostName);
        Assert.Equal("My Home", instance.Name);

        var expiring = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 0, 0, "pending.local", 8123), expiring);
        DnsDiscoveryPacket.ReadInto(CreateTxtOnlyPacket(120, new[] { "location_name=Pending" }), expiring);
        now += TimeSpan.FromMilliseconds(5100);
        Assert.Empty(expiring.Build());
        Assert.Equal(0, expiring.ServiceCount);
        Assert.Equal(0, expiring.TextOwnerCount);
    }

    [Fact]
    public void DnsSdRetainsAnAddressThatArrivesBeforeItsServiceRecord()
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        var address = IPAddress.Parse("192.0.2.10");

        DnsDiscoveryPacket.ReadInto(CreateAddressOnlyPacket(120, address), aggregate);
        Assert.Equal(1, aggregate.AddressHostCount);
        Assert.Empty(aggregate.Build());

        DnsDiscoveryPacket.ReadInto(CreatePtrOnlyPacket(120), aggregate);
        DnsDiscoveryPacket.ReadInto(CreateSrvOnlyPacket(120, 0, 0, "ha.local", 8123), aggregate);

        var instance = Assert.Single(aggregate.Build());
        Assert.Equal(address, Assert.Single(instance.Addresses));

        now += TimeSpan.FromMilliseconds(5100);
        Assert.Equal(address, Assert.Single(Assert.Single(aggregate.Build()).Addresses));
    }

    [Fact]
    public void DnsSdExpiresAnUncorrelatedPendingAddress()
    {
        var now = TimeSpan.Zero;
        var aggregate = new DnsDiscoveryAggregate(clock: () => now);
        DnsDiscoveryPacket.ReadInto(CreateAddressOnlyPacket(120, IPAddress.Parse("192.0.2.10")), aggregate);

        now += TimeSpan.FromMilliseconds(5100);

        Assert.Equal(0, aggregate.AddressHostCount);
    }

    [Fact]
    public void DnsSdPacketIndexingIsLinearForCacheFlushRecords()
    {
        var updates = Enumerable.Range(0, 64)
            .Select(index => new DnsDiscoveryUpdate
            {
                Kind = DnsDiscoveryRecordKind.Srv,
                Name = "Test._home-assistant._tcp.local",
                Host = $"host-{index}.local",
                Port = 8123,
                DataKey = $"service-{index}",
                Ttl = 120,
                CacheFlush = true
            })
            .ToArray();
        var packet = new CountingReadOnlyList<DnsDiscoveryUpdate>(updates, updates.Length + 1);
        var aggregate = new DnsDiscoveryAggregate();

        aggregate.ApplyPacket(packet);

        Assert.InRange(packet.MoveNextCount, updates.Length, updates.Length + 1);
    }

    [Fact]
    public void DnsSdVerifiedRecordsEvictPendingOwnersAtSharedQuotas()
    {
        var aggregate = new DnsDiscoveryAggregate(clock: () => TimeSpan.Zero);
        for (var index = 0; index < 128; index++)
        {
            var pendingInstance = $"Pending-{index}._home-assistant._tcp.local";
            var host = $"pending-{index}.local";
            aggregate.ApplyPacket(new[]
            {
                new DnsDiscoveryUpdate
                {
                    Kind = DnsDiscoveryRecordKind.Srv,
                    Name = pendingInstance,
                    Host = host,
                    Port = 8123,
                    DataKey = $"service-{index}",
                    Ttl = 120
                },
                new DnsDiscoveryUpdate
                {
                    Kind = DnsDiscoveryRecordKind.Txt,
                    Name = pendingInstance,
                    Properties = new Dictionary<string, string?> { ["location_name"] = $"Pending {index}" },
                    DataKey = $"text-{index}",
                    Ttl = 120
                },
                new DnsDiscoveryUpdate
                {
                    Kind = DnsDiscoveryRecordKind.A,
                    Name = host,
                    Address = IPAddress.Parse($"192.0.2.{index % 254 + 1}"),
                    DataKey = $"address-{index}",
                    Ttl = 120
                }
            });
        }
        Assert.Equal(128, aggregate.ServiceCount);
        Assert.Equal(128, aggregate.TextOwnerCount);
        Assert.Equal(128, aggregate.AddressHostCount);

        aggregate.ApplyPacket(new[]
        {
            new DnsDiscoveryUpdate
            {
                Kind = DnsDiscoveryRecordKind.Ptr,
                Name = "_home-assistant._tcp.local",
                Target = "Verified._home-assistant._tcp.local",
                DataKey = "VERIFIED._HOME-ASSISTANT._TCP.LOCAL",
                Ttl = 120
            },
            new DnsDiscoveryUpdate
            {
                Kind = DnsDiscoveryRecordKind.Srv,
                Name = "Verified._home-assistant._tcp.local",
                Host = "verified.local",
                Port = 8123,
                DataKey = "verified-service",
                Ttl = 120
            },
            new DnsDiscoveryUpdate
            {
                Kind = DnsDiscoveryRecordKind.Txt,
                Name = "Verified._home-assistant._tcp.local",
                Properties = new Dictionary<string, string?> { ["location_name"] = "Verified" },
                DataKey = "verified-text",
                Ttl = 120
            },
            new DnsDiscoveryUpdate
            {
                Kind = DnsDiscoveryRecordKind.A,
                Name = "verified.local",
                Address = IPAddress.Parse("198.51.100.10"),
                DataKey = "verified-address",
                Ttl = 120
            }
        });

        var instance = Assert.Single(aggregate.Build());
        Assert.Equal("Verified", instance.Name);
        Assert.Equal("verified.local", instance.HostName);
        Assert.Equal(IPAddress.Parse("198.51.100.10"), Assert.Single(instance.Addresses));
        Assert.Equal(128, aggregate.ServiceCount);
        Assert.Equal(128, aggregate.TextOwnerCount);
        Assert.Equal(128, aggregate.AddressHostCount);
    }

    [Fact]
    public void DnsSdVerifiedHostsEvictPendingAddressesWhenServiceOwnersAreNotFull()
    {
        var aggregate = new DnsDiscoveryAggregate(clock: () => TimeSpan.Zero);
        for (var owner = 0; owner < 16; owner++)
        {
            var updates = new List<DnsDiscoveryUpdate>();
            for (var record = 0; record < 8; record++)
            {
                var host = $"pending-{owner}-{record}.local";
                updates.Add(new DnsDiscoveryUpdate
                {
                    Kind = DnsDiscoveryRecordKind.Srv,
                    Name = $"Pending-{owner}._home-assistant._tcp.local",
                    Host = host,
                    Port = 8123,
                    DataKey = $"service-{owner}-{record}",
                    Ttl = 120
                });
                updates.Add(new DnsDiscoveryUpdate
                {
                    Kind = DnsDiscoveryRecordKind.A,
                    Name = host,
                    Address = IPAddress.Parse($"192.0.{owner}.{record + 1}"),
                    DataKey = $"address-{owner}-{record}",
                    Ttl = 120
                });
            }
            aggregate.ApplyPacket(updates);
        }
        Assert.Equal(16, aggregate.ServiceCount);
        Assert.Equal(128, aggregate.AddressHostCount);

        aggregate.ApplyPacket(new[]
        {
            new DnsDiscoveryUpdate
            {
                Kind = DnsDiscoveryRecordKind.Ptr,
                Name = "_home-assistant._tcp.local",
                Target = "Verified._home-assistant._tcp.local",
                DataKey = "VERIFIED._HOME-ASSISTANT._TCP.LOCAL",
                Ttl = 120
            },
            new DnsDiscoveryUpdate
            {
                Kind = DnsDiscoveryRecordKind.Srv,
                Name = "Verified._home-assistant._tcp.local",
                Host = "verified.local",
                Port = 8123,
                DataKey = "verified-service",
                Ttl = 120
            },
            new DnsDiscoveryUpdate
            {
                Kind = DnsDiscoveryRecordKind.A,
                Name = "verified.local",
                Address = IPAddress.Parse("198.51.100.20"),
                DataKey = "verified-address",
                Ttl = 120
            }
        });

        var instance = Assert.Single(aggregate.Build());
        Assert.Equal("verified.local", instance.HostName);
        Assert.Equal(IPAddress.Parse("198.51.100.20"), Assert.Single(instance.Addresses));
        Assert.Equal(17, aggregate.ServiceCount);
        Assert.Equal(128, aggregate.AddressHostCount);
    }

    [Fact]
    public async Task DnsSdDiscoveryNormalizesSendTimeoutAndPreservesCallerCancellation()
    {
        var address = IPAddress.Parse("192.0.2.10");
        var timeoutClient = new HomeAssistantDiscoveryClient(
            new TestDiscoveryTransportFactory(new[] { address }, CreateDiscoveryPacket(), blockSend: true));
        Assert.Empty(await timeoutClient.DiscoverAsync(TimeSpan.FromMilliseconds(50)));

        var canceledClient = new HomeAssistantDiscoveryClient(
            new TestDiscoveryTransportFactory(new[] { address }, CreateDiscoveryPacket(), blockSend: true));
        using var source = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            canceledClient.DiscoverAsync(TimeSpan.FromSeconds(5), source.Token));
    }

    [Fact]
    public void DnsSdInterfaceEnumerationRetainsHealthyAdaptersWhenOneDisappears()
    {
        var healthy = IPAddress.Parse("192.0.2.10");

        var addresses = UdpHomeAssistantDiscoveryTransportFactory.CollectLocalAddresses(
            new Func<IReadOnlyList<IPAddress>>[]
            {
                () => throw new NetworkInformationException(5),
                () => new[] { healthy }
            });

        Assert.Equal(healthy, Assert.Single(addresses));
    }

    [Fact]
    public void DnsSdAddressBudgetRetainsEveryEligibleInterfaceBeforeAliases()
    {
        var aliases = Enumerable.Range(1, 32).Select(value => IPAddress.Parse("192.0.2." + value)).ToArray();
        var tunnel = IPAddress.Parse("198.51.100.20");
        var interfaces = UdpHomeAssistantDiscoveryTransportFactory.CollectLocalInterfaces(
            new Func<HomeAssistantDiscoveryInterface?>[]
            {
                () => new HomeAssistantDiscoveryInterface("ethernet", aliases, 7),
                () => new HomeAssistantDiscoveryInterface("tunnel", new[] { tunnel }, 8)
            });

        Assert.Equal(32, interfaces.Sum(value => value.Addresses.Count));
        Assert.Contains(interfaces, value => value.Id == "tunnel" && value.InterfaceIndex == 8 && value.Addresses.Contains(tunnel));
    }

    [Fact]
    public async Task DnsSdAliasesShareOneInterfaceTransportAndDatagramBudget()
    {
        var aliases = new[] { IPAddress.Parse("192.0.2.10"), IPAddress.Parse("192.0.2.11") };
        var factory = new TestDiscoveryTransportFactory(
            aliases,
            CreateDiscoveryPacket(),
            singleInterface: true);
        var client = new HomeAssistantDiscoveryClient(factory);

        var results = await client.DiscoverAsync(TimeSpan.FromMilliseconds(100));

        Assert.Single(results);
        Assert.Equal(1, factory.CreateCount);
        Assert.Equal(aliases.OrderBy(value => value.ToString()), factory.SentAddresses.OrderBy(value => value.ToString()));
    }

    [Fact]
    public async Task MobileAppRegistrationAndWebhookAreTypedAndEncryptionFailsClosed()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var request = RegistrationRequest(false);
        request.AdditionalData["future_registration_field"] = "preserved-request";
        var registration = await client.MobileApp.RegisterAsync(request);

        Assert.Equal("test-webhook", registration.WebhookId);
        Assert.DoesNotContain(registration.WebhookId, registration.ToString(), StringComparison.Ordinal);
        Assert.Equal("preserved", registration.AdditionalData["future_field"].GetString());
        Assert.Null(registration.Secret);
        using (var registrationBody = JsonDocument.Parse(Assert.IsType<string>(server.LastRequestBody)))
        {
            Assert.Equal("com.example.app", registrationBody.RootElement.GetProperty("app_id").GetString());
            Assert.Equal("Windows", registrationBody.RootElement.GetProperty("os_name").GetString());
            Assert.False(registrationBody.RootElement.GetProperty("supports_encryption").GetBoolean());
            Assert.Equal(
                "preserved-request",
                registrationBody.RootElement.GetProperty("future_registration_field").GetString());
        }
        using (var webhook = client.MobileApp.CreateWebhookClient(registration))
        {
            var config = await webhook.GetConfigurationAsync();
            Assert.Equal("2026.8.3", config.GetProperty("version").GetString());
            Assert.True(string.IsNullOrEmpty(server.LastAuthorization));

            var updateAppData = new Dictionary<string, object?> { ["push_token"] = "updated", ["attempts"] = 1 };
            var registrationUpdate = new HomeAssistantMobileAppRegistrationUpdate(
                "1.0", "CasaRay", "Evotec", "Windows")
            {
                OperatingSystemVersion = "11.0",
                AppData = updateAppData
            };
            await webhook.UpdateRegistrationAsync(registrationUpdate);
            Assert.Same(updateAppData, registrationUpdate.AppData);
            Assert.IsType<int>(registrationUpdate.AppData["attempts"]);
            using var updateBody = JsonDocument.Parse(Assert.IsType<string>(server.LastRequestBody));
            Assert.Equal("update_registration", updateBody.RootElement.GetProperty("type").GetString());
            var updateData = updateBody.RootElement.GetProperty("data");
            Assert.Equal("11.0", updateData.GetProperty("os_version").GetString());
            Assert.Equal("updated", updateData.GetProperty("app_data").GetProperty("push_token").GetString());
            Assert.Equal("1.0", updateData.GetProperty("app_version").GetString());
            Assert.Equal("CasaRay", updateData.GetProperty("device_name").GetString());
            Assert.Equal("Evotec", updateData.GetProperty("manufacturer").GetString());
            Assert.Equal("Windows", updateData.GetProperty("model").GetString());
        }

        var encrypted = await client.MobileApp.RegisterAsync(RegistrationRequest(true));
        Assert.NotNull(encrypted.Secret);
        Assert.DoesNotContain(encrypted.Secret, encrypted.ToString(), StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => client.MobileApp.CreateWebhookClient(encrypted));
        using var protectedWebhook = client.MobileApp.CreateWebhookClient(encrypted, new TestPayloadProtector());
        var protectedConfig = await protectedWebhook.GetConfigurationAsync();
        Assert.Equal("2026.8.3", protectedConfig.GetProperty("version").GetString());
        using var encryptedBody = JsonDocument.Parse(Assert.IsType<string>(server.LastRequestBody));
        Assert.True(encryptedBody.RootElement.GetProperty("encrypted").GetBoolean());
        Assert.Equal("get_config", encryptedBody.RootElement.GetProperty("type").GetString());
        Assert.False(encryptedBody.RootElement.TryGetProperty("data", out _));
        var plaintext = Convert.FromBase64String(encryptedBody.RootElement.GetProperty("encrypted_data").GetString()!);
        using var plaintextBody = JsonDocument.Parse(plaintext);
        Assert.Equal(JsonValueKind.Object, plaintextBody.RootElement.ValueKind);
        Assert.Empty(plaintextBody.RootElement.EnumerateObject());

        await protectedWebhook.UpdateRegistrationAsync(new HomeAssistantMobileAppRegistrationUpdate(
            "1.0", "CasaRay", "Evotec", "Windows")
        {
            OperatingSystemVersion = "12.0",
            AppData = new Dictionary<string, object?> { ["push_token"] = "encrypted-update" }
        });
        using var encryptedUpdateBody = JsonDocument.Parse(Assert.IsType<string>(server.LastRequestBody));
        Assert.Equal("update_registration", encryptedUpdateBody.RootElement.GetProperty("type").GetString());
        var updatePlaintext = Convert.FromBase64String(encryptedUpdateBody.RootElement.GetProperty("encrypted_data").GetString()!);
        using var updatePlaintextBody = JsonDocument.Parse(updatePlaintext);
        Assert.Equal("12.0", updatePlaintextBody.RootElement.GetProperty("os_version").GetString());
        Assert.Equal("encrypted-update", updatePlaintextBody.RootElement.GetProperty("app_data").GetProperty("push_token").GetString());
        Assert.False(updatePlaintextBody.RootElement.TryGetProperty("type", out _));
        Assert.False(updatePlaintextBody.RootElement.TryGetProperty("data", out _));

        Assert.Throws<ArgumentException>(() => new HomeAssistantMobileAppRegistrationUpdate(
            " ", "CasaRay", "Evotec", "Windows"));
    }

    [Fact]
    public async Task MobileAppRegistrationRejectsNullAppDataBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var request = RegistrationRequest(false);
        request.AppData = null!;

        await Assert.ThrowsAsync<ArgumentNullException>(() => client.MobileApp.RegisterAsync(request));

        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task MobileAppRegistrationRejectsInvalidAdditionalDataBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var request = RegistrationRequest(false);
        request.AdditionalData = null!;
        await Assert.ThrowsAsync<ArgumentNullException>(() => client.MobileApp.RegisterAsync(request));

        request = RegistrationRequest(false);
        request.AdditionalData["APP_ID"] = "replacement";
        await Assert.ThrowsAsync<ArgumentException>(() => client.MobileApp.RegisterAsync(request));

        request = RegistrationRequest(false);
        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;
        request.AdditionalData["future"] = cyclic;
        await Assert.ThrowsAsync<ArgumentException>(() => client.MobileApp.RegisterAsync(request));
        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task MobileAppRegistrationAllowsHomeAssistantsOptionalOperatingSystemVersion()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var request = RegistrationRequest(false);
        request.OperatingSystemVersion = null;

        _ = await client.MobileApp.RegisterAsync(request);

        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastRequestBody));
        Assert.False(body.RootElement.TryGetProperty("os_version", out _));
    }

    [Theory]
    [InlineData("secret")]
    [InlineData("")]
    public async Task MobileAppRegistrationRejectsSecretsWhenEncryptionWasNotRequested(string secret)
    {
        using var server = new TestHomeAssistantServer
        {
            MobileRegistrationResponseJson = "{\"webhook_id\":\"test-webhook\",\"secret\":\"" + secret + "\"}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.MobileApp.RegisterAsync(RegistrationRequest(false)));
    }

    [Fact]
    public async Task MobileAppRegistrationFreezesAndRejectsInvalidAppDataBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var request = RegistrationRequest(false);
        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;
        request.AppData = cyclic;

        await Assert.ThrowsAsync<ArgumentException>(() => client.MobileApp.RegisterAsync(request));
        Assert.Null(server.LastRequestBody);

        var tokenProvider = new BlockingTokenProvider();
        using var delayedClient = TestClientFactory.Create(server, accessTokenProvider: tokenProvider);
        request = RegistrationRequest(false);
        var appData = new Dictionary<string, object?> { ["push_token"] = "original" };
        request.AppData = appData;
        request.AdditionalData["future_registration_field"] = "original";
        var registration = delayedClient.MobileApp.RegisterAsync(request);
        await tokenProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        appData["push_token"] = "changed";
        request.AppData = new Dictionary<string, object?> { ["push_token"] = "replacement" };
        request.AdditionalData["future_registration_field"] = "changed";
        request.AppName = "Changed app";
        tokenProvider.Release.TrySetResult(TestHomeAssistantServer.AccessToken);
        _ = await registration;

        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastRequestBody));
        Assert.Equal("original", body.RootElement.GetProperty("app_data").GetProperty("push_token").GetString());
        Assert.Equal("original", body.RootElement.GetProperty("future_registration_field").GetString());
        Assert.Equal("Example", body.RootElement.GetProperty("app_name").GetString());

        server.MobileRegistrationResponseJson = "{\"webhook_id\":\"test-webhook\",\"secret\":null}";
        var encryptionTokenProvider = new BlockingTokenProvider();
        using var encryptionClient = TestClientFactory.Create(server, accessTokenProvider: encryptionTokenProvider);
        request = RegistrationRequest(true);
        var encryptedRegistration = encryptionClient.MobileApp.RegisterAsync(request);
        await encryptionTokenProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        request.SupportsEncryption = false;
        encryptionTokenProvider.Release.TrySetResult(TestHomeAssistantServer.AccessToken);
        await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(() => encryptedRegistration);
    }

    [Fact]
    public async Task MobileAppRegistrationClassifiesUndefinedJsonAppDataBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var request = RegistrationRequest(false);
        request.AppData = new Dictionary<string, object?> { ["undefined"] = default(JsonElement) };

        await Assert.ThrowsAsync<ArgumentException>(() => client.MobileApp.RegisterAsync(request));
        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task VacuumCommandFreezesAndRejectsProviderParametersBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Vacuums.SendCommandAsync(
            HomeAssistantTarget.ForEntity("vacuum.downstairs"), "provider_command", cyclic));
        Assert.Null(server.LastServiceCallBody);

        var tokenProvider = new BlockingTokenProvider();
        using var delayedClient = TestClientFactory.Create(server, accessTokenProvider: tokenProvider);
        var parameters = new Dictionary<string, object?> { ["zone"] = "original" };
        var operation = delayedClient.Controls.Vacuums.SendCommandAsync(
            HomeAssistantTarget.ForEntity("vacuum.downstairs"), "provider_command", parameters);
        await tokenProvider.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        parameters["zone"] = "changed";
        tokenProvider.Release.TrySetResult(TestHomeAssistantServer.AccessToken);
        await operation;

        using var call = LastCall(server);
        Assert.Equal("original", call.RootElement.GetProperty("service_data").GetProperty("params").GetProperty("zone").GetString());
    }

    [Fact]
    public async Task RawWebhookPayloadsFailBeforeTransport()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(new HomeAssistantMobileAppRegistration { WebhookId = "undefined" });
        await Assert.ThrowsAsync<ArgumentException>(() => webhook.SendAsync("custom", default(JsonElement)));
        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task CancelledWebhookCommandsDoNotInspectCallerOwnedPayloads()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(new HomeAssistantMobileAppRegistration { WebhookId = "undefined" });
        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            webhook.SendAsync("custom", cyclic, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            webhook.UpdateRegistrationAsync(new HomeAssistantMobileAppRegistrationUpdate(
                "1.0", "CasaRay", "Evotec", "Windows")
            {
                AppData = cyclic
            }, cancellation.Token));
        var registration = RegistrationRequest(false);
        registration.AppData = cyclic;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.MobileApp.RegisterAsync(registration, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.Vacuums.SendCommandAsync(
                HomeAssistantTarget.ForEntity("vacuum.downstairs"),
                "custom",
                cyclic,
                cancellation.Token));

        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task MobileRegistrationStopsCallerPayloadTraversalAfterCancellation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        var values = new CancellationProbeEnumerable(cancellation);
        var registration = RegistrationRequest(false);
        registration.AppData = new Dictionary<string, object?> { ["values"] = values };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.MobileApp.RegisterAsync(registration, cancellation.Token));

        Assert.InRange(values.ReadCount, 1, 16);
        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task MobileRegistrationStopsAdditionalFieldTraversalBeforeHashing()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registration = RegistrationRequest(false);
        registration.AdditionalData["future_" + new string('a', 16_000_000)] = true;
        var operation = Task.Run(async () =>
        {
            started.TrySetResult(true);
            await client.MobileApp.RegisterAsync(registration, cancellation.Token);
        });
        await started.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
        Assert.Null(server.LastRequestBody);
    }

    [Theory]
    [InlineData("cloudhook_url", "ftp://example.invalid/webhook")]
    [InlineData("remote_ui_url", "file:///private/home-assistant")]
    [InlineData("cloudhook_url", "https://user:password@example.invalid/webhook")]
    [InlineData("remote_ui_url", "https://user@example.invalid/")]
    public async Task MobileAppRegistrationClassifiesInvalidReturnedUris(string field, string value)
    {
        using var server = new TestHomeAssistantServer
        {
            MobileRegistrationResponseJson = "{\"webhook_id\":\"test-webhook\",\"secret\":null,\"cloudhook_url\":null,\"remote_ui_url\":null,\"" + field + "\":\"" + value + "\"}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(() =>
            client.MobileApp.RegisterAsync(RegistrationRequest(false)));
    }

    [Fact]
    public async Task MobileAppWebhookHonorsConnectionTimeoutAndResponseLimit()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        client.Options.RequestTimeout = TimeSpan.FromMilliseconds(100);
        client.Options.MaximumRestResponseBytes = 1024;
        using var stalled = client.MobileApp.CreateWebhookClient(new HomeAssistantMobileAppRegistration { WebhookId = "stall" });
        using var oversized = client.MobileApp.CreateWebhookClient(new HomeAssistantMobileAppRegistration { WebhookId = "oversize" });

        var timeout = await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantConnectionException>(() => stalled.GetConfigurationAsync());
        Assert.IsType<TimeoutException>(timeout.InnerException);
        await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(() => oversized.GetConfigurationAsync());
    }

    [Fact]
    public async Task ListControlPayloadsHonorCancellationBeforeValidation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var values = new[] { "one", " " };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Controls.Vacuums.CleanAreaAsync(
            HomeAssistantTarget.ForEntity("vacuum.house"),
            values,
            cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Controls.Helpers.SetSelectOptionsAsync(
            HomeAssistantTarget.ForEntity("input_select.mode"),
            values,
            cancellation.Token));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task MobileAppWebhookUsesOnlyAnOwnedCredentialFreeTransport()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var factoryMethods = typeof(HomeAssistantMobileAppClient).GetMethods()
            .Where(method => method.Name == nameof(HomeAssistantMobileAppClient.CreateWebhookClient))
            .ToArray();
        var factoryMethod = Assert.Single(factoryMethods, method => method.GetParameters().Length == 2);
        Assert.DoesNotContain(factoryMethod.GetParameters(), parameter =>
            parameter.ParameterType == typeof(HttpClient)
            || typeof(HttpMessageHandler).IsAssignableFrom(parameter.ParameterType));
        Assert.Contains(factoryMethods, method => method.GetParameters().Any(parameter =>
            parameter.ParameterType == typeof(Func<HttpMessageHandler>)));

        var registration = new HomeAssistantMobileAppRegistration
        {
            WebhookId = "test-webhook",
            CloudhookUri = new Uri(server.BaseUri, "api/webhook/test-webhook")
        };

        using var webhook = client.MobileApp.CreateWebhookClient(registration);
        var transportField = typeof(HomeAssistantMobileAppWebhookClient).GetField(
            "_httpClient",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var ownedTransport = Assert.IsType<HttpClient>(transportField!.GetValue(webhook));
        Assert.Equal(Timeout.InfiniteTimeSpan, ownedTransport.Timeout);
        await webhook.GetConfigurationAsync();
        Assert.True(string.IsNullOrEmpty(server.LastAuthorization));

        var customHandler = new TrackingHttpMessageHandler(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseDefaultCredentials = false,
            UseCookies = false,
            UseProxy = false
        });
        using (var customizedWebhook = client.MobileApp.CreateWebhookClient(
                   registration,
                   protector: null,
                   () => customHandler))
        {
            await customizedWebhook.GetConfigurationAsync();
        }
        Assert.Equal(1, customHandler.SendCount);

        var credentialed = new HomeAssistantMobileAppRegistration
        {
            WebhookId = "credentialed-webhook",
            CloudhookUri = new Uri("https://user:password@example.invalid/webhook")
        };
        Assert.Throws<ArgumentException>(() => client.MobileApp.CreateWebhookClient(credentialed));

        foreach (var invalidSecret in new[] { string.Empty, "   " })
        {
            var invalidRegistration = new HomeAssistantMobileAppRegistration
            {
                WebhookId = "invalid-secret",
                Secret = invalidSecret
            };
            Assert.Throws<ArgumentException>(() =>
                client.MobileApp.CreateWebhookClient(invalidRegistration, new TestPayloadProtector()));
        }
    }

    [Fact]
    public async Task MobileAppWebhookDoesNotFollowCrossOriginRedirects()
    {
        using var destination = new TestHomeAssistantServer();
        using var redirector = new TestHomeAssistantServer
        {
            WebhookRedirectUri = new Uri(destination.BaseUri, "api/webhook/test-webhook")
        };
        using var client = TestClientFactory.Create(redirector);
        using var webhook = client.MobileApp.CreateWebhookClient(new HomeAssistantMobileAppRegistration
        {
            WebhookId = "redirect",
            CloudhookUri = new Uri(redirector.BaseUri, "api/webhook/redirect")
        });

        var error = await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantCommandException>(
            () => webhook.UpdateRegistrationAsync(new HomeAssistantMobileAppRegistrationUpdate(
                "1.0", "CasaRay", "Evotec", "Windows")
            {
                AppData = new Dictionary<string, object?> { ["push_token"] = "sensitive" }
            }));

        Assert.Equal("http_307", error.Code);
        Assert.Null(destination.LastRequestPath);
    }

    [Fact]
    public async Task EncryptedMobileAppWebhookRejectsPlaintextResponses()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "plaintext-encrypted",
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/plaintext-encrypted"),
                Secret = "test-secret"
            },
            new TestPayloadProtector());

        await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(
            () => webhook.GetConfigurationAsync());

        using var invalidEncrypted = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "invalid-encrypted",
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/invalid-encrypted"),
                Secret = "test-secret"
            },
            new TestPayloadProtector());
        await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(
            () => invalidEncrypted.GetConfigurationAsync());
    }

    [Fact]
    public async Task EncryptedMobileAppWebhookClassifiesDecryptionFailures()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var failure = new FormatException("Invalid protected payload.");
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "test-webhook",
                Secret = "test-secret"
            },
            new ThrowingUnprotectPayloadProtector(failure));

        var exception = await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(
            () => webhook.GetConfigurationAsync());
        Assert.Same(failure, exception.InnerException);
    }

    [Fact]
    public async Task MobileAppWebhookClassifiesTruncatedResponseReads()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "truncated",
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/truncated")
            });

        await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantConnectionException>(
            () => webhook.GetConfigurationAsync());
    }

    [Fact]
    public async Task MobileAppWebhookClassifiesTypedCameraDecodeFailures()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "invalid-camera-response",
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/invalid-camera-response")
            });

        var exception = await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(
            () => webhook.GetCameraStreamAsync("camera.front"));

        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Theory]
    [InlineData("incomplete-camera-response")]
    [InlineData("failed-camera-response")]
    public async Task MobileAppWebhookRejectsCameraResponsesWithoutAUsableStream(string webhookId)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = webhookId,
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/" + webhookId)
            });

        await Assert.ThrowsAsync<HomeAssistantX.Exceptions.HomeAssistantProtocolException>(
            () => webhook.GetCameraStreamAsync("camera.front"));
    }

    [Fact]
    public async Task MobileAppWebhookAcceptsAnHlsOnlyCameraStream()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "hls-only-camera-response",
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/hls-only-camera-response")
            });

        var stream = await webhook.GetCameraStreamAsync("camera.front");

        Assert.Null(stream.MjpegPath);
        Assert.Equal("/api/hls/front/master_playlist.m3u8", stream.HlsPath);
        Assert.Null(stream.Success);
    }

    [Fact]
    public async Task MobileAppWebhookAcceptsTheNativeMjpegResponseWithoutASuccessFlag()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "mjpeg-only-camera-response",
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/mjpeg-only-camera-response")
            });

        var stream = await webhook.GetCameraStreamAsync("camera.front");

        Assert.Equal("/api/camera_proxy_stream/camera.front", stream.MjpegPath);
        Assert.Null(stream.HlsPath);
        Assert.Null(stream.Success);
    }

    [Fact]
    public async Task MobileAppWebhookHonorsCancellationBeforeSerializingTheOutboundEnvelope()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "cancel-envelope",
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/cancel-envelope"),
                Secret = "test-secret"
            },
            new CancelAfterProtectPayloadProtector(cancellation));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            webhook.SendAsync("test", new Dictionary<string, object?> { ["value"] = "payload" }, cancellation.Token));

        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task MobileAppWebhookCommandValidationObservesCancellationDuringNormalization()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var webhook = client.MobileApp.CreateWebhookClient(
            new HomeAssistantMobileAppRegistration
            {
                WebhookId = "cancel-command",
                CloudhookUri = new Uri(server.BaseUri, "api/webhook/cancel-command")
            });
        using var cancellation = new CancellationTokenSource();
        var command = new string(' ', 16_000_000);
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Task.Factory.StartNew(
            async () =>
            {
                started.TrySetResult(true);
                await webhook.SendAsync(command, null, cancellation.Token);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

        await started.Task;
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task MobileAppWebhookResponseParsingObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HomeAssistantMobileAppWebhookClient.ParseResponseAsync(
                Encoding.UTF8.GetBytes("{\"value\":[1,2,3]}"),
                cancellation.Token));
    }

    [Fact]
    public async Task HelperTimesRejectFractionalSecondsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Helpers.SetTimeAsync(
            HomeAssistantHelperDomain.Time,
            HomeAssistantX.Services.HomeAssistantTarget.ForEntity("time.wakeup"),
            TimeSpan.FromMilliseconds(500)));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task MobileAppWebhookPreservesPayloadProtectorFailureProvenance()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        foreach (var failure in new Exception[]
                 {
                     new IOException("Protector storage failed."),
                     new HttpRequestException("Protector network failed."),
                     new JsonException("Protector JSON failed."),
                     new OperationCanceledException("Protector canceled independently.")
                 })
        {
            using var webhook = client.MobileApp.CreateWebhookClient(
                new HomeAssistantMobileAppRegistration
                {
                    WebhookId = "test-webhook",
                    Secret = "test-secret"
                },
                new ThrowingPayloadProtector(failure));

            var actual = await Record.ExceptionAsync(() => webhook.GetConfigurationAsync());
            Assert.NotNull(actual);
            Assert.Equal(failure.GetType(), actual.GetType());
        }
    }

    private static HomeAssistantMobileAppRegistrationRequest RegistrationRequest(bool encryption) => new()
    {
        AppId = "com.example.app",
        AppName = "Example",
        AppVersion = "1.0",
        DeviceName = "Test device",
        Manufacturer = "Example",
        Model = "Test",
        OperatingSystemName = "Windows",
        OperatingSystemVersion = "11.0",
        SupportsEncryption = encryption
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

    private static JsonDocument LastCall(TestHomeAssistantServer server) => JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));

    private static void AssertCall(TestHomeAssistantServer server, string domain, string service)
    {
        using var call = LastCall(server);
        Assert.Equal(domain, call.RootElement.GetProperty("domain").GetString());
        Assert.Equal(service, call.RootElement.GetProperty("service").GetString());
    }

    private static void AssertCall<T>(TestHomeAssistantServer server, string domain, string service, string field, T value)
    {
        using var call = LastCall(server);
        Assert.Equal(domain, call.RootElement.GetProperty("domain").GetString());
        Assert.Equal(service, call.RootElement.GetProperty("service").GetString());
        var actual = call.RootElement.GetProperty("service_data").GetProperty(field);
        if (value is double number) Assert.Equal(number, actual.GetDouble());
        else Assert.Equal(value?.ToString(), actual.GetString());
    }

    private static byte[] CreateDiscoveryPacket(uint ttl = 120, int recordClass = 1, byte addressLastOctet = 10, string host = "ha.local", string[]? textItems = null, uint? additionalTtl = null, int srvPriority = 0, int srvWeight = 0)
    {
        using var stream = new MemoryStream();
        U16(stream, 0); U16(stream, 0x8400); U16(stream, 1); U16(stream, 4); U16(stream, 0); U16(stream, 0);
        Name(stream, "_home-assistant._tcp.local");
        var serviceOffset = 12;
        U16(stream, 12); U16(stream, 1);

        Pointer(stream, serviceOffset); U16(stream, 12); U16(stream, recordClass); U32(stream, ttl); U16(stream, 7); Label(stream, "Test"); Pointer(stream, serviceOffset);
        var recordTtl = additionalTtl ?? ttl;
        Label(stream, "Test"); Pointer(stream, serviceOffset); U16(stream, 33); U16(stream, recordClass); U32(stream, recordTtl);
        using (var data = new MemoryStream()) { U16(data, srvPriority); U16(data, srvWeight); U16(data, 8123); Name(data, host); WriteData(stream, data.ToArray()); }
        Label(stream, "Test"); Pointer(stream, serviceOffset); U16(stream, 16); U16(stream, recordClass); U32(stream, recordTtl);
        var text = textItems ?? new[] { "location_name=My Home", "uuid=test-uuid", "version=2026.8.3", "internal_url=http://ha.local:8123/", "external_url=file:///unsafe" };
        using (var data = new MemoryStream()) { foreach (var item in text) { var bytes = Encoding.UTF8.GetBytes(item); data.WriteByte((byte)bytes.Length); data.Write(bytes); } WriteData(stream, data.ToArray()); }
        Name(stream, host); U16(stream, 1); U16(stream, recordClass); U32(stream, recordTtl); U16(stream, 4); stream.Write(new byte[] { 192, 0, 2, addressLastOctet });
        return stream.ToArray();
    }

    private static byte[] CreateSrvOnlyPacket(uint ttl, int priority, int weight, string host, int port, bool cacheFlush = false)
    {
        using var stream = new MemoryStream();
        U16(stream, 0); U16(stream, 0x8400); U16(stream, 0); U16(stream, 1); U16(stream, 0); U16(stream, 0);
        Name(stream, "Test._home-assistant._tcp.local"); U16(stream, 33); U16(stream, cacheFlush ? 0x8001 : 1); U32(stream, ttl);
        using var data = new MemoryStream();
        U16(data, priority); U16(data, weight); U16(data, port);
        if (host == ".") data.WriteByte(0); else Name(data, host);
        WriteData(stream, data.ToArray());
        return stream.ToArray();
    }

    private static byte[] CreatePtrOnlyPacket(uint ttl)
    {
        using var stream = new MemoryStream();
        U16(stream, 0); U16(stream, 0x8400); U16(stream, 0); U16(stream, 1); U16(stream, 0); U16(stream, 0);
        Name(stream, "_home-assistant._tcp.local"); U16(stream, 12); U16(stream, 1); U32(stream, ttl);
        using var data = new MemoryStream();
        Name(data, "Test._home-assistant._tcp.local");
        WriteData(stream, data.ToArray());
        return stream.ToArray();
    }

    private static byte[] CreateTxtOnlyPacket(uint ttl, string[] textItems, bool cacheFlush = false)
    {
        using var stream = new MemoryStream();
        U16(stream, 0); U16(stream, 0x8400); U16(stream, 0); U16(stream, 1); U16(stream, 0); U16(stream, 0);
        Name(stream, "Test._home-assistant._tcp.local"); U16(stream, 16); U16(stream, cacheFlush ? 0x8001 : 1); U32(stream, ttl);
        using var data = new MemoryStream();
        foreach (var item in textItems) { var bytes = Encoding.UTF8.GetBytes(item); data.WriteByte((byte)bytes.Length); data.Write(bytes); }
        WriteData(stream, data.ToArray());
        return stream.ToArray();
    }

    private static byte[] CreateAddressOnlyPacket(uint ttl, IPAddress address, bool cacheFlush = false)
    {
        using var stream = new MemoryStream();
        U16(stream, 0); U16(stream, 0x8400); U16(stream, 0); U16(stream, 1); U16(stream, 0); U16(stream, 0);
        Name(stream, "ha.local"); U16(stream, 1); U16(stream, cacheFlush ? 0x8001 : 1); U32(stream, ttl); U16(stream, 4); stream.Write(address.GetAddressBytes());
        return stream.ToArray();
    }

    private static byte[] AppendAaaaRecord(byte[] packet, string host, IPAddress address)
    {
        var updated = (byte[])packet.Clone();
        updated[7]++;
        using var stream = new MemoryStream();
        stream.Write(updated);
        Name(stream, host); U16(stream, 28); U16(stream, 1); U32(stream, 120); U16(stream, 16); stream.Write(address.GetAddressBytes());
        return stream.ToArray();
    }

    private static byte[] AppendMalformedRecord(byte[] packet, int recordType, int dataLength)
    {
        packet[7]++;
        using var stream = new MemoryStream();
        stream.Write(packet);
        Name(stream, "bad.local"); U16(stream, recordType); U16(stream, 1); U32(stream, 120); U16(stream, dataLength);
        stream.Write(new byte[dataLength]);
        return stream.ToArray();
    }

    private static byte[] CreateUnrelatedDiscoveryPacket()
    {
        using var stream = new MemoryStream();
        U16(stream, 0); U16(stream, 0x8400); U16(stream, 0); U16(stream, 3); U16(stream, 0); U16(stream, 0);
        Name(stream, "Printer._ipp._tcp.local"); U16(stream, 33); U16(stream, 1); U32(stream, 120);
        using (var data = new MemoryStream()) { U16(data, 0); U16(data, 0); U16(data, 631); Name(data, "printer.local"); WriteData(stream, data.ToArray()); }
        Name(stream, "Printer._ipp._tcp.local"); U16(stream, 16); U16(stream, 1); U32(stream, 120);
        using (var data = new MemoryStream()) { var bytes = Encoding.UTF8.GetBytes("note=not Home Assistant"); data.WriteByte((byte)bytes.Length); data.Write(bytes); WriteData(stream, data.ToArray()); }
        Name(stream, "printer.local"); U16(stream, 1); U16(stream, 1); U32(stream, 120); U16(stream, 4); stream.Write(new byte[] { 192, 0, 2, 50 });
        return stream.ToArray();
    }

    private static byte[] CreateOversizedDiscoveryNamePacket()
    {
        using var stream = new MemoryStream();
        U16(stream, 0); U16(stream, 0x8400); U16(stream, 0); U16(stream, 1); U16(stream, 0); U16(stream, 0);
        Name(stream, "_home-assistant._tcp.local"); U16(stream, 12); U16(stream, 1); U32(stream, 120);
        using var data = new MemoryStream();
        for (var index = 0; index < 5; index++) Label(data, new string((char)('a' + index), 63));
        data.WriteByte(0);
        WriteData(stream, data.ToArray());
        return stream.ToArray();
    }

    private static void WriteData(Stream stream, byte[] data) { U16(stream, data.Length); stream.Write(data); }
    private static void Name(Stream stream, string name) { foreach (var label in name.Split('.')) Label(stream, label); stream.WriteByte(0); }
    private static void Label(Stream stream, string label) { var bytes = Encoding.UTF8.GetBytes(label); stream.WriteByte((byte)bytes.Length); stream.Write(bytes); }
    private static void Pointer(Stream stream, int offset) { stream.WriteByte((byte)(0xC0 | (offset >> 8))); stream.WriteByte((byte)offset); }
    private static void U16(Stream stream, int value) { stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }
    private static void U32(Stream stream, uint value) { stream.WriteByte((byte)(value >> 24)); stream.WriteByte((byte)(value >> 16)); stream.WriteByte((byte)(value >> 8)); stream.WriteByte((byte)value); }

    private sealed class TestPayloadProtector : IHomeAssistantMobileAppPayloadProtector
    {
        public Task<string> ProtectAsync(byte[] plaintextJson, string secret, CancellationToken cancellationToken = default)
            => Task.FromResult(Convert.ToBase64String(plaintextJson));

        public Task<byte[]> UnprotectAsync(string protectedPayload, string secret, CancellationToken cancellationToken = default)
            => Task.FromResult(Convert.FromBase64String(protectedPayload));
    }

    private sealed class ThrowingPayloadProtector : IHomeAssistantMobileAppPayloadProtector
    {
        private readonly Exception _exception;

        internal ThrowingPayloadProtector(Exception exception)
        {
            _exception = exception;
        }

        public Task<string> ProtectAsync(byte[] plaintextJson, string secret, CancellationToken cancellationToken = default)
            => Task.FromException<string>(_exception);

        public Task<byte[]> UnprotectAsync(string protectedPayload, string secret, CancellationToken cancellationToken = default)
            => Task.FromException<byte[]>(_exception);
    }

    private sealed class ThrowingUnprotectPayloadProtector : IHomeAssistantMobileAppPayloadProtector
    {
        private readonly Exception _exception;

        internal ThrowingUnprotectPayloadProtector(Exception exception)
        {
            _exception = exception;
        }

        public Task<string> ProtectAsync(byte[] plaintextJson, string secret, CancellationToken cancellationToken = default)
            => Task.FromResult(Convert.ToBase64String(plaintextJson));

        public Task<byte[]> UnprotectAsync(string protectedPayload, string secret, CancellationToken cancellationToken = default)
            => Task.FromException<byte[]>(_exception);
    }

    private sealed class TrackingHttpMessageHandler : DelegatingHandler
    {
        internal TrackingHttpMessageHandler(HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
        }

        internal int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return base.SendAsync(request, cancellationToken);
        }
    }

    private sealed class CancelAfterProtectPayloadProtector : IHomeAssistantMobileAppPayloadProtector
    {
        private readonly CancellationTokenSource _cancellation;

        internal CancelAfterProtectPayloadProtector(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        public Task<string> ProtectAsync(byte[] plaintextJson, string secret, CancellationToken cancellationToken = default)
        {
            _cancellation.Cancel();
            return Task.FromResult(Convert.ToBase64String(plaintextJson));
        }

        public Task<byte[]> UnprotectAsync(string protectedPayload, string secret, CancellationToken cancellationToken = default)
            => Task.FromResult(Convert.FromBase64String(protectedPayload));
    }

    private sealed class CancellationProbeEnumerable : IEnumerable<string>
    {
        private readonly CancellationTokenSource _cancellation;

        internal CancellationProbeEnumerable(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        internal int ReadCount { get; private set; }

        public IEnumerator<string> GetEnumerator()
        {
            for (var index = 0; index < 1000; index++)
            {
                ReadCount++;
                if (ReadCount == 1) _cancellation.Cancel();
                if (ReadCount > 16) throw new InvalidOperationException("Serialization continued after cancellation.");
                yield return new string('x', 4096);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CountingReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly IReadOnlyList<T> _values;
        private readonly int _maximumMoveNextCount;

        internal CountingReadOnlyList(IReadOnlyList<T> values, int maximumMoveNextCount)
        {
            _values = values;
            _maximumMoveNextCount = maximumMoveNextCount;
        }

        internal int MoveNextCount { get; private set; }
        public int Count => _values.Count;
        public T this[int index] => _values[index];

        public IEnumerator<T> GetEnumerator()
        {
            foreach (var value in _values)
            {
                MoveNextCount++;
                if (MoveNextCount > _maximumMoveNextCount)
                    throw new InvalidOperationException("The packet was repeatedly scanned while applying records.");
                yield return value;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class TestDiscoveryTransportFactory : IHomeAssistantDiscoveryTransportFactory
    {
        private readonly IReadOnlyList<IPAddress> _addresses;
        private readonly byte[] _packet;
        private readonly bool _blockSend;
        private readonly bool _failAfterPacket;
        private readonly bool _singleInterface;
        private readonly IPAddress? _failCreateAddress;
        private readonly object _gate = new();
        private readonly List<IPAddress> _createdAddresses = new();
        private readonly List<IPAddress> _sentAddresses = new();
        private int _createCount;

        internal TestDiscoveryTransportFactory(IReadOnlyList<IPAddress> addresses, byte[] packet, bool blockSend = false, bool failAfterPacket = false, IPAddress? failCreateAddress = null, bool singleInterface = false)
        {
            _addresses = addresses;
            _packet = packet;
            _blockSend = blockSend;
            _failAfterPacket = failAfterPacket;
            _failCreateAddress = failCreateAddress;
            _singleInterface = singleInterface;
        }

        internal IReadOnlyList<IPAddress> CreatedAddresses { get { lock (_gate) return _createdAddresses.ToArray(); } }
        internal IReadOnlyList<IPAddress> SentAddresses { get { lock (_gate) return _sentAddresses.ToArray(); } }
        internal int CreateCount => Volatile.Read(ref _createCount);

        public IReadOnlyList<HomeAssistantDiscoveryInterface> GetLocalInterfaces()
            => _singleInterface
                ? new[] { new HomeAssistantDiscoveryInterface("test", _addresses, 1) }
                : _addresses.Select((address, index) => new HomeAssistantDiscoveryInterface(
                "test-" + index.ToString(System.Globalization.CultureInfo.InvariantCulture),
                new[] { address },
                index + 1)).ToArray();

        public IHomeAssistantDiscoveryTransport Create(HomeAssistantDiscoveryInterface network)
        {
            Interlocked.Increment(ref _createCount);
            if (network.Addresses.Any(address => address.Equals(_failCreateAddress))) throw new SocketException((int)SocketError.AddressNotAvailable);
            lock (_gate) _createdAddresses.AddRange(network.Addresses);
            return new TestDiscoveryTransport(_packet, _blockSend, _failAfterPacket, () =>
            {
                lock (_gate) _sentAddresses.AddRange(network.Addresses);
            });
        }
    }

    private sealed class TestDiscoveryTransport : IHomeAssistantDiscoveryTransport
    {
        private readonly byte[] _packet;
        private readonly bool _blockSend;
        private readonly bool _failAfterPacket;
        private readonly Action _sent;
        private int _received;

        internal TestDiscoveryTransport(byte[] packet, bool blockSend, bool failAfterPacket, Action sent)
        {
            _packet = packet;
            _blockSend = blockSend;
            _failAfterPacket = failAfterPacket;
            _sent = sent;
        }

        public async Task SendAsync(byte[] query, CancellationToken cancellationToken)
        {
            if (_blockSend)
            {
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw new ObjectDisposedException(nameof(TestDiscoveryTransport));
                }
            }

            _sent();
        }

        public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
        {
            if (Interlocked.Exchange(ref _received, 1) == 0) return _packet;
            if (_failAfterPacket) throw new SocketException((int)SocketError.NetworkDown);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("Unreachable receive continuation.");
        }

        public void Dispose()
        {
        }
    }
}
#endif
