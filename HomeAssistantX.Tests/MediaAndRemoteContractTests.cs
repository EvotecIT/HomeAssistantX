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
    public async Task SharedOptionalAttributeTraversalObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var attributes = new CancellationProbeAttributeDictionary(cancellation, "activity_list");
        var operation = Task.Factory.StartNew(
            () => HomeAssistantAttributeReader.GetStringList(
                attributes,
                "activity_list",
                cancellation.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
        Assert.InRange(attributes.ReadCount, 1, 64);
    }

    [Fact]
    public async Task MediaGroupMemberLookupStopsAtCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var attributes = new CancellationProbeAttributeDictionary(cancellation);
        var operation = Task.Factory.StartNew(
            () => HomeAssistantMediaPlayerStatus.GetGroupMembers(attributes, cancellation.Token),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
        Assert.InRange(attributes.ReadCount, 1, 64);
    }

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
    [InlineData("12:00")]
    [InlineData("2026/08/27T10:00:00Z")]
    [InlineData("2026-08-27T10:00:00 +00:00")]
    public void MediaStatusIgnoresInvalidPositionTimestamps(string timestamp)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.bad\",\"state\":\"playing\",\"attributes\":{" +
            "\"media_position\":10,\"media_position_updated_at\":\"" + timestamp + "\"}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Null(status.MediaPositionUpdatedAt);
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

    [Theory]
    [InlineData("1.00000000000000000000000000001")]
    [InlineData("\"1.00000000000000000000000000001\"")]
    [InlineData("\"١\"")]
    public void TypedStatusDoesNotRoundOverprecisionCapabilitiesToAnInteger(string value)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"remote.bad\",\"state\":\"on\",\"attributes\":{" +
            "\"supported_features\":" + value + "}}");

        Assert.Equal(HomeAssistantRemoteFeature.None, HomeAssistantRemoteStatus.FromState(raw).SupportedFeatures);
    }

    [Fact]
    public void TypedStatusIntegerParsingHonorsCancellation()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"remote.bad\",\"state\":\"on\",\"attributes\":{" +
            "\"supported_features\":\"1." + new string('0', 1_000_000) + "\"}}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRemoteStatus.FromState(raw, cancellation.Token));
    }

    [Theory]
    [InlineData("[\"light.kitchen\"]")]
    [InlineData("[\"media_player.Kitchen\"]")]
    [InlineData("[\"media_player.kitchen\",\"media_player.kitchen\"]")]
    [InlineData("[null]")]
    public void MediaStatusRejectsInvalidGroupMemberIdentities(string groupMembers)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.group\",\"state\":\"idle\",\"attributes\":{" +
            "\"group_members\":" + groupMembers + "}}");

        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantMediaPlayerStatus.FromState(raw));
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

    [Fact]
    public void MediaStatusSkipsMalformedArtworkBeforeSelectingFallback()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.fallback\",\"state\":\"idle\",\"attributes\":{" +
            "\"media_image_url\":\"http://[\",\"entity_picture_local\":\"/api/media/local-artwork\"}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Equal(
            new Uri("https://ha.example.test/api/media/local-artwork"),
            status.ResolveArtworkUri(new Uri("https://ha.example.test/")));
        Assert.Equal(
            new Uri("https://ha.example.test/api/media/local-artwork"),
            status.ResolveArtworkUri(new Uri("https://ha.example.test/home-assistant/")));
    }

    [Fact]
    public void MediaStatusPreservesRelativeArtworkUnderAConfiguredBasePath()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.prefixed\",\"state\":\"idle\",\"attributes\":{" +
            "\"media_image_url\":\"api/media/relative-artwork\"}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Equal(
            new Uri("https://ha.example.test/home-assistant/api/media/relative-artwork"),
            status.ResolveArtworkUri(new Uri("https://ha.example.test/home-assistant/")));
    }

    [Fact]
    public void MediaStatusResolvesProtocolRelativeArtworkAgainstTheHomeAssistantScheme()
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.cdn\",\"state\":\"idle\",\"attributes\":{" +
            "\"media_image_url\":\"//cdn.example.test/artwork.png\"}}");

        var status = HomeAssistantMediaPlayerStatus.FromState(raw);

        Assert.Equal(
            new Uri("https://cdn.example.test/artwork.png"),
            status.ResolveArtworkUri(new Uri("https://ha.example.test/home-assistant/")));
    }

    [Theory]
    [InlineData("file:///private/artwork.png")]
    [InlineData("mailto:artwork@example.test")]
    [InlineData("https://user:password@example.test/artwork.png")]
    public void MediaStatusSkipsUnsupportedOrCredentialBearingArtworkBeforeFallback(string primary)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"media_player.fallback\",\"state\":\"idle\",\"attributes\":{" +
            "\"media_image_url\":" + JsonSerializer.Serialize(primary) + ",\"entity_picture_local\":\"/api/media/local-artwork\"}}");

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
    [InlineData("9223372036854775808")]
    public void RemoteStatusRejectsCapabilityMasksOutsideTheWireRange(string value)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"remote.bad\",\"state\":\"on\",\"attributes\":{" +
            "\"supported_features\":" + value + "}}");

        var status = HomeAssistantRemoteStatus.FromState(raw);

        Assert.Equal(HomeAssistantRemoteFeature.None, status.SupportedFeatures);
    }

    [Theory]
    [InlineData("2147483649")]
    [InlineData("4294967297")]
    public void RemoteStatusRetainsKnownCapabilitiesWhenFutureHighBitsArePresent(string value)
    {
        var raw = DeserializeState(
            "{\"entity_id\":\"remote.living_room\",\"state\":\"on\",\"attributes\":{" +
            "\"supported_features\":" + value + "}}");

        var status = HomeAssistantRemoteStatus.FromState(raw);

        Assert.True(status.Supports(HomeAssistantRemoteFeature.LearnCommand));
        Assert.Equal(long.Parse(value, System.Globalization.CultureInfo.InvariantCulture), (long)status.SupportedFeatures);
    }

    [Fact]
    public void TypedMediaAndRemoteViewsRejectTheWrongEntityDomain()
    {
        var state = DeserializeState("{\"entity_id\":\"light.kitchen\",\"state\":\"on\",\"attributes\":{}}");

        Assert.Throws<ArgumentException>(() => HomeAssistantMediaPlayerStatus.FromState(state));
        Assert.Throws<ArgumentException>(() => HomeAssistantRemoteStatus.FromState(state));
    }

    [Theory]
    [InlineData("media_player.kitchen.extra", true)]
    [InlineData("media_player.Kitchen", true)]
    [InlineData(" media_player.kitchen ", true)]
    [InlineData("remote.living_room.extra", false)]
    [InlineData("remote.Living_room", false)]
    [InlineData(" remote.living_room ", false)]
    public void TypedMediaAndRemoteViewsRejectNonCanonicalEntityIdentifiers(string entityId, bool mediaPlayer)
    {
        var state = DeserializeState("{\"entity_id\":\"" + entityId + "\",\"state\":\"on\",\"attributes\":{}}");

        if (mediaPlayer)
            Assert.Throws<ArgumentException>(() => HomeAssistantMediaPlayerStatus.FromState(state));
        else
            Assert.Throws<ArgumentException>(() => HomeAssistantRemoteStatus.FromState(state));
    }

    [Theory]
    [InlineData("media_player.kitchen")]
    [InlineData("remote.living_room")]
    public void TypedMediaAndRemoteViewsRejectMissingStateValues(string entityId)
    {
        var state = DeserializeState("{\"entity_id\":\"" + entityId + "\",\"state\":null,\"attributes\":{}}");

        if (entityId.StartsWith("media_player", StringComparison.Ordinal))
            Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantMediaPlayerStatus.FromState(state));
        else
            Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRemoteStatus.FromState(state));
    }

    [Theory]
    [InlineData("9007199254740993.0")]
    [InlineData("\"9007199254740993.0\"")]
    public void IntegralAttributesPreserveValuesBeyondDoublePrecision(string value)
    {
        using var document = JsonDocument.Parse("{\"value\":" + value + "}");
        var attributes = new Dictionary<string, JsonElement>
        {
            ["value"] = document.RootElement.GetProperty("value").Clone()
        };

        Assert.Equal(9007199254740993L, HomeAssistantAttributeReader.GetInt64(attributes, "value"));
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
    public async Task CompoundTypedControlsCaptureOneTransportPerOperation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var pause = server.PauseNextServiceCall();

        var operation = client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions
            {
                Power = HomeAssistantPowerAction.On,
                VolumePercent = 35
            });

        await pause.Received.WaitAsync(TimeSpan.FromSeconds(2));
        client.Options.ControlServiceCallTransport = HomeAssistantServiceCallTransport.Rest;
        pause.Release();
        await operation;

        Assert.Equal(2, server.ServiceCallBodies.Count);
        foreach (var body in server.ServiceCallBodies)
        {
            using var call = JsonDocument.Parse(body);
            Assert.Equal("call_service", call.RootElement.GetProperty("type").GetString());
        }

        await client.Controls.MediaPlayers.SetVolumeAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            40);
        Assert.Equal("/api/services/media_player/volume_set", server.LastRequestPath);
    }

    [Fact]
    public async Task MediaSequencesCaptureOneTransportAcrossEveryStep()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var pause = server.PauseNextServiceCall();

        var operation = client.Controls.MediaPlayers.ExecuteSequenceAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions { VolumePercent = 35 },
            HomeAssistantMediaVolumeStepAction.Up,
            clearPlaylist: true,
            groupMembers: null,
            unjoin: false,
            mediaContentId: null,
            mediaContentType: null,
            playMediaOptions: null,
            seekPosition: null,
            playback: HomeAssistantMediaPlaybackAction.Play,
            CancellationToken.None);

        await pause.Received.WaitAsync(TimeSpan.FromSeconds(2));
        client.Options.ControlServiceCallTransport = HomeAssistantServiceCallTransport.Rest;
        pause.Release();
        await operation;

        Assert.Equal(4, server.ServiceCallBodies.Count);
        Assert.All(server.ServiceCallBodies, body =>
        {
            using var call = JsonDocument.Parse(body);
            Assert.Equal("call_service", call.RootElement.GetProperty("type").GetString());
        });
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
    public async Task ExplicitFalseAnnouncementWithoutMediaIsRejectedBeforeOtherDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.SetAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            new HomeAssistantMediaPlayerOptions
            {
                VolumePercent = 35,
                Announce = false
            }));

        Assert.Empty(server.ServiceCallBodies);
    }

    [Fact]
    public async Task CompoundMediaOperationSnapshotsEveryOptionBeforeItsFirstDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var pause = server.PauseNextServiceCall();
        var target = HomeAssistantTarget.ForEntity("media_player.kitchen");
        var options = new HomeAssistantMediaPlayerOptions
        {
            Power = HomeAssistantPowerAction.On,
            MediaContentId = "media-source://radio/station",
            MediaContentType = "music",
            Enqueue = HomeAssistantMediaEnqueueMode.Add,
            Announce = false
        };

        var operation = client.Controls.MediaPlayers.SetAsync(target, options);
        await pause.Received.WaitAsync(TimeSpan.FromSeconds(2));
        target.EntityIds = new[] { "media_player.mutated" };
        options.Announce = true;
        options.MediaContentId = "media-source://radio/mutated";
        pause.Release();
        await operation;

        using var play = FindCall(server, "play_media");
        var data = play.RootElement.GetProperty("service_data");
        Assert.Equal("media_player.kitchen", play.RootElement.GetProperty("target").GetProperty("entity_id")[0].GetString());
        Assert.Equal("media-source://radio/station", data.GetProperty("media_content_id").GetString());
        Assert.Equal("add", data.GetProperty("enqueue").GetString());
        Assert.False(data.GetProperty("announce").GetBoolean());
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
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.JoinAsync(
            target,
            new[] { "media_player.Kitchen" }));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HomeAssistantRemoteLearnOptions
        {
            Timeout = TimeSpan.FromSeconds((double)int.MaxValue + 1d)
        });
        var cyclicExtra = new Dictionary<string, object?>();
        cyclicExtra["self"] = cyclicExtra;
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.MediaPlayers.SetAsync(
            target,
            new HomeAssistantMediaPlayerOptions
            {
                VolumePercent = 35,
                MediaContentId = "media-source://radio/station",
                MediaContentType = "music",
                MediaExtra = cyclicExtra
            }));

        Assert.Empty(server.ServiceCallBodies);
    }

    [Fact]
    public async Task CancelledMediaOperationsDoNotFreezeCallerOwnedExtraPayloads()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cyclicExtra = new Dictionary<string, object?>();
        cyclicExtra["self"] = cyclicExtra;
        var target = HomeAssistantTarget.ForEntity("media_player.kitchen");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Controls.MediaPlayers.SetAsync(
            target,
            new HomeAssistantMediaPlayerOptions
            {
                MediaContentId = "media-source://radio/station",
                MediaContentType = "music",
                MediaExtra = cyclicExtra
            },
            cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Controls.MediaPlayers.PlayMediaAsync(
            target,
            "media-source://radio/station",
            "music",
            new HomeAssistantPlayMediaOptions { Extra = cyclicExtra },
            cancellation.Token));

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
    public async Task TypedMediaAndRemoteGettersHonorCancellationBeforeEntityValidation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.MediaPlayers.GetAsync(" light." + new string('x', 1_000_000), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.Remotes.GetAsync(" remote." + new string('x', 1_000_000), cancellation.Token));

        Assert.Null(server.LastRequestPath);
    }

    [Fact]
    public async Task TypedMediaAndRemoteGettersRejectMismatchedResponseEntities()
    {
        using var server = new TestHomeAssistantServer
        {
            ExactStateResponseJson = "{\"entity_id\":\"media_player.bedroom\",\"state\":\"idle\",\"attributes\":{}}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Controls.MediaPlayers.GetAsync("media_player.kitchen"));

        server.ExactStateResponseJson = "{\"entity_id\":\"remote.bedroom\",\"state\":\"on\",\"attributes\":{}}";
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Controls.Remotes.GetAsync("remote.living_room"));
    }

    [Fact]
    public void TypedResponseEntityValidationAndStateParsingHonorCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var state = DeserializeState(
            "{\"entity_id\":\"media_player.kitchen\",\"state\":\"idle\",\"attributes\":{}}");

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantEntityId.RequireResponseEntity(
                state,
                "media_player.kitchen",
                cancellation.Token));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantMediaPlayerStatus.FromState(state, cancellation.Token));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantMediaPlayerClient.NormalizeEntityIds(
                new[] { "media_player.kitchen" },
                "entityIds",
                cancellation.Token));
    }

    [Fact]
    public async Task TypedBulkReadsRejectMalformedServerEntityIds()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        server.SetStates("[{\"entity_id\":\"media_player.kitchen.extra\",\"state\":\"idle\",\"attributes\":{}}]");

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Controls.MediaPlayers.GetAllAsync());

        server.SetStates("[{\"entity_id\":\"remote.living.room\",\"state\":\"on\",\"attributes\":{}}]");
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Controls.Remotes.GetAllAsync());

        server.SetStates("[{\"entity_id\":null,\"state\":\"idle\",\"attributes\":{}}]");
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Controls.MediaPlayers.GetAllAsync());

        server.SetStates("[{\"entity_id\":\" remote.living_room\",\"state\":\"on\",\"attributes\":{}}]");
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.Controls.Remotes.GetAllAsync());
    }

    [Fact]
    public void TypedBulkProjectionStopsWhenCancellationArrivesDuringStateValidation()
    {
        using var cancellation = new CancellationTokenSource();
        var states = new CancellationProbeStateEnumerable(cancellation);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantEntityId.RequireResponseDomainStates(
                states,
                "media_player",
                cancellation.Token).ToArray());

        Assert.InRange(states.ReadCount, 1, 64);
    }

    [Fact]
    public void TypedStatusProjectionChecksCancellationBeforeParsingAttributes()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var invalid = DeserializeState(
            "{\"entity_id\":\"media_player.kitchen\",\"state\":\"idle\",\"attributes\":{\"source_list\":{}}}");

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantMediaPlayerStatus.FromState(invalid, cancellation.Token));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRemoteStatus.FromState(invalid, cancellation.Token));
    }

    [Fact]
    public void MediaGroupMembersAreNormalizedIntoAnIndependentSnapshot()
    {
        var callerOwned = new List<string> { " media_player.kitchen " };

        var snapshot = HomeAssistantMediaPlayerClient.NormalizeEntityIds(
            callerOwned,
            "JoinMember",
            CancellationToken.None);
        callerOwned[0] = "light.changed_after_validation";

        Assert.Equal(new[] { "media_player.kitchen" }, snapshot);
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
    public async Task RemoteSelectorsRejectBlankValuesBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("remote.living_room");

        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Remotes.SetPowerAsync(target, HomeAssistantPowerAction.On, " "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Remotes.SendCommandsAsync(
            target, new[] { "power" }, new HomeAssistantRemoteSendOptions { Device = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Remotes.LearnCommandsAsync(
            target, new HomeAssistantRemoteLearnOptions { Device = " " }));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Controls.Remotes.DeleteCommandsAsync(target, new[] { "power" }, " "));

        Assert.Empty(server.ServiceCallBodies);
    }

    [Fact]
    public async Task RemoteActivityAndDeviceSelectorsPreserveIntegrationDefinedText()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = HomeAssistantTarget.ForEntity("remote.living_room");

        await client.Controls.Remotes.SetPowerAsync(target, HomeAssistantPowerAction.On, " Watch TV ");
        await client.Controls.Remotes.SendCommandsAsync(
            target,
            new[] { "power" },
            new HomeAssistantRemoteSendOptions { Device = " Living Room TV " });

        using var power = FindCall(server, "turn_on");
        Assert.Equal(" Watch TV ", power.RootElement.GetProperty("service_data").GetProperty("activity").GetString());
        using var send = FindCall(server, "send_command");
        Assert.Equal(" Living Room TV ", send.RootElement.GetProperty("service_data").GetProperty("device").GetString());
    }

    [Fact]
    public async Task RemoteAndMediaEnumerablesHonorPreCanceledTokens()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var values = new ThrowingStringList();
        var remoteTarget = HomeAssistantTarget.ForEntity("remote.living_room");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.Remotes.SendCommandsAsync(remoteTarget, values, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.Remotes.LearnCommandsAsync(
                remoteTarget,
                new HomeAssistantRemoteLearnOptions { Commands = values },
                cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.Remotes.DeleteCommandsAsync(remoteTarget, values, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.MediaPlayers.JoinAsync(
                HomeAssistantTarget.ForEntity("media_player.kitchen"),
                values,
                cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.MediaPlayers.SetVolumeAsync(
                new HomeAssistantTarget { EntityIds = values },
                35,
                cancellation.Token));
        var cyclicExtra = new Dictionary<string, object?>();
        cyclicExtra["self"] = cyclicExtra;
        var mediaTarget = HomeAssistantTarget.ForEntity("media_player.kitchen");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.MediaPlayers.SetAsync(
                mediaTarget,
                new HomeAssistantMediaPlayerOptions
                {
                    MediaContentId = "media-source://local/song.mp3",
                    MediaContentType = "music",
                    MediaExtra = cyclicExtra
                },
                cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Controls.MediaPlayers.PlayMediaAsync(
                mediaTarget,
                "media-source://local/song.mp3",
                "music",
                new HomeAssistantPlayMediaOptions { Extra = cyclicExtra },
                cancellation.Token));
        Assert.Empty(server.ServiceCallBodies);
    }

    [Fact]
    public async Task RemoteAndMediaEnumerablesHonorMidTraversalCancellation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var remoteTarget = HomeAssistantTarget.ForEntity("remote.living_room");

        using (var cancellation = new CancellationTokenSource())
        {
            var values = new CancellationProbeStringList(cancellation);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.Controls.Remotes.SendCommandsAsync(remoteTarget, values, cancellationToken: cancellation.Token));
            Assert.InRange(values.ReadCount, 1, 64);
        }

        using (var cancellation = new CancellationTokenSource())
        {
            var values = new CancellationProbeStringList(cancellation);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.Controls.Remotes.LearnCommandsAsync(
                    remoteTarget,
                    new HomeAssistantRemoteLearnOptions { Commands = values },
                    cancellation.Token));
            Assert.InRange(values.ReadCount, 1, 64);
        }

        using (var cancellation = new CancellationTokenSource())
        {
            var values = new CancellationProbeStringList(cancellation);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.Controls.Remotes.DeleteCommandsAsync(remoteTarget, values, cancellationToken: cancellation.Token));
            Assert.InRange(values.ReadCount, 1, 64);
        }

        using (var cancellation = new CancellationTokenSource())
        {
            var values = new CancellationProbeStringList(cancellation, "media_player.dining");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.Controls.MediaPlayers.JoinAsync(
                    HomeAssistantTarget.ForEntity("media_player.kitchen"),
                    values,
                    cancellation.Token));
            Assert.InRange(values.ReadCount, 1, 64);
        }

        using (var cancellation = new CancellationTokenSource())
        {
            var values = new CancellationProbeStringList(cancellation, "media_player.kitchen");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.Controls.MediaPlayers.SetVolumeAsync(
                    new HomeAssistantTarget { EntityIds = values },
                    35,
                    cancellation.Token));
            Assert.InRange(values.ReadCount, 1, 64);
        }

        using (var cancellation = new CancellationTokenSource())
        {
            var values = new CancellationProbeStringList(cancellation, new string('x', 4096));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                client.Controls.MediaPlayers.SetAsync(
                    HomeAssistantTarget.ForEntity("media_player.kitchen"),
                    new HomeAssistantMediaPlayerOptions
                    {
                        MediaContentId = "media-source://local/song.mp3",
                        MediaContentType = "music",
                        MediaExtra = new Dictionary<string, object?> { ["values"] = values }
                    },
                    cancellation.Token));
            Assert.InRange(values.ReadCount, 1, 64);
        }

        Assert.Empty(server.ServiceCallBodies);
    }

    [Fact]
    public async Task DirectSoundModeSelectionNormalizesTheSelector()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Controls.MediaPlayers.SelectSoundModeAsync(
            HomeAssistantTarget.ForEntity("media_player.kitchen"),
            " Music ");

        using var call = FindCall(server, "select_sound_mode");
        Assert.Equal("Music", call.RootElement.GetProperty("service_data").GetProperty("sound_mode").GetString());
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
    public async Task CompoundMediaControlPinsOneRequestTimeoutBeforeTargetPreparation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromSeconds(3));
        var target = new HomeAssistantTarget
        {
            EntityIds = new MutatingStringList(
                () => client.Options.RequestTimeout = TimeSpan.Zero,
                "media_player.living_room")
        };

        var results = await client.Controls.MediaPlayers.SetAsync(
            target,
            new HomeAssistantMediaPlayerOptions
            {
                Power = HomeAssistantPowerAction.On,
                VolumePercent = 25
            });

        Assert.Equal(2, results.Count);
        Assert.Equal(2, server.ServiceCallBodies.Count);
    }

    [Fact]
    public async Task RemoteLearningPinsOneDeadlineAcrossValidationAndDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromSeconds(2.2));
        var pause = server.PauseNextServiceCall();
        var commands = new MutatingStringList(
            () => client.Options.RequestTimeout = TimeSpan.FromSeconds(10),
            "power");

        var operation = client.Controls.Remotes.LearnCommandsAsync(
            HomeAssistantTarget.ForEntity("remote.living_room"),
            new HomeAssistantRemoteLearnOptions
            {
                Timeout = TimeSpan.FromSeconds(1),
                Commands = commands
            });

        await pause.Received;
        try
        {
            var winner = await Task.WhenAny(operation, Task.Delay(TimeSpan.FromSeconds(5)));
            Assert.Same(operation, winner);
            await Assert.ThrowsAsync<HomeAssistantConnectionException>(async () => await operation);
        }
        finally
        {
            pause.Release();
        }
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

    private sealed class ThrowingStringList : IReadOnlyList<string>
    {
        public int Count => 1;

        public string this[int index] => throw new InvalidOperationException("The collection must not be enumerated.");

        public IEnumerator<string> GetEnumerator()
            => throw new InvalidOperationException("The collection must not be enumerated.");

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class CancellationProbeStringList : IReadOnlyList<string>
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly string _value;

        internal CancellationProbeStringList(CancellationTokenSource cancellation, string value = "power")
        {
            _cancellation = cancellation;
            _value = value;
        }

        internal int ReadCount { get; private set; }

        public int Count => 1000;

        public string this[int index] => _value;

        public IEnumerator<string> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                ReadCount++;
                if (ReadCount == 1) _cancellation.Cancel();
                if (ReadCount > 64) throw new InvalidOperationException("Enumeration continued after cancellation.");
                yield return _value;
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class CancellationProbeAttributeDictionary : IReadOnlyDictionary<string, JsonElement>
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly string _missingKey;

        internal CancellationProbeAttributeDictionary(
            CancellationTokenSource cancellation,
            string missingKey = "group_members")
        {
            _cancellation = cancellation;
            _missingKey = missingKey;
        }

        internal int ReadCount { get; private set; }

        public int Count => 1000;

        public IEnumerable<string> Keys => throw new NotSupportedException();

        public IEnumerable<JsonElement> Values => throw new NotSupportedException();

        public JsonElement this[string key] => default;

        public bool ContainsKey(string key) => !string.Equals(key, _missingKey, StringComparison.Ordinal);

        public bool TryGetValue(string key, out JsonElement value)
        {
            value = default;
            return !string.Equals(key, _missingKey, StringComparison.Ordinal);
        }

        public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
            {
                ReadCount++;
                if (ReadCount == 1) _cancellation.Cancel();
                if (ReadCount > 64)
                {
                    throw new InvalidOperationException("Attribute traversal continued after cancellation.");
                }

                yield return new KeyValuePair<string, JsonElement>("provider_" + index, default);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class MutatingStringList : IReadOnlyList<string>
    {
        private readonly Action _onRead;
        private readonly string _value;
        private int _read;

        internal MutatingStringList(Action onRead, string value)
        {
            _onRead = onRead;
            _value = value;
        }

        public int Count => 1;

        public string this[int index]
        {
            get
            {
                MutateOnce();
                return _value;
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            MutateOnce();
            yield return _value;
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();

        private void MutateOnce()
        {
            if (Interlocked.Exchange(ref _read, 1) == 0)
            {
                _onRead();
            }
        }
    }

    private sealed class CancellationProbeStateEnumerable : IEnumerable<HomeAssistantState>
    {
        private readonly CancellationTokenSource _cancellation;

        internal CancellationProbeStateEnumerable(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        internal int ReadCount { get; private set; }

        public IEnumerator<HomeAssistantState> GetEnumerator()
        {
            for (var index = 0; index < 1000; index++)
            {
                ReadCount++;
                if (ReadCount == 1) _cancellation.Cancel();
                if (ReadCount > 64) throw new InvalidOperationException("Enumeration continued after cancellation.");
                yield return new HomeAssistantState
                {
                    EntityId = "media_player.test_" + index,
                    State = "idle"
                };
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

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
