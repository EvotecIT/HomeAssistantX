using HomeAssistantX.Operations;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class OperationalSnapshotContractTests
{
#if !NET472
    [Fact]
    public async Task SnapshotCombinesReadOnlyCountsWithoutReturningHomeData()
    {
        using var server = new TestHomeAssistantServer
        {
            ComponentsResponseJson = "[\"api\",\"websocket_api\",\"repairs\",\"system_log\",\"update\"]"
        };
        server.SetStates("["
            + "{\"entity_id\":\"light.kitchen\",\"state\":\"unavailable\",\"attributes\":{}},"
            + "{\"entity_id\":\"sensor.office\",\"state\":\"unknown\",\"attributes\":{}},"
            + "{\"entity_id\":\"update.core\",\"state\":\"on\",\"attributes\":{}},"
            + "{\"entity_id\":\"update.other\",\"state\":\"off\",\"attributes\":{}}]");
        using var client = TestClientFactory.Create(server);
        var before = DateTimeOffset.UtcNow;

        var snapshot = await client.Operations.GetOperationalSnapshotAsync();

        Assert.InRange(snapshot.ObservedAt, before, DateTimeOffset.UtcNow);
        Assert.Equal("2026.8.3", snapshot.Capabilities.CoreVersion);
        Assert.Equal(4, snapshot.EntityCount);
        Assert.Equal(1, snapshot.UnavailableEntityCount);
        Assert.Equal(1, snapshot.UnknownEntityCount);
        Assert.Equal(1, snapshot.AvailableUpdateCount);
        Assert.Equal(1, snapshot.ActiveRepairIssueCount);
        Assert.Equal(1, snapshot.SystemLogEntryCount);
        Assert.False(snapshot.IsPartial);
        Assert.Empty(snapshot.UnavailableSections);
        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task OptionalFailureRemainsVisibleWithoutDiscardingSuccessfulReads()
    {
        using var server = new TestHomeAssistantServer
        {
            ComponentsResponseJson = "[\"api\",\"websocket_api\",\"repairs\"]",
            RepairsListErrorCode = "unavailable"
        };
        using var client = TestClientFactory.Create(server);

        var snapshot = await client.Operations.GetOperationalSnapshotAsync();

        Assert.Equal(2, snapshot.EntityCount);
        Assert.Null(snapshot.AvailableUpdateCount);
        Assert.Null(snapshot.ActiveRepairIssueCount);
        Assert.Null(snapshot.SystemLogEntryCount);
        Assert.True(snapshot.IsPartial);
        Assert.Equal("repairs", Assert.Single(snapshot.UnavailableSections));
        Assert.Equal(HomeAssistantCapabilityAvailability.NotInstalled,
            Assert.Single(snapshot.Capabilities.Capabilities, capability => capability.Name == "system_log").Availability);
    }
#endif

    [Fact]
    public async Task CancellationIsNeverReportedAsAnOptionalSectionFailure()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Operations.GetOperationalSnapshotAsync(cancellation.Token));
    }
}
