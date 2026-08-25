using HomeAssistantX.States;
using Xunit.Abstractions;

namespace HomeAssistantX.LiveTests;

public sealed class LiveHomeAssistantTests
{
    private readonly ITestOutputHelper _output;

    public LiveHomeAssistantTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [LiveFact]
    public async Task ReadOnlyRestAndWebSocketContractsWorkAgainstARealHomeAssistant()
    {
        var baseUri = new Uri(Environment.GetEnvironmentVariable("HOME_ASSISTANT_URL")!, UriKind.Absolute);
        var token = Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN")!;
        using var client = HomeAssistantClient.Create(baseUri, token);

        var api = await client.Rest.CheckApiAsync();
        var configuration = await client.Rest.GetConfigurationAsync();
        var restStates = await client.States.GetAllAsync();
        var serviceCatalog = await client.Services.GetCatalogAsync();
        await client.WebSocket.ConnectAsync();
        var pong = await client.WebSocket.PingAsync();
        var webSocketStates = await client.WebSocket.RequestAsync("get_states");
        var registries = await client.Registries.GetSnapshotAsync();
        using var subscription = await client.States.SubscribeAsync(
            HomeAssistantStateFilter.All,
            (_, _) => Task.CompletedTask);
        await subscription.StopAsync();

        Assert.False(string.IsNullOrWhiteSpace(api.Message));
        Assert.False(string.IsNullOrWhiteSpace(configuration.Version));
        Assert.NotEmpty(restStates);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, serviceCatalog.ValueKind);
        Assert.NotEmpty(registries.Entities);
        Assert.NotEmpty(registries.Devices);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, pong.ValueKind);
        Assert.True(webSocketStates.GetArrayLength() > 0);
        _output.WriteLine("Home Assistant {0}: REST states={1}, WebSocket states={2}, registry entities={3}",
            configuration.Version,
            restStates.Count,
            webSocketStates.GetArrayLength(),
            registries.Entities.Count);
    }
}

internal sealed class LiveFactAttribute : FactAttribute
{
    public LiveFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOME_ASSISTANT_URL"))
            || string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HOME_ASSISTANT_TOKEN")))
        {
            Skip = "Set HOME_ASSISTANT_URL and HOME_ASSISTANT_TOKEN to run read-only live validation.";
        }
    }
}
