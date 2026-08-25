#if NET10_0
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.States;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class StateClientContractTests
{
    [Fact]
    public async Task BuffersEventsReceivedBetweenSubscriptionAndInitialSnapshot()
    {
        using var server = new TestHomeAssistantServer { SendStateChangeBeforeSnapshot = true };
        using var client = TestClientFactory.Create(server);

        await client.States.InitializeAsync();

        Assert.Equal("on", client.States.Snapshot["light.kitchen"].State);
        Assert.Equal(180, client.States.Snapshot["light.kitchen"].Attributes["brightness"].GetInt32());
    }

    [Fact]
    public async Task MaintainsSnapshotAndAppliesEntityRemovalFromPushEvents()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var received = new TaskCompletionSource<HomeAssistantStateChange>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.States.SubscribeAsync(
            HomeAssistantStateFilter.ForDomains("light"),
            (change, _) =>
            {
                received.TrySetResult(change);
                return Task.CompletedTask;
            });

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOffStateJson,
            null);
        var change = await WithTimeoutAsync(received.Task);

        Assert.True(change.IsRemoval);
        Assert.False(client.States.Snapshot.ContainsKey("light.kitchen"));
        Assert.True(client.States.Snapshot.ContainsKey("sensor.kitchen_temperature"));
    }

    [Fact]
    public async Task ReconnectRestoresSubscriptionAndReconcilesMissedState()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var changes = new List<HomeAssistantStateChange>();
        var reconciled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.States.SubscribeAsync(HomeAssistantStateFilter.All, (change, _) =>
        {
            lock (changes)
            {
                changes.Add(change);
                if (changes.Count(value => value.IsReconciliation) >= 2)
                {
                    reconciled.TrySetResult(true);
                }
            }

            return Task.CompletedTask;
        });
        server.SetStates("[" + TestHomeAssistantServer.KitchenLightOnStateJson + "]");
        server.FailNextSubscription();

        await server.DropWebSocketsAsync();
        await WithTimeoutAsync(reconciled.Task);

        Assert.True(server.WebSocketConnectionCount >= 3);
        Assert.Equal("on", client.States.Snapshot["light.kitchen"].State);
        Assert.False(client.States.Snapshot.ContainsKey("sensor.kitchen_temperature"));
        lock (changes)
        {
            Assert.Contains(changes, value => value.EntityId == "light.kitchen" && value.IsReconciliation);
            Assert.Contains(changes, value => value.EntityId == "sensor.kitchen_temperature" && value.IsRemoval && value.IsReconciliation);
        }
    }

    [Fact]
    public async Task BoundedStateSubscriptionFaultsWhenConsumerCannotKeepUp()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server, subscriptionBufferCapacity: 1);
        var handlerStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseHandler = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.States.SubscribeAsync(HomeAssistantStateFilter.All, async (_, _) =>
        {
            handlerStarted.TrySetResult(true);
            await releaseHandler.Task;
        });

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOffStateJson,
            TestHomeAssistantServer.KitchenLightOnStateJson);
        await WithTimeoutAsync(handlerStarted.Task);
        Assert.Equal("on", client.States.Snapshot["light.kitchen"].State);

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOnStateJson,
            TestHomeAssistantServer.KitchenLightOffStateJson);
        await WaitUntilAsync(() => client.States.Snapshot["light.kitchen"].State == "off");

        await server.PublishStateChangeAsync(
            "light.kitchen",
            TestHomeAssistantServer.KitchenLightOffStateJson,
            TestHomeAssistantServer.KitchenLightOnStateJson);
        await WaitUntilAsync(() => client.States.Snapshot["light.kitchen"].State == "on");
        releaseHandler.TrySetResult(true);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(
            async () => await WithTimeoutAsync(subscription.Completion));
    }

    private static async Task WaitUntilAsync(Func<bool> predicate)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        while (!predicate())
        {
            Assert.True(DateTime.UtcNow < deadline, "The expected state transition was not observed.");
            await Task.Delay(10);
        }
    }

    private static async Task<T> WithTimeoutAsync<T>(Task<T> task)
    {
        var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, winner);
        return await task;
    }

    private static async Task WithTimeoutAsync(Task task)
    {
        var winner = await Task.WhenAny(task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(task, winner);
        await task;
    }
}
#endif
