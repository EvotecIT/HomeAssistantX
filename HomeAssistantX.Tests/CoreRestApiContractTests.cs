using System.Text;
using System.Text.Json;
using HomeAssistantX.Rest;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class CoreRestApiContractTests
{
    [Fact]
    public async Task DocumentedReadOnlyRestFamiliesUseTheirRealWireContracts()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var start = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(2);

        var components = await client.Rest.GetComponentsAsync();
        var events = await client.Rest.GetEventTypesAsync();
        var history = await client.Rest.GetHistoryAsync(new HomeAssistantHistoryQuery("sensor.kitchen_temperature")
        {
            StartTime = start,
            EndTime = end,
            MinimalResponse = true,
            NoAttributes = true,
            SignificantChangesOnly = true
        });
        var logbook = await client.Rest.GetLogbookAsync(new HomeAssistantLogbookQuery
        {
            StartTime = start,
            EndTime = end,
            EntityId = "light.kitchen"
        });
        var errorLog = await client.Rest.GetErrorLogAsync();
        var camera = await client.Rest.GetCameraImageAsync("camera.front");
        var calendars = await client.Rest.GetCalendarsAsync();
        var calendarEvents = await client.Rest.GetCalendarEventsAsync("calendar.home", start, end);

        Assert.Contains("recorder", components);
        Assert.Equal(5, Assert.Single(events).ListenerCount);
        Assert.Equal("sensor.kitchen_temperature", Assert.Single(Assert.Single(history)).EntityId);
        Assert.Equal("turned on", Assert.Single(logbook).Message);
        Assert.Equal("test integration warning", errorLog);
        Assert.Equal("test-image-bytes", Encoding.UTF8.GetString(camera));
        Assert.Equal("calendar.home", Assert.Single(calendars).EntityId);
        Assert.Equal("Dinner", Assert.Single(calendarEvents).Summary);
    }

    [Fact]
    public async Task DocumentedRestCommandsSerializeAndDecodeTheirContracts()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var state = await client.States.SetAsync(
            "sensor.virtual",
            new HomeAssistantStateUpdate("ready")
            {
                Attributes = new Dictionary<string, object?> { ["friendly_name"] = "Virtual" }
            });
        var removed = await client.States.DeleteAsync("sensor.virtual");
        var fired = await client.Rest.FireEventAsync(
            "homeassistantx_test",
            new Dictionary<string, object?> { ["source"] = "contract" });
        var rendered = await client.Rest.RenderTemplateAsync(
            "{{ value }}",
            new Dictionary<string, object?> { ["value"] = "rendered value" });
        var config = await client.Rest.CheckConfigurationAsync();
        var intent = await client.Rest.HandleIntentAsync(
            new Dictionary<string, object?> { ["name"] = "HassTurnOn" });
        var conversation = await client.Rest.ProcessConversationAsync("Turn on the kitchen light", "en");

        Assert.Equal("ready", state.State);
        Assert.Equal("Entity removed.", removed.GetProperty("message").GetString());
        Assert.Contains("fired", fired.GetProperty("message").GetString());
        Assert.Equal("rendered value", rendered);
        Assert.True(config.IsValid);
        Assert.Equal("Done", intent.GetProperty("response").GetProperty("speech").GetProperty("plain").GetProperty("speech").GetString());
        Assert.Equal("conversation-1", conversation.GetProperty("conversation_id").GetString());
    }

    [Fact]
    public async Task HistoryQueryEscapesTimestampsAndUsesPresenceFlags()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Rest.GetHistoryAsync(new HomeAssistantHistoryQuery("sensor.kitchen_temperature")
        {
            StartTime = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero),
            MinimalResponse = true,
            NoAttributes = true
        });

        Assert.Contains("filter_entity_id=sensor.kitchen_temperature", server.LastRequestPath);
        Assert.Contains("minimal_response", server.LastRequestPath);
        Assert.Contains("no_attributes", server.LastRequestPath);
        Assert.DoesNotContain("minimal_response=", server.LastRequestPath);
    }
}
