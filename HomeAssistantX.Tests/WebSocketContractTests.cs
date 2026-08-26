#if NET10_0
using System.Collections.Concurrent;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class WebSocketContractTests
{
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
        var target = HomeAssistantTarget.ForEntity("light.kitchen");

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
        var changedExposure = await client.System.SetEntityExposureAsync("light.kitchen", "cloud", true);
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
}
#endif
