#if NET10_0
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using HomeAssistantX.Authentication;
using HomeAssistantX.Diagnostics;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class WebSocketContractTests
{
    [Fact]
    public void ConnectFailureClassificationPreservesCallerCancellationAndTimeouts()
    {
        using var caller = new CancellationTokenSource();
        using var disposal = new CancellationTokenSource();
        using var deadline = new CancellationTokenSource();
        caller.Cancel();
        var disposed = new ObjectDisposedException("socket");

        var callerFailure = HomeAssistantX.WebSockets.HomeAssistantWebSocketClient.ClassifyConnectFailure(
            disposed, caller.Token, disposal.Token, deadline.Token);

        Assert.IsType<OperationCanceledException>(callerFailure);
        using var timeoutCaller = new CancellationTokenSource();
        using var timeoutDeadline = new CancellationTokenSource();
        timeoutDeadline.Cancel();
        var timeoutFailure = HomeAssistantX.WebSockets.HomeAssistantWebSocketClient.ClassifyConnectFailure(
            disposed, timeoutCaller.Token, disposal.Token, timeoutDeadline.Token);
        Assert.IsType<HomeAssistantConnectionException>(timeoutFailure);
    }

    [Fact]
    public async Task RequestDeadlineIncludesWaitingForTheSharedSendGate()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromMilliseconds(150));
        await client.WebSocket.ConnectAsync();
        var field = typeof(HomeAssistantX.WebSockets.HomeAssistantWebSocketClient)
            .GetField("_sendGate", BindingFlags.Instance | BindingFlags.NonPublic);
        var gate = Assert.IsType<SemaphoreSlim>(field!.GetValue(client.WebSocket));
        await gate.WaitAsync();
        try
        {
            var exception = await Assert.ThrowsAsync<HomeAssistantConnectionException>(
                () => client.WebSocket.PingAsync());
            Assert.IsType<TimeoutException>(exception.InnerException);
        }
        finally
        {
            gate.Release();
        }
    }

    [Fact]
    public async Task ConnectNegotiatesCoalescingAsFirstCommandAndRoutesBatchedMessages()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var result = await client.WebSocket.RequestAsync("test/coalesced");

        Assert.Equal("coalesced", result.GetProperty("value").GetString());
        using var featureCommand = JsonDocument.Parse(
            Assert.IsType<string>(server.GetLastWebSocketCommand("supported_features")));
        Assert.Equal(1, featureCommand.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(
            1,
            featureCommand.RootElement
                .GetProperty("features")
                .GetProperty("coalesce_messages")
                .GetInt32());
    }

    [Fact]
    public async Task UnsupportedFeatureNegotiationFallsBackToOrdinaryMessages()
    {
        using var server = new TestHomeAssistantServer { RejectSupportedFeatures = true };
        using var client = TestClientFactory.Create(server);

        var pong = await client.WebSocket.PingAsync();

        Assert.Equal(JsonValueKind.Null, pong.ValueKind);
        Assert.NotNull(server.GetLastWebSocketCommand("supported_features"));
    }

    [Fact]
    public async Task MalformedFeatureNegotiationIsAProtocolFailure()
    {
        using var server = new TestHomeAssistantServer { ReturnMalformedSupportedFeatures = true };
        using var client = TestClientFactory.Create(server);

        var exception = await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            () => client.WebSocket.ConnectAsync());

        Assert.IsAssignableFrom<JsonException>(exception.InnerException);
        Assert.Contains("supported-features", exception.Message);
    }

    [Fact]
    public async Task CommandIdentifiersRemainMonotonicAcrossConnections()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.WebSocket.PingAsync();
        using var first = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("ping")));
        var firstId = first.RootElement.GetProperty("id").GetInt32();
        await client.WebSocket.DisconnectAsync();
        await client.WebSocket.PingAsync();
        using var second = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("ping")));

        Assert.True(second.RootElement.GetProperty("id").GetInt32() > firstId);
    }

    [Fact]
    public async Task CoalescedBatchCountIsBoundedIndependentlyFromFrameBytes()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(
            server,
            maximumCoalescedWebSocketMessages: 1);

        var exception = await Assert.ThrowsAsync<HomeAssistantConnectionException>(
            () => client.WebSocket.RequestAsync("test/coalesced"));

        var protocolFailure = Assert.IsType<HomeAssistantProtocolException>(exception.InnerException);
        Assert.Contains("message-count limit", protocolFailure.Message);
    }

    [Fact]
    public async Task CoalescedBatchRejectsValuesThatAreNotMessages()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var exception = await Assert.ThrowsAsync<HomeAssistantConnectionException>(
            () => client.WebSocket.RequestAsync("test/malformed_coalesced"));

        var protocolFailure = Assert.IsType<HomeAssistantProtocolException>(exception.InnerException);
        Assert.Contains("non-message value", protocolFailure.Message);
    }

    [Fact]
    public async Task CoalescedBatchRejectsRoutedMessagesWithoutAnIntegerIdentifierBeforeRoutingAnyItem()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var exception = await Assert.ThrowsAsync<HomeAssistantConnectionException>(
            () => client.WebSocket.RequestAsync("test/coalesced_invalid_id"));

        var protocolFailure = Assert.IsType<HomeAssistantProtocolException>(exception.InnerException);
        Assert.Contains("command identifier", protocolFailure.Message);
    }

    [Fact]
    public async Task RejectedOAuthTokenRefreshesBeforeOpeningAFreshWebSocketSession()
    {
        using var server = new TestHomeAssistantServer
        {
            RequiredAccessToken = "refreshed-access-token"
        };
        using var oauth = new HomeAssistantOAuthClient(server.BaseUri);
        using var provider = new RefreshingAccessTokenProvider(
            oauth,
            new Uri("https://app.example.net/"),
            new HomeAssistantOAuthTokens
            {
                AccessToken = "locally-unexpired-but-rejected",
                RefreshToken = "oauth-refresh-token",
                ExpiresInSeconds = 1800,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(20)
            });
        using var client = TestClientFactory.Create(server, accessTokenProvider: provider);

        var result = await client.WebSocket.PingAsync();

        Assert.Equal(JsonValueKind.Null, result.ValueKind);
        Assert.Equal(1, server.OAuthTokenRequestCount);
        Assert.Equal(2, server.WebSocketConnectionCount);
    }

    [Fact]
    public async Task PermanentReconnectAuthenticationFailureStopsTheReconnectEpisode()
    {
        using var server = new TestHomeAssistantServer();
        var provider = new NonRecoveringTokenProvider(TestHomeAssistantServer.AccessToken);
        using var client = TestClientFactory.Create(server, accessTokenProvider: provider);
        using var subscription = await client.Events.SubscribeAsync("state_changed", (_, _) => Task.CompletedTask);
        var faulted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.WebSocket.ConnectionStateChanged += (_, args) =>
        {
            if (args.CurrentState == HomeAssistantX.WebSockets.HomeAssistantConnectionState.Faulted)
            {
                faulted.TrySetResult(true);
            }
        };

        await Task.Delay(100);
        server.RequiredAccessToken = "replacement-token";
        await server.DropWebSocketsAsync();
        await WithTimeoutAsync(faulted.Task);
        await Assert.ThrowsAsync<HomeAssistantAuthenticationException>(
            async () => await WithTimeoutAsync(subscription.Completion));
        var stoppedAt = server.WebSocketConnectionCount;
        await Task.Delay(250);

        Assert.Equal(1, provider.RecoveryCount);
        Assert.Equal(stoppedAt, server.WebSocketConnectionCount);
        Assert.Equal(HomeAssistantX.WebSockets.HomeAssistantConnectionState.Faulted, client.WebSocket.State);
    }

    [Fact]
    public async Task TerminalReconnectFailureCancelsRunningHandlerAndPreservesUpstreamDiagnostics()
    {
        using var server = new TestHomeAssistantServer();
        var diagnostics = new RecordingDiagnosticsSink();
        var provider = new NonRecoveringTokenProvider(TestHomeAssistantServer.AccessToken);
        using var client = TestClientFactory.Create(server, accessTokenProvider: provider, diagnostics: diagnostics);
        var handlerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.Events.SubscribeAsync("state_changed", async (_, token) =>
        {
            handlerStarted.TrySetResult(true);
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
            }
            catch (OperationCanceledException)
            {
                throw new ObjectDisposedException("canceled-handler-resource");
            }
        });

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOffStateJson,
            TestHomeAssistantServer.KitchenLightOnStateJson);
        await WithTimeoutAsync(handlerStarted.Task);
        for (var index = 0; index < 8; index++)
        {
            await server.PublishStateChangeAsync(
                "light.buffered_" + index,
                TestHomeAssistantServer.KitchenLightOffStateJson,
                TestHomeAssistantServer.KitchenLightOnStateJson);
        }

        await Task.Delay(100);
        server.RequiredAccessToken = "replacement-token";
        await server.DropWebSocketsAsync();

        await Assert.ThrowsAsync<HomeAssistantAuthenticationException>(
            async () => await WithTimeoutAsync(subscription.Completion));
        Assert.DoesNotContain(diagnostics.Events, value => value.Name == "subscription.handler_failed");
        Assert.Contains(diagnostics.Events, value => value.Name == "websocket.reconnect_authentication_failed");
    }

    [Theory]
    [InlineData(true, null)]
    [InlineData(false, "invalid_format")]
    public async Task PermanentReconnectNegotiationFailureStopsTheReconnectEpisode(
        bool malformedResponse,
        string? commandErrorCode)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Events.SubscribeAsync("state_changed", (_, _) => Task.CompletedTask);
        var faulted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.WebSocket.ConnectionStateChanged += (_, args) =>
        {
            if (args.CurrentState == HomeAssistantX.WebSockets.HomeAssistantConnectionState.Faulted
                && (args.Exception is HomeAssistantProtocolException || args.Exception is HomeAssistantCommandException))
            {
                faulted.TrySetResult(true);
            }
        };

        server.ReturnMalformedSupportedFeatures = malformedResponse;
        server.SupportedFeaturesErrorCode = commandErrorCode;
        await server.DropWebSocketsAsync();
        await WithTimeoutAsync(faulted.Task);
        if (malformedResponse)
        {
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(
                async () => await WithTimeoutAsync(subscription.Completion));
        }
        else
        {
            await Assert.ThrowsAsync<HomeAssistantCommandException>(
                async () => await WithTimeoutAsync(subscription.Completion));
        }
        var stoppedAt = server.WebSocketConnectionCount;
        await Task.Delay(250);

        Assert.Equal(stoppedAt, server.WebSocketConnectionCount);
        Assert.Equal(HomeAssistantX.WebSockets.HomeAssistantConnectionState.Faulted, client.WebSocket.State);
    }

    [Fact]
    public async Task EventSubscriptionRejectsNullRequiredDataDictionary()
    {
        using var server = new TestHomeAssistantServer { PublishNullStateEventData = true };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Events.SubscribeAsync("state_changed", (_, _) => Task.CompletedTask);

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOffStateJson,
            TestHomeAssistantServer.KitchenLightOnStateJson);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            async () => await WithTimeoutAsync(subscription.Completion));
    }

    [Fact]
    public async Task AuthenticatesPingsAndReassemblesFragmentedResponses()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.WebSocket.ConnectAsync();
        var pong = await client.WebSocket.PingAsync();
        var states = await client.WebSocket.RequestAsync("get_states");

        Assert.Equal(JsonValueKind.Null, pong.ValueKind);
        Assert.Equal(2, states.GetArrayLength());
        Assert.Equal("sensor.kitchen_temperature", states[0].GetProperty("entity_id").GetString());
    }

    [Fact]
    public async Task MultiplexesConcurrentCommandsByIdentifierRatherThanArrivalOrder()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var slow = client.WebSocket.RequestAsync("test/slow");
        var fast = client.WebSocket.RequestAsync("test/fast");
        var results = await Task.WhenAll(slow, fast);

        Assert.Equal("slow", results[0].GetProperty("value").GetString());
        Assert.Equal("fast", results[1].GetProperty("value").GetString());
    }

    [Fact]
    public async Task SurfacesStructuredHomeAssistantCommandErrors()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var exception = await Assert.ThrowsAsync<HomeAssistantCommandException>(
            () => client.WebSocket.RequestAsync("test/error"));

        Assert.Equal("service_validation_error", exception.Code);
        Assert.Equal("unsupported_option", exception.TranslationKey);
        Assert.Equal("Option is not supported.", exception.Message);
    }

    [Fact]
    public async Task EventSubscriptionDeliversStateChangesAndStopsCleanly()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var received = new TaskCompletionSource<HomeAssistantX.Models.HomeAssistantEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var subscription = await client.Events.SubscribeAsync("state_changed", (value, _) =>
        {
            received.TrySetResult(value);
            return Task.CompletedTask;
        });

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOffStateJson,
            TestHomeAssistantServer.KitchenLightOnStateJson);

        var eventValue = await WithTimeoutAsync(received.Task);
        Assert.Equal("light.kitchen", eventValue.Data["entity_id"].GetString());
        Assert.Equal("preserved", eventValue.AdditionalData["custom_event_field"].GetString());
        Assert.Equal("test-trace", eventValue.Context!.AdditionalData["trace_hint"].GetString());
        await subscription.StopAsync();
        await subscription.Completion;
    }

    [Fact]
    public async Task HandlerFailureFaultsCompletionAndRemovesTheServerSubscription()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Events.SubscribeAsync(
            "state_changed",
            (_, _) => throw new InvalidOperationException("consumer failed"));

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOffStateJson,
            TestHomeAssistantServer.KitchenLightOnStateJson);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await WithTimeoutAsync(subscription.Completion));
        Assert.Equal("consumer failed", exception.Message);
    }

    [Fact]
    public async Task ImmediateReconnectIsNotCorruptedByStaleReceiveLoopCleanup()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.Events.SubscribeAsync("state_changed", (_, _) =>
        {
            received.TrySetResult(true);
            return Task.CompletedTask;
        });

        for (var iteration = 0; iteration < 20; iteration++)
        {
            await client.WebSocket.DisconnectAsync();
            await client.WebSocket.ConnectAsync();
            await client.WebSocket.PingAsync();
        }

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOffStateJson,
            TestHomeAssistantServer.KitchenLightOnStateJson);

        Assert.True(await WithTimeoutAsync(received.Task));
        Assert.Equal(HomeAssistantX.WebSockets.HomeAssistantConnectionState.Connected, client.WebSocket.State);
    }

    [Fact]
    public async Task StopDuringReconnectActivationUnsubscribesTheReplacementServerRegistration()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Events.SubscribeAsync("state_changed", (_, _) => Task.CompletedTask);
        server.PauseNextSubscription();

        await server.DropWebSocketsAsync();
        await WithTimeoutAsync(server.WaitForPausedSubscriptionAsync());
        var stop = subscription.StopAsync();
        Assert.False(stop.IsCompleted);

        server.ReleasePausedSubscription();
        await WithTimeoutAsync(stop);

        Assert.Equal(1, server.UnsubscribeCommandCount);
        Assert.Equal(0, server.InvalidUnsubscribeCommandCount);
        Assert.Equal(server.LastSubscriptionSessionId, server.LastUnsubscribeSessionId);
        await subscription.Completion;
    }

    [Fact]
    public async Task CancellationBoundsTheCallerWithoutAbandoningSubscriptionCleanup()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Events.SubscribeAsync("state_changed", (_, _) => Task.CompletedTask);
        server.PauseNextSubscription();

        await server.DropWebSocketsAsync();
        await WithTimeoutAsync(server.WaitForPausedSubscriptionAsync());
        using var cancellation = new CancellationTokenSource();
        var stop = subscription.StopAsync(cancellation.Token);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop);

        server.ReleasePausedSubscription();
        await WithTimeoutAsync(server.WaitForUnsubscribeAsync());
        await WithTimeoutAsync(subscription.StopAsync());

        Assert.Equal(1, server.UnsubscribeCommandCount);
        Assert.Equal(0, server.InvalidUnsubscribeCommandCount);
        Assert.Equal(server.LastSubscriptionSessionId, server.LastUnsubscribeSessionId);
        await subscription.Completion;
    }

    [Fact]
    public async Task StopCallerIsBoundedWhenTheServerNeverAcknowledgesUnsubscribe()
    {
        using var server = new TestHomeAssistantServer { IgnoreUnsubscribeAcknowledgement = true };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Events.SubscribeAsync("state_changed", (_, _) => Task.CompletedTask);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

        var stop = subscription.StopAsync(cancellation.Token);
        await WithTimeoutAsync(server.WaitForUnsubscribeAsync());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => stop);
        await WithTimeoutAsync(subscription.Completion);

        Assert.Equal(1, server.UnsubscribeCommandCount);
        Assert.Equal(0, server.InvalidUnsubscribeCommandCount);
    }

    [Fact]
    public async Task PreCanceledStopDoesNotPreventASecondCleanupAttempt()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Events.SubscribeAsync("state_changed", (_, _) => Task.CompletedTask);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => subscription.StopAsync(cancellation.Token));
        await subscription.StopAsync();

        Assert.Equal(1, server.UnsubscribeCommandCount);
        Assert.Equal(0, server.InvalidUnsubscribeCommandCount);
        Assert.Equal(server.LastSubscriptionSessionId, server.LastUnsubscribeSessionId);
        await subscription.Completion;
    }

    [Fact]
    public async Task CanceledInitialActivationCleansUpAnAmbiguousServerSubscription()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        await client.WebSocket.ConnectAsync();
        server.PauseNextSubscription();
        using var cancellation = new CancellationTokenSource();

        var activation = client.Events.SubscribeAsync(
            "state_changed",
            (_, _) => Task.CompletedTask,
            cancellation.Token);
        await WithTimeoutAsync(server.WaitForPausedSubscriptionAsync());
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => activation);
        server.ReleasePausedSubscription();
        await WithTimeoutAsync(server.WaitForUnsubscribeAsync());

        Assert.Equal(1, server.UnsubscribeCommandCount);
        Assert.Equal(0, server.InvalidUnsubscribeCommandCount);
        Assert.Equal(server.LastSubscriptionSessionId, server.LastUnsubscribeSessionId);
    }

    [Fact]
    public async Task TimedOutInitialActivationCleansUpAnAmbiguousServerSubscription()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, requestTimeout: TimeSpan.FromMilliseconds(500));
        await client.WebSocket.ConnectAsync();
        server.PauseNextSubscription();

        var activation = client.Events.SubscribeAsync(
            "state_changed",
            (_, _) => Task.CompletedTask);
        await WithTimeoutAsync(server.WaitForPausedSubscriptionAsync());

        await Assert.ThrowsAsync<HomeAssistantConnectionException>(() => activation);
        server.ReleasePausedSubscription();
        await WithTimeoutAsync(server.WaitForUnsubscribeAsync());

        Assert.Equal(1, server.UnsubscribeCommandCount);
        Assert.Equal(0, server.InvalidUnsubscribeCommandCount);
        Assert.Equal(server.LastSubscriptionSessionId, server.LastUnsubscribeSessionId);
    }

    [Fact]
    public async Task FluentServiceAndRegistryClientsUseTypedContracts()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var call = HomeAssistantServiceCall.Create("light", "turn_on")
            .ForArea("kitchen")
            .WithData("brightness_pct", 60)
            .WithResponse();

        var service = await client.Services.CallAsync(call);
        var registry = await client.Registries.GetSnapshotAsync();

        Assert.Equal("service-context", service.Context!.Id);
        Assert.True(service.Response!.Value.GetProperty("accepted").GetBoolean());
        Assert.Equal("Kitchen", Assert.Single(registry.Areas).Name);
        Assert.Equal("Ground", Assert.Single(registry.Floors).Name);
        Assert.Equal("Evotec", Assert.Single(registry.Devices).Manufacturer);
        Assert.Contains(registry.Entities, entity => entity.EntityId == "sensor.kitchen_temperature");
        Assert.Contains(registry.Entities, entity => entity.EntityId == "light.kitchen");
        Assert.Equal("loaded", Assert.Single(registry.ConfigEntries).State);

        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.Equal("call_service", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("kitchen", body.RootElement.GetProperty("target").GetProperty("area_id")[0].GetString());
        Assert.Equal(60, body.RootElement.GetProperty("service_data").GetProperty("brightness_pct").GetInt32());
    }

    [Fact]
    public async Task MutableTargetsAreRevalidatedAndNormalizedBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = new HomeAssistantTarget { EntityIds = new[] { " light.kitchen " } };

        await client.Services.CallAsync(HomeAssistantServiceCall.Create("light", "turn_on").ForTarget(target));

        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.Equal("light.kitchen", body.RootElement.GetProperty("target").GetProperty("entity_id")[0].GetString());
        target.EntityIds = new[] { " " };
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.Services.CallAsync(HomeAssistantServiceCall.Create("light", "turn_on").ForTarget(target)));
        target.EntityIds = new[] { "_light.kitchen" };
        await Assert.ThrowsAsync<ArgumentException>(
            () => client.Services.CallAsync(HomeAssistantServiceCall.Create("light", "turn_on").ForTarget(target)));
        Assert.Throws<ArgumentException>(() => HomeAssistantTarget.ForEntity("LIGHT.kitchen"));
    }

    [Fact]
    public async Task DefaultServiceCallOmitsOptionalWebSocketFields()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Services.CallAsync(HomeAssistantServiceCall.Create("homeassistant", "update_entity"));

        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.False(body.RootElement.TryGetProperty("service_data", out _));
        Assert.False(body.RootElement.TryGetProperty("target", out _));
        Assert.False(body.RootElement.TryGetProperty("return_response", out _));
    }

    [Fact]
    public async Task DocumentedSystemCommandsHaveFirstClassWebSocketWrappers()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var target = new HomeAssistantTarget { EntityIds = new[] { " light.kitchen " } };

        var configuration = await client.System.GetConfigurationAsync();
        var services = await client.Services.GetCatalogWebSocketAsync();
        var panels = await client.System.GetPanelsAsync();
        var validation = await client.System.ValidateConfigAsync(action: new[] { new { service = "light.turn_on" } });
        var extracted = await client.System.ExtractFromTargetAsync(target);
        var triggers = await client.System.GetTriggersForTargetAsync(target);
        var conditions = await client.System.GetConditionsForTargetAsync(target);
        var targetServices = await client.System.GetServicesForTargetAsync(target);
        var displayRegistry = await client.System.GetEntityRegistryForDisplayAsync();
        var exposures = await client.System.GetExposedEntitiesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => client.System.SetEntityExposureAsync("light.Kitchen", "cloud", true));
        var changedExposure = await client.System.SetEntityExposureAsync(" light.kitchen ", " cloud ", true);
        var signedPath = await client.System.SignPathAsync("/api/camera_proxy/camera.front", TimeSpan.FromMinutes(1));
        var longLivedToken = await client.System.CreateLongLivedAccessTokenAsync("Contract test", 30);
        var conversation = await client.System.ProcessConversationAsync("Turn on the kitchen light");
        var fired = await client.Events.FireAsync("homeassistantx_test");

        Assert.Equal("Test Home", configuration.LocationName);
        Assert.True(services.GetProperty("light").TryGetProperty("turn_on", out _));
        Assert.Equal("Overview", panels.GetProperty("lovelace").GetProperty("title").GetString());
        Assert.True(validation.TryGetProperty("action", out _));
        Assert.Equal("light.kitchen", extracted.GetProperty("referenced_entities")[0].GetString());
        Assert.Equal(1, triggers.GetArrayLength());
        Assert.Equal(1, conditions.GetArrayLength());
        Assert.Equal(1, targetServices.GetArrayLength());
        Assert.True(displayRegistry.TryGetProperty("entities", out _));
        Assert.True(exposures.GetProperty("exposed_entities").GetProperty("light.kitchen").GetProperty("conversation").GetBoolean());
        Assert.Equal(JsonValueKind.Null, changedExposure.ValueKind);
        Assert.Contains("authSig=signed", signedPath);
        Assert.Equal("fake-long-lived-token", longLivedToken);
        Assert.Equal("conversation-1", conversation.GetProperty("conversation_id").GetString());
        Assert.Equal("event-context", fired.GetProperty("context").GetProperty("id").GetString());

        using var exposureCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("homeassistant/expose_entity")));
        Assert.Equal("light.kitchen", exposureCommand.RootElement.GetProperty("entity_ids")[0].GetString());
        Assert.Equal("cloud", exposureCommand.RootElement.GetProperty("assistants")[0].GetString());
        using var targetCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("get_services_for_target")));
        Assert.True(targetCommand.RootElement.GetProperty("expand_group").GetBoolean());
        using var extractCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("extract_from_target")));
        Assert.False(extractCommand.RootElement.GetProperty("expand_group").GetBoolean());
        using var fireCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("fire_event")));
        Assert.False(fireCommand.RootElement.TryGetProperty("event_data", out _));
        using var conversationCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("conversation/process")));
        Assert.False(conversationCommand.RootElement.TryGetProperty("language", out _));
        using var validationCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("validate_config")));
        Assert.True(validationCommand.RootElement.TryGetProperty("action", out _));
        Assert.False(validationCommand.RootElement.TryGetProperty("trigger", out _));
        target.EntityIds = new[] { " " };
        await Assert.ThrowsAsync<ArgumentException>(() => client.System.ExtractFromTargetAsync(target));
        await Assert.ThrowsAsync<ArgumentException>(() => client.System.GetTriggersForTargetAsync(target));
    }

    [Fact]
    public async Task SignPathRejectsExpiryValuesOutsideTheWireIntegerRange()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => client.System.SignPathAsync("/api/camera_proxy/camera.front", TimeSpan.MaxValue));
        var signed = await client.System.SignPathAsync(
            "/api/camera_proxy/camera.front",
            TimeSpan.FromSeconds(int.MaxValue - 0.25));

        Assert.Contains("authSig=", signed);
        using var command = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("auth/sign_path")));
        Assert.Equal(int.MaxValue, command.RootElement.GetProperty("expires").GetInt32());
    }

    [Fact]
    public async Task WebSocketConversationRejectsExplicitBlankSelectorsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.System.ProcessConversationAsync("hello", language: " "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.System.ProcessConversationAsync("hello", agentId: " "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.System.ProcessConversationAsync("hello", conversationId: " "));

        Assert.Null(server.GetLastWebSocketCommand("conversation/process"));
    }

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(task, winner);
        return await task;
    }

    private static async Task WithTimeoutAsync(Task task)
    {
        var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.Same(task, winner);
        await task;
    }

    private sealed class NonRecoveringTokenProvider : IHomeAssistantAccessTokenProvider, IHomeAssistantAccessTokenRecovery
    {
        private readonly string _token;

        internal NonRecoveringTokenProvider(string token)
        {
            _token = token;
        }

        internal int RecoveryCount { get; private set; }

        public Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_token);

        public Task RecoverAccessTokenAsync(string rejectedAccessToken, CancellationToken cancellationToken = default)
        {
            RecoveryCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDiagnosticsSink : IHomeAssistantDiagnosticsSink
    {
        private readonly ConcurrentQueue<HomeAssistantDiagnosticEvent> _events = new();

        internal IReadOnlyList<HomeAssistantDiagnosticEvent> Events => _events.ToArray();

        public void Write(HomeAssistantDiagnosticEvent diagnosticEvent) => _events.Enqueue(diagnosticEvent);
    }
}
#endif
