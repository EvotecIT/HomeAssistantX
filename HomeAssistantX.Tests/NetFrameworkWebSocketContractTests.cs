#if NET472
using HomeAssistantX.Authentication;
using HomeAssistantX.Configuration;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Services;
using HomeAssistantX.States;
using HomeAssistantX.Tests.Infrastructure;
using HomeAssistantX.WebSockets;

namespace HomeAssistantX.Tests;

public sealed class NetFrameworkWebSocketContractTests
{
    [Fact]
    public async Task NetFrameworkAuthenticatesSubscribesAndReconcilesAfterReconnect()
    {
        using var server = await CrossProcessHomeAssistantServer.StartAsync();
        var options = new HomeAssistantClientOptions(
            server.BaseUri,
            new StaticAccessTokenProvider(TestHomeAssistantServer.AccessToken))
        {
            RequestTimeout = TimeSpan.FromSeconds(5),
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ReconnectMinimumDelay = TimeSpan.FromMilliseconds(10),
            ReconnectMaximumDelay = TimeSpan.FromMilliseconds(50)
        };
        using var client = new HomeAssistantClient(options);

        await client.WebSocket.ConnectAsync();
        var pong = await client.WebSocket.PingAsync();
        var rawStates = await client.WebSocket.RequestAsync("get_states");
        Assert.Equal(System.Text.Json.JsonValueKind.Null, pong.ValueKind);
        Assert.Equal(2, rawStates.GetArrayLength());

        var slow = client.WebSocket.RequestAsync("test/slow");
        var fast = client.WebSocket.RequestAsync("test/fast");
        var responses = await Task.WhenAll(slow, fast);
        Assert.Equal("slow", responses[0].GetProperty("value").GetString());
        Assert.Equal("fast", responses[1].GetProperty("value").GetString());

        var service = await client.Services.CallAsync(
            HomeAssistantServiceCall.Create("light", "turn_on").ForEntity("light.kitchen"));
        Assert.True(service.Response!.Value.GetProperty("accepted").GetBoolean());

        var reconciled = new TaskCompletionSource<HomeAssistantStateChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.States.SubscribeAsync(HomeAssistantStateFilter.All, (change, _) =>
        {
            if (change.IsReconciliation && change.IsRemoval && change.EntityId == "sensor.kitchen_temperature")
            {
                reconciled.TrySetResult(change);
            }

            return Task.CompletedTask;
        });
        Assert.Equal("on", client.States.Snapshot["light.kitchen"].State);

        await server.SendCommandAsync("SET_RECONNECT_STATES", "STATES_SET");
        await server.SendCommandAsync("DROP", "DROPPED");
        await WithTimeoutAsync(reconciled.Task);

        Assert.Equal(HomeAssistantConnectionState.Connected, client.WebSocket.State);
        Assert.Equal("on", client.States.Snapshot["light.kitchen"].State);
        Assert.False(client.States.Snapshot.ContainsKey("sensor.kitchen_temperature"));
    }

    [Fact]
    public async Task NetFrameworkCanceledActivationCleansUpAnAmbiguousServerSubscription()
    {
        using var server = await CrossProcessHomeAssistantServer.StartAsync();
        var options = new HomeAssistantClientOptions(
            server.BaseUri,
            new StaticAccessTokenProvider(TestHomeAssistantServer.AccessToken))
        {
            RequestTimeout = TimeSpan.FromSeconds(5),
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };
        using var client = new HomeAssistantClient(options);
        await client.WebSocket.ConnectAsync();
        await server.SendCommandAsync("PAUSE_NEXT_SUBSCRIPTION", "PAUSE_CONFIGURED");
        using var cancellation = new CancellationTokenSource();

        var activation = client.Events.SubscribeAsync(
            "state_changed",
            (_, _) => Task.CompletedTask,
            cancellation.Token);
        await server.SendCommandAsync("WAIT_FOR_PAUSED_SUBSCRIPTION", "SUBSCRIPTION_PAUSED");
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => activation);
        await server.SendCommandAsync("RELEASE_PAUSED_SUBSCRIPTION", "SUBSCRIPTION_RELEASED");
        await server.SendCommandAsync("WAIT_FOR_UNSUBSCRIBE", "UNSUBSCRIBED");
    }

    [Fact]
    public async Task NetFrameworkTimedOutActivationCleansUpAnAmbiguousServerSubscription()
    {
        using var server = await CrossProcessHomeAssistantServer.StartAsync();
        var options = new HomeAssistantClientOptions(
            server.BaseUri,
            new StaticAccessTokenProvider(TestHomeAssistantServer.AccessToken))
        {
            RequestTimeout = TimeSpan.FromMilliseconds(500),
            ConnectTimeout = TimeSpan.FromSeconds(5)
        };
        using var client = new HomeAssistantClient(options);
        await client.WebSocket.ConnectAsync();
        await server.SendCommandAsync("PAUSE_NEXT_SUBSCRIPTION", "PAUSE_CONFIGURED");

        var activation = client.Events.SubscribeAsync(
            "state_changed",
            (_, _) => Task.CompletedTask);
        await server.SendCommandAsync("WAIT_FOR_PAUSED_SUBSCRIPTION", "SUBSCRIPTION_PAUSED");

        await Assert.ThrowsAsync<HomeAssistantConnectionException>(() => activation);
        await server.SendCommandAsync("RELEASE_PAUSED_SUBSCRIPTION", "SUBSCRIPTION_RELEASED");
        await server.SendCommandAsync("WAIT_FOR_UNSUBSCRIBE", "UNSUBSCRIBED");
    }

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        var completed = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(task, completed);
        return await task;
    }
}
#endif
