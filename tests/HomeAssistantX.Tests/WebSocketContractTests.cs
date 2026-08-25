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
        var subscription = await client.Events.SubscribeAsync(
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
        Assert.Equal("sensor.kitchen_temperature", Assert.Single(registry.Entities).EntityId);
        Assert.Equal("loaded", Assert.Single(registry.ConfigEntries).State);

        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.Equal("call_service", body.RootElement.GetProperty("type").GetString());
        Assert.Equal("kitchen", body.RootElement.GetProperty("target").GetProperty("area_id")[0].GetString());
        Assert.Equal(60, body.RootElement.GetProperty("service_data").GetProperty("brightness_pct").GetInt32());
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
