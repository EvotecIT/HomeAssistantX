using System.Text.Json;
using HomeAssistantX.Controls;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class MediaAndRemoteContractTests
{
    [Fact]
    public void MediaStatusParsesFeaturesMetadataAndForwardCompatibleRawAttributes()
    {
        var raw = DeserializeState(MediaPlayingStateJson);

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Equal(HomeAssistantMediaPlayerState.Playing, status.State);
        Assert.True(status.Supports(HomeAssistantMediaPlayerFeature.VolumeSet));
        Assert.True(status.Supports(HomeAssistantMediaPlayerFeature.VolumeStep));
        Assert.True(status.Supports(HomeAssistantMediaPlayerFeature.SelectSource));
        Assert.True(status.Supports(HomeAssistantMediaPlayerFeature.Grouping));
        Assert.Equal(35, status.VolumePercent);
        Assert.Equal(TimeSpan.FromMinutes(5), status.MediaDuration);
        Assert.Equal(TimeSpan.FromSeconds(120), status.MediaPosition);
        Assert.Equal("Kitchen speaker", status.FriendlyName);
        Assert.Equal(new[] { "AirPlay", "TV" }, status.Sources);
        Assert.Equal(new[] { "Music", "Night" }, status.SoundModes);
        Assert.Equal(new[] { "media_player.kitchen", "media_player.dining" }, status.GroupMembers);
        Assert.Equal(7, status.MediaTrack);
        Assert.Equal("preserved", status.RawState.Attributes["future_media_field"].GetString());
        Assert.Equal(
            new Uri("https://ha.example.test/api/media_player_proxy/media_player.kitchen"),
            status.ResolveArtworkUri(new Uri("https://ha.example.test/")));

        var estimated = status.GetEstimatedPosition(
            new DateTimeOffset(2026, 8, 26, 10, 1, 30, TimeSpan.Zero));
        Assert.Equal(TimeSpan.FromSeconds(210), estimated);
    }

    [Fact]
    public void MediaStatusTreatsMalformedOptionalNumbersAsAbsent()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.bad\",\"state\":\"future_state\",\"attributes\":{" +
            "\"supported_features\":\"7.5\",\"volume_level\":\"Infinity\"," +
            "\"media_duration\":1e100,\"media_position\":-1}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Equal(HomeAssistantMediaPlayerState.Other, status.State);
        Assert.Equal(HomeAssistantMediaPlayerFeature.None, status.SupportedFeatures);
        Assert.Null(status.VolumeLevel);
        Assert.Null(status.MediaDuration);
        Assert.Null(status.MediaPosition);
        Assert.Null(status.GetEstimatedPosition(DateTimeOffset.UtcNow));
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(1e100)]
    public void MediaStatusTreatsOutOfRangeVolumeMetadataAsAbsent(double value)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.bad\",\"state\":\"idle\",\"attributes\":{" +
            "\"volume_level\":" + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "," +
            "\"volume_step\":" + value.ToString(System.Globalization.CultureInfo.InvariantCulture) + "}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Null(status.VolumeLevel);
        Assert.Null(status.VolumePercent);
        Assert.Null(status.VolumeStep);
    }

    [Fact]
    public void MediaStatusRejectsNegativeCapabilitiesAndUnrepresentableTimes()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.bad\",\"state\":\"playing\",\"attributes\":{" +
            "\"supported_features\":-1,\"media_duration\":922337203685.4775," +
            "\"media_position\":922337203685.4775}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Equal(HomeAssistantMediaPlayerFeature.None, status.SupportedFeatures);
        Assert.Null(status.MediaDuration);
        Assert.Null(status.MediaPosition);
    }

    [Fact]
    public void MediaPositionEstimationSaturatesAtTheLargestRepresentableTimeSpan()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.long\",\"state\":\"playing\",\"attributes\":{" +
            "\"media_position\":922337203685.4774," +
            "\"media_position_updated_at\":\"0001-01-01T00:00:00Z\"}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.NotNull(status.MediaPosition);
        Assert.Equal(TimeSpan.MaxValue, status.GetEstimatedPosition(DateTimeOffset.MaxValue));
    }

    [Theory]
    [InlineData("9223372036854775808")]
    [InlineData("\"9223372036854775808\"")]
    public void TypedStatusTreatsOutOfRangeInt64AttributesAsAbsent(string value)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"remote.bad\",\"state\":\"on\",\"attributes\":{" +
            "\"supported_features\":" + value + "}}");

        var status = HomeAssistantRemoteStatus.FromState(raw);

        Assert.Equal(HomeAssistantRemoteFeature.None, status.SupportedFeatures);
    }

    [Fact]
    public void MediaStatusSkipsBlankArtworkBeforeSelectingFallback()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.fallback\",\"state\":\"idle\",\"attributes\":{" +
            "\"media_image_url\":\"   \",\"entity_picture_local\":\"/api/media/local-artwork\"}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Equal(
            new Uri("https://ha.example.test/api/media/local-artwork"),
            status.ResolveArtworkUri(new Uri("https://ha.example.test/")));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"attributes\":null}")]
    [InlineData("{\"attributes\":\"malformed\"}")]
    public void StateDecoderTreatsMissingNullOrMalformedAttributesAsEmpty(string fragment)
    {
        using var document = JsonDocument.Parse(fragment);
        var attributes = document.RootElement.TryGetProperty("attributes", out var value)
            ? ",\"attributes\":" + value.GetRawText()
            : "";
        var state = DeserializeState(
            "{\"entity_id\":\"sensor.compatibility\",\"state\":\"unknown\"" + attributes + "}");

        Assert.Empty(state.Attributes);
    }

    [Fact]
    public void StateDecoderPreservesCaseDistinctAttributesAndKnownReadsStayCaseInsensitive()
    {
        var state = DeserializeState(
            "{\"entity_id\":\"media_player.case\",\"state\":\"idle\",\"attributes\":{" +
            "\"friendly_name\":\"First\",\"Friendly_Name\":\"Second\",\"future\":1,\"Future\":2}}");

        Assert.Equal(4, state.Attributes.Count);
        Assert.Equal(1, state.Attributes["future"].GetInt32());
        Assert.Equal(2, state.Attributes["Future"].GetInt32());
        Assert.Equal("First", HomeAssistantMediaPlayerStatus.FromState(state).FriendlyName);
        Assert.True(state.TryGetAttribute<string>("FRIENDLY_NAME", out var name));
        Assert.Equal("First", name);
    }

    [Fact]
    public void StateAttributesNormalizeCallerAssignedNullBeforeSerialization()
    {
        var state = new HomeAssistantState
        {
            EntityId = "sensor.compatibility",
            State = "unknown",
            Attributes = null!
        };

        var json = JsonSerializer.Serialize(state);

        Assert.NotNull(state.Attributes);
        Assert.Empty(state.Attributes);
        using var document = JsonDocument.Parse(json);
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("attributes").ValueKind);
    }

    [Fact]
    public void RemoteStatusParsesActivityAndUnknownFieldsWithoutLosingRawState()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"remote.living_room\",\"state\":\"on\",\"attributes\":{" +
            "\"friendly_name\":\"Living room remote\",\"supported_features\":\"7.0\"," +
            "\"current_activity\":\"Watch TV\",\"activity_list\":[\"Watch TV\",\"Music\"]," +
            "\"future_remote_field\":{\"value\":1}}}");

        var status = HomeAssistantRemoteStatus.FromState(raw);

        Assert.True(status.IsOn);
        Assert.True(status.IsAvailable);
        Assert.True(status.Supports(HomeAssistantRemoteFeature.LearnCommand));
        Assert.True(status.Supports(HomeAssistantRemoteFeature.DeleteCommand));
        Assert.True(status.Supports(HomeAssistantRemoteFeature.Activity));
        Assert.Equal("Watch TV", status.CurrentActivity);
        Assert.Equal(new[] { "Watch TV", "Music" }, status.Activities);
        Assert.Equal(1, status.RawState.Attributes["future_remote_field"].GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("4294967297")]
    public void RemoteStatusRejectsCapabilityMasksOutsideTheEnumRange(string value)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"remote.bad\",\"state\":\"on\",\"attributes\":{" +
            "\"supported_features\":" + value + "}}");

        var status = HomeAssistantRemoteStatus.FromState(raw);

        Assert.Equal(HomeAssistantRemoteFeature.None, status.SupportedFeatures);
    }

    [Fact]
    public void TypedMediaAndRemoteViewsRejectTheWrongEntityDomain()
    {
        var state = DeserializeState("{\"entity_id\":\"light.kitchen\",\"state\":\"on\",\"attributes\":{}}");

        Assert.Throws<ArgumentException>(() => HomeAssistantMediaPlayerStatus.FromState(state));
        Assert.Throws<ArgumentException>(() => HomeAssistantRemoteStatus.FromState(state));
    }

#if NET10_0
    [Fact]
    public async Task MediaActionsMapCompleteStandardServicePayloadsInDeterministicOrder()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("media_player.kitchen");

        await client.Controls.MediaPlayers.SetAsync(
            target,
            new HomeAssistantMediaPlayerOptions
            {
                Power = HomeAssistantPowerAction.On,
                VolumePercent = 35,
                Muted = false,
                Source = "AirPlay",
                SoundMode = "Music",
                Shuffle = true,
                Repeat = HomeAssistantMediaRepeatMode.All,
                Playback = HomeAssistantMediaPlaybackAction.Play
            });

        Assert.Equal(
            new[]
            {
                "turn_on",
                "volume_set",
                "volume_mute",
                "select_source",
                "select_sound_mode",
                "shuffle_set",
                "repeat_set",
                "media_play"
            },
            ReadServices(server));

        using var sound = FindCall(server, "select_sound_mode");
        Assert.Equal("Music", sound.RootElement.GetProperty("service_data").GetProperty("sound_mode").GetString());
        using var repeat = FindCall(server, "repeat_set");
        Assert.Equal("all", repeat.RootElement.GetProperty("service_data").GetProperty("repeat").GetString());
    }

    [Fact]
    public async Task TypedControlsCanUseRestWithoutChangingExplicitServiceMethods()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(
            server,
            controlServiceCallTransport: HomeAssistantServiceCallTransport.Rest);

        await client.Controls.MediaPlayers.SetVolumeAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            35);

        Assert.Equal("/api/services/media_player/volume_set", server.LastRequestPath);
        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.Equal("media_player.kitchen", body.RootElement.GetProperty("entity_id").GetString());
        Assert.Equal(0.35, body.RootElement.GetProperty("volume_level").GetDouble(), 3);

        await client.Services.CallAsync(
            HomeAssistantServiceCall.Create("homeassistant", "update_entity")
                .ForEntity("media_player.kitchen"));

        using var explicitCall = JsonDocument.Parse(server.ServiceCallBodies.Last());
        Assert.Equal("call_service", explicitCall.RootElement.GetProperty("type").GetString());
        Assert.Equal("update_entity", explicitCall.RootElement.GetProperty("service").GetString());
    }

    [Fact]
    public async Task TypedControlsObserveTransportChangesAfterClientConstruction()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        client.Options.ControlServiceCallTransport = HomeAssistantServiceCallTransport.Rest;

        await client.Controls.MediaPlayers.SetVolumeAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            35);

        Assert.Equal("/api/services/media_player/volume_set", server.LastRequestPath);
    }

    [Fact]
    public async Task MediaSeekGroupingPlaylistAndPlayMediaUseExactHomeAssistantFields()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("media_player.kitchen");

        await client.Controls.MediaPlayers.StepVolumeAsync(target, HomeAssistantMediaVolumeStepAction.Up);
        await client.Controls.MediaPlayers.SeekAsync(target, TimeSpan.FromSeconds(42.5));
        await client.Controls.MediaPlayers.ClearPlaylistAsync(target);
        await client.Controls.MediaPlayers.JoinAsync(
            target,
            new[] { " media_player.dining ", "media_player.office" });
        await client.Controls.MediaPlayers.UnjoinAsync(target);
        await client.Controls.MediaPlayers.PlayMediaAsync(
            target,
            "media-source://radio/station",
            "music",
            new HomeAssistantPlayMediaOptions
            {
                Enqueue = HomeAssistantMediaEnqueueMode.Next,
                Extra = new Dictionary<string, object?> { ["metadata"] = "station" }
            });

        Assert.Equal(
            new[] { "volume_up", "media_seek", "clear_playlist", "join", "unjoin", "play_media" },
            ReadServices(server));
        using var seek = FindCall(server, "media_seek");
        Assert.Equal(42.5, seek.RootElement.GetProperty("service_data").GetProperty("seek_position").GetDouble());
        using var join = FindCall(server, "join");
        Assert.Equal(
            new[] { "media_player.dining", "media_player.office" },
            join.RootElement.GetProperty("service_data").GetProperty("group_members")
                .EnumerateArray().Select(value => value.GetString()).ToArray());
        using var play = FindCall(server, "play_media");
        var playData = play.RootElement.GetProperty("service_data");
        Assert.Equal("next", playData.GetProperty("enqueue").GetString());
        Assert.Equal("station", playData.GetProperty("extra").GetProperty("metadata").GetString());
    }

    [Fact]
    public async Task ExplicitFalseAnnouncementCanBeCombinedWithEnqueue()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("media_player.kitchen");

        await client.Controls.MediaPlayers.PlayMediaAsync(
            target,
            "media-source://radio/station",
            "music",
            new HomeAssistantPlayMediaOptions
            {
                Enqueue = HomeAssistantMediaEnqueueMode.Next,
                Announce = false
            });
        await client.Controls.MediaPlayers.SetAsync(
            target,
            new HomeAssistantMediaPlayerOptions
            {
                MediaContentId = "media-source://radio/second",
                MediaContentType = "music",
                Enqueue = HomeAssistantMediaEnqueueMode.Add,
                Announce = false
            });

        Assert.Equal(2, server.ServiceCallBodies.Count);
        foreach (var body in server.ServiceCallBodies)
        {
            using var call = JsonDocument.Parse(body);
            var data = call.RootElement.GetProperty("service_data");
            Assert.False(data.GetProperty("announce").GetBoolean());
            Assert.True(data.TryGetProperty("enqueue", out _));
        }
    }

    [Fact]
    public async Task ContradictoryOrInvalidAdvancedMediaOperationsFailBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("media_player.kitchen");

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.PlayMediaAsync(
            target,
            "media-source://radio/station",
            "music",
            new HomeAssistantPlayMediaOptions
            {
                Enqueue = HomeAssistantMediaEnqueueMode.Next,
                Announce = true
            }));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.Controls.MediaPlayers.StepVolumeAsync(
            target,
            (HomeAssistantMediaVolumeStepAction)99));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.JoinAsync(
            target,
            new[] { "light.kitchen" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.JoinAsync(
            target,
            new[] { "media_player." }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.JoinAsync(
            target,
            new[] { "media_player.kitchen.extra" }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.JoinAsync(
            target,
            new[] { "media_player.kitchen-speaker" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeAssistantRemoteLearnOptions
        {
            Timeout = TimeSpan.FromSeconds((double)int.MaxValue + 1d)
        });

        Assert.Empty(server.ServiceCallBodies);
    }

    [Fact]
    public async Task TypedMediaAndRemoteGettersRejectInvalidEntityIdsBeforeRestDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.GetAsync("light.kitchen"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.GetAsync("media_player.kitchen.extra"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Remotes.GetAsync("switch.remote"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Remotes.GetAsync("remote."));

        Assert.Null(server.LastRequestPath);
    }

    [Fact]
    public async Task RemoteActionsMapPowerSendLearnAndDeleteParameterContracts()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromSeconds(30));
        var target = HomeAssistantTarget.ForEntity("remote.living_room");

        await client.Controls.Remotes.SetPowerAsync(target, HomeAssistantPowerAction.On, "Watch TV");
        await client.Controls.Remotes.SendCommandsAsync(
            target,
            new[] { "up", "select" },
            new HomeAssistantRemoteSendOptions
            {
                Device = "television",
                RepeatCount = 2,
                Delay = TimeSpan.FromMilliseconds(400),
                Hold = TimeSpan.FromSeconds(1)
            });
        await client.Controls.Remotes.LearnCommandsAsync(
            target,
            new HomeAssistantRemoteLearnOptions
            {
                Device = "receiver",
                Commands = new[] { "power" },
                CommandType = HomeAssistantRemoteCommandType.Ir,
                Alternative = true,
                Timeout = TimeSpan.FromSeconds(15)
            });
        await client.Controls.Remotes.DeleteCommandsAsync(target, new[] { "power" }, "receiver");

        Assert.Equal(new[] { "turn_on", "send_command", "learn_command", "delete_command" }, ReadServices(server));
        using var send = FindCall(server, "send_command");
        var sendData = send.RootElement.GetProperty("service_data");
        Assert.Equal(new[] { "up", "select" }, sendData.GetProperty("command").EnumerateArray().Select(value => value.GetString()).ToArray());
        Assert.Equal("television", sendData.GetProperty("device").GetString());
        Assert.Equal(2, sendData.GetProperty("num_repeats").GetInt32());
        Assert.Equal(0.4, sendData.GetProperty("delay_secs").GetDouble(), 3);
        Assert.Equal(1, sendData.GetProperty("hold_secs").GetDouble());
        using var learn = FindCall(server, "learn_command");
        var learnData = learn.RootElement.GetProperty("service_data");
        Assert.Equal("ir", learnData.GetProperty("command_type").GetString());
        Assert.True(learnData.GetProperty("alternative").GetBoolean());
        Assert.Equal(15, learnData.GetProperty("timeout").GetInt32());
    }

    [Fact]
    public async Task RemoteLearningTimeoutMustFitInsideTheTransportDeadline()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromSeconds(10));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Controls.Remotes.LearnCommandsAsync(
                HomeAssistantTarget.ForEntity("remote.living_room"),
                new HomeAssistantRemoteLearnOptions { Timeout = TimeSpan.FromSeconds(10) }));

        Assert.Empty(server.ServiceCallBodies);
    }

    [Fact]
    public async Task RemoteLearningSendsAnEffectiveDefaultInsideTheTransportDeadline()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromSeconds(10));

        await client.Controls.Remotes.LearnCommandsAsync(
            HomeAssistantTarget.ForEntity("remote.living_room"));

        using var call = FindCall(server, "learn_command");
        Assert.Equal(9, call.RootElement.GetProperty("service_data").GetProperty("timeout").GetInt32());

        using var shortClient = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromSeconds(1));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            shortClient.Controls.Remotes.LearnCommandsAsync(
                HomeAssistantTarget.ForEntity("remote.living_room")));

        using var fractionalClient = TestClientFactory.Create(
            server,
            requestTimeout: TimeSpan.FromSeconds(10.001));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            fractionalClient.Controls.Remotes.LearnCommandsAsync(
                HomeAssistantTarget.ForEntity("remote.living_room"),
                new HomeAssistantRemoteLearnOptions { Timeout = TimeSpan.FromSeconds(10) }));

        server.ClearLastServiceCall();
        await fractionalClient.Controls.Remotes.LearnCommandsAsync(
            HomeAssistantTarget.ForEntity("remote.living_room"));
        using var fractionalCall = FindCall(server, "learn_command");
        Assert.Equal(9, fractionalCall.RootElement.GetProperty("service_data").GetProperty("timeout").GetInt32());
    }

    [Fact]
    public async Task RemoteLearningUsesTheCurrentTransportDeadlineAtDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromSeconds(30));
        client.Options.RequestTimeout = TimeSpan.FromSeconds(5);

        await client.Controls.Remotes.LearnCommandsAsync(
            HomeAssistantTarget.ForEntity("remote.living_room"));

        using var call = FindCall(server, "learn_command");
        Assert.Equal(4, call.RootElement.GetProperty("service_data").GetProperty("timeout").GetInt32());

        client.Options.RequestTimeout = TimeSpan.FromSeconds(4);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            client.Controls.Remotes.LearnCommandsAsync(
                HomeAssistantTarget.ForEntity("remote.living_room"),
                new HomeAssistantRemoteLearnOptions { Timeout = TimeSpan.FromSeconds(4) }));
    }

    [Fact]
    public async Task TypedMediaSubscriptionConvertsStateChangesWithoutPolling()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[" + MediaPausedStateJson + "]");
        using var client = TestClientFactory.Create(server);
        var received = new TaskCompletionSource<HomeAssistantMediaPlayerStateChange>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.Controls.MediaPlayers.SubscribeAsync((change, _) =>
        {
            received.TrySetResult(change);
            return Task.CompletedTask;
        });

        await server.PublishStateChangeAsync(
            "media_player.kitchen",
            MediaPausedStateJson,
            MediaPlayingStateJson);
        var change = await WithTimeoutAsync(received.Task);

        Assert.Equal(HomeAssistantMediaPlayerState.Paused, change.Previous!.State);
        Assert.Equal(HomeAssistantMediaPlayerState.Playing, change.Current!.State);
        Assert.Equal("Track one", change.Current.MediaTitle);
    }

    [Fact]
    public async Task TypedMediaSubscriptionClassifiesNestedWrongDomainStates()
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[" + MediaPausedStateJson + "]");
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Controls.MediaPlayers.SubscribeAsync((_, _) => Task.CompletedTask);

        await server.PublishStateChangeAsync(
            "media_player.kitchen",
            MediaPausedStateJson,
            "{\"entity_id\":\"sensor.kitchen\",\"state\":\"on\",\"attributes\":{}}");

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () =>
        {
            var completion = subscription.Completion;
            var winner = await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(completion, winner);
            await completion;
        });
    }

    [Theory]
    [InlineData(" media_player.kitchen")]
    [InlineData("media_player.kitchen ")]
    public async Task TypedMediaSubscriptionClassifiesNoncanonicalOuterEntityIds(string entityId)
    {
        using var server = new TestHomeAssistantServer();
        server.SetStates("[" + MediaPausedStateJson + "]");
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Controls.MediaPlayers.SubscribeAsync((_, _) => Task.CompletedTask);
        var state = "{\"entity_id\":\"" + entityId + "\",\"state\":\"playing\",\"attributes\":{}}";

        await server.PublishStateChangeAsync(entityId, MediaPausedStateJson, state);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () =>
        {
            var completion = subscription.Completion;
            var winner = await Task.WhenAny(completion, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(completion, winner);
            await completion;
        });
    }
#endif

    private static HomeAssistantState DeserializeState(string json)
    {
        return JsonSerializer.Deserialize<HomeAssistantState>(json)
            ?? throw new InvalidOperationException("State fixture did not deserialize.");
    }

#if NET10_0
    private static string[] ReadServices(TestHomeAssistantServer server)
    {
        return server.ServiceCallBodies.Select(body =>
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.GetProperty("service").GetString() ?? string.Empty;
        }).ToArray();
    }

    private static JsonDocument FindCall(TestHomeAssistantServer server, string service)
    {
        var body = Assert.Single(server.ServiceCallBodies, value =>
        {
            using var document = JsonDocument.Parse(value);
            return string.Equals(
                document.RootElement.GetProperty("service").GetString(),
                service,
                StringComparison.Ordinal);
        });
        return JsonDocument.Parse(body);
    }

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, winner);
        return await task;
    }
#endif

    private const string MediaPausedStateJson =
        "{\"entity_id\":\"media_player.kitchen\",\"state\":\"paused\",\"attributes\":{" +
        "\"friendly_name\":\"Kitchen speaker\",\"supported_features\":\"527364.0\"," +
        "\"media_title\":\"Track one\"}}";

    private const string MediaPlayingStateJson =
        "{\"entity_id\":\"media_player.kitchen\",\"state\":\"playing\",\"attributes\":{" +
        "\"friendly_name\":\"Kitchen speaker\",\"device_class\":\"speaker\"," +
        "\"supported_features\":\"527364.0\",\"volume_level\":\"0.35\",\"volume_step\":0.05," +
        "\"is_volume_muted\":false,\"source\":\"AirPlay\",\"source_list\":[\"AirPlay\",\"TV\"]," +
        "\"sound_mode\":\"Music\",\"sound_mode_list\":[\"Music\",\"Night\"]," +
        "\"media_content_id\":\"track:1\",\"media_content_type\":\"music\"," +
        "\"media_duration\":300,\"media_position\":120," +
        "\"media_position_updated_at\":\"2026-08-26T10:00:00Z\",\"media_title\":\"Track one\"," +
        "\"media_artist\":\"Artist\",\"media_album_name\":\"Album\",\"media_album_artist\":\"Album artist\"," +
        "\"media_track\":\"7.0\",\"media_series_title\":\"Series\",\"media_season\":\"2\"," +
        "\"media_episode\":\"4\",\"media_channel\":\"Channel\",\"media_playlist\":\"Favorites\"," +
        "\"app_id\":\"app.test\",\"app_name\":\"Test app\",\"shuffle\":true,\"repeat\":\"all\"," +
        "\"group_members\":[\"media_player.kitchen\",\"media_player.dining\"]," +
        "\"media_image_url\":\"/api/media_player_proxy/media_player.kitchen\"," +
        "\"entity_picture\":\"/api/image/old\",\"entity_picture_local\":\"/api/image/local\"," +
        "\"manufacturer\":\"Evotec\",\"model_name\":\"Speaker One\",\"future_media_field\":\"preserved\"}}";
}
