using System.Text;
using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Rest;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class CoreRestApiContractTests
{
    [Fact]
    public void ExtensionDataPreservesCaseDistinctUnknownFields()
    {
        var configuration = JsonSerializer.Deserialize<HomeAssistantConfiguration>(
            "{\"location_name\":\"Home\",\"future\":1,\"Future\":2}");

        Assert.NotNull(configuration);
        Assert.Equal(2, configuration.AdditionalData.Count);
        Assert.Equal(1, configuration.AdditionalData["future"].GetInt32());
        Assert.Equal(2, configuration.AdditionalData["Future"].GetInt32());
    }

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
            EntityId = " light.kitchen "
        });
        var logbookPath = server.LastRequestPath;
        var errorLog = await client.Rest.GetErrorLogAsync();
        var camera = await client.Rest.GetCameraImageAsync("camera.front");
        var calendars = await client.Rest.GetCalendarsAsync();
        var calendarEvents = await client.Rest.GetCalendarEventsAsync("calendar.home", start, end);

        Assert.Contains("recorder", components);
        Assert.Equal(5, Assert.Single(events).ListenerCount);
        Assert.Equal("sensor.kitchen_temperature", Assert.Single(Assert.Single(history)).EntityId);
        Assert.Equal("turned on", Assert.Single(logbook).Message);
        Assert.Contains("entity=light.kitchen", logbookPath);
        Assert.DoesNotContain("%20", logbookPath);
        Assert.Equal("test integration warning", errorLog);
        Assert.Equal("test-image-bytes", Encoding.UTF8.GetString(camera));
        Assert.Equal("calendar.home", Assert.Single(calendars).EntityId);
        Assert.Equal("Dinner", Assert.Single(calendarEvents).Summary);
    }

    [Theory]
    [InlineData("[null]")]
    [InlineData("[{\"entity_id\":\"light.kitchen\",\"name\":\"Wrong domain\"}]")]
    [InlineData("[{\"entity_id\":\" calendar.home \",\"name\":\"Padded\"}]")]
    [InlineData("[{\"entity_id\":\"calendar.Home\",\"name\":\"Noncanonical\"}]")]
    [InlineData("[{\"entity_id\":\"calendar.home\"}]")]
    [InlineData("[{\"entity_id\":\"calendar.home\",\"name\":\"   \"}]")]
    [InlineData("[{\"entity_id\":\"calendar.home\",\"name\":\"First\"},{\"entity_id\":\"calendar.home\",\"name\":\"Duplicate\"}]")]
    [InlineData("[{\"entity_id\":\"calendar.home\",\"entity_id\":\"calendar.other\",\"name\":\"Duplicate property\"}]")]
    [InlineData("[{\"entity_id\":\"calendar.home\",\"name\":\"First\",\"name\":\"Second\"}]")]
    public async Task DirectCalendarRestDiscoveryRejectsMalformedResponseIdentities(string response)
    {
        using var server = new TestHomeAssistantServer { CalendarListResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Rest.GetCalendarsAsync());
    }

    [Fact]
    public async Task CalendarDiscoveryPreservesOpaqueProviderObjectsWithDuplicateNestedKeys()
    {
        using var server = new TestHomeAssistantServer
        {
            CalendarListResponseJson = "[{\"entity_id\":\"calendar.home\",\"name\":\"Home\","
                + "\"provider_payload\":{\"key\":1,\"key\":2}}]"
        };
        using var client = TestClientFactory.Create(server);

        var calendar = Assert.Single(await client.Rest.GetCalendarsAsync());
        var providerPayload = calendar.AdditionalData["provider_payload"];

        Assert.Equal(2, providerPayload.EnumerateObject().Count());
        Assert.Equal(new[] { 1, 2 }, providerPayload.EnumerateObject().Select(value => value.Value.GetInt32()));
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
        const string template = " \n{{ value }}\n ";
        var rendered = await client.Rest.RenderTemplateAsync(
            template,
            new Dictionary<string, object?> { ["value"] = "rendered value" });
        var templateRequestBody = server.LastRequestBody;
        var config = await client.Rest.CheckConfigurationAsync();
        var intent = await client.Rest.HandleIntentAsync(
            new Dictionary<string, object?> { ["name"] = "HassTurnOn" });
        var conversation = await client.Rest.ProcessConversationAsync("Turn on the kitchen light", "en");

        Assert.Equal("ready", state.State);
        Assert.Equal("Entity removed.", removed.GetProperty("message").GetString());
        Assert.Contains("fired", fired.GetProperty("message").GetString());
        Assert.Equal("rendered value", rendered);
        using (var templateRequest = JsonDocument.Parse(Assert.IsType<string>(templateRequestBody)))
        {
            Assert.Equal(template, templateRequest.RootElement.GetProperty("template").GetString());
        }
        Assert.True(config.IsValid);
        Assert.Equal("Done", intent.GetProperty("response").GetProperty("speech").GetProperty("plain").GetProperty("speech").GetString());
        Assert.Equal("conversation-1", conversation.GetProperty("conversation_id").GetString());
    }

    [Fact]
    public async Task RestRouteEscapingPrioritizesAPreCanceledToken()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Rest.FireEventAsync(new string('a', 1_000_000), cancellationToken: cancellation.Token));
        Assert.Null(server.LastRequestPath);
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

    [Fact]
    public async Task RestConversationRejectsExplicitBlankSelectorsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.ProcessConversationAsync("hello", language: " "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.ProcessConversationAsync("hello", agentId: " "));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.ProcessConversationAsync("hello", conversationId: " "));

        Assert.Null(server.LastRequestBody);
    }

    [Fact]
    public async Task RestConversationPreservesNonblankTextExactly()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        const string text = "  keep exact spacing\n";

        await client.Rest.ProcessConversationAsync(text);

        using var payload = JsonDocument.Parse(Assert.IsType<string>(server.LastRequestBody));
        Assert.Equal(text, payload.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public async Task RestEntityIdentifiersAreTrimmedAtSharedPathAndHistoryBoundaries()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.States.GetAsync(" sensor.kitchen_temperature ");
        Assert.Equal("/api/states/sensor.kitchen_temperature", server.LastRequestPath);
        await client.Rest.GetHistoryAsync(new HomeAssistantHistoryQuery(" sensor.kitchen_temperature ")
        {
            StartTime = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero)
        });

        Assert.Contains("filter_entity_id=sensor.kitchen_temperature", server.LastRequestPath);
        Assert.DoesNotContain("%20", server.LastRequestPath);
    }

    [Theory]
    [InlineData("sensor.front")]
    [InlineData("CAMERA.front")]
    [InlineData("camera.Front")]
    public async Task CameraImageRejectsIdentifiersOutsideTheCanonicalCameraDomain(string entityId)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.GetCameraImageAsync(entityId));

        Assert.Null(server.LastRequestPath);
    }

    [Theory]
    [InlineData("sensor.home")]
    [InlineData("CALENDAR.home")]
    [InlineData("calendar.Home")]
    public async Task CalendarEventsRejectIdentifiersOutsideTheCanonicalCalendarDomain(string entityId)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var start = new DateTimeOffset(2026, 8, 24, 0, 0, 0, TimeSpan.Zero);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.Rest.GetCalendarEventsAsync(entityId, start, start.AddDays(1)));

        Assert.Null(server.LastRequestPath);
    }
}
