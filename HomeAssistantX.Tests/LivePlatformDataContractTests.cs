#if NET10_0
using System.Text.Json;
using HomeAssistantX.Calendars;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Notifications;
using HomeAssistantX.Registries;
using HomeAssistantX.Services;
using HomeAssistantX.Tests.Infrastructure;

namespace HomeAssistantX.Tests;

public sealed class LivePlatformDataContractTests
{
    [Fact]
    public void NewPlatformExtensionDataPreservesCaseDistinctUnknownFields()
    {
        var notification = JsonSerializer.Deserialize<HomeAssistantPersistentNotification>(
            "{\"notification_id\":\"notice\",\"message\":\"Ready\",\"future\":1,\"Future\":2}");

        Assert.NotNull(notification);
        Assert.Equal(2, notification.AdditionalData.Count);
        Assert.Equal(1, notification.AdditionalData["future"].GetInt32());
        Assert.Equal(2, notification.AdditionalData["Future"].GetInt32());
    }

    [Fact]
    public async Task PersistentNotificationsReadSendDismissAndStreamWithoutPolling()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var current = await client.Notifications.GetPersistentAsync();
        Assert.Equal("notice-1", Assert.Single(current).NotificationId);
        Assert.Equal("fixture", current[0].AdditionalData["source"].GetString());

        var updateReceived = new TaskCompletionSource<HomeAssistantPersistentNotificationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.Notifications.SubscribePersistentAsync((update, _) =>
        {
            updateReceived.TrySetResult(update);
            return Task.CompletedTask;
        });
        var update = await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(HomeAssistantPersistentNotificationUpdateType.Current, update.Type);
        Assert.Equal("Current", update.RawType);
        Assert.Equal("Door open", update.Notifications["notice-1"].Message);
        Assert.Equal("Upper", update.Notifications["Alert"].Message);
        Assert.Equal("Lower", update.Notifications["alert"].Message);

        await client.Notifications.CreatePersistentAsync("Window open", "Security", "window-open");
        AssertServiceCall(server, "persistent_notification", "create", data =>
        {
            Assert.Equal("Window open", data.GetProperty("message").GetString());
            Assert.Equal("window-open", data.GetProperty("notification_id").GetString());
        });

        await client.Notifications.SendAsync(HomeAssistantTarget.ForArea("kitchen"), "Dinner is ready", "Kitchen");
        using (var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody)))
        {
            Assert.Equal("notify", body.RootElement.GetProperty("domain").GetString());
            Assert.Equal("send_message", body.RootElement.GetProperty("service").GetString());
            Assert.Equal("kitchen", body.RootElement.GetProperty("target").GetProperty("area_id")[0].GetString());
        }

        await client.Notifications.DismissPersistentAsync("window-open");
        AssertServiceCall(server, "persistent_notification", "dismiss", data =>
            Assert.Equal("window-open", data.GetProperty("notification_id").GetString()));
        await client.Notifications.DismissAllPersistentAsync();
        AssertServiceCall(server, "persistent_notification", "dismiss_all", data => Assert.Equal(JsonValueKind.Undefined, data.ValueKind));
    }

    [Theory]
    [InlineData("[{\"message\":\"Door open\"}]")]
    [InlineData("[{\"notification_id\":\"notice-1\"}]")]
    [InlineData("[null]")]
    public async Task PersistentNotificationReadsRejectIncompleteItems(string response)
    {
        using var server = new TestHomeAssistantServer { PersistentNotificationResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Notifications.GetPersistentAsync());
    }

    [Fact]
    public async Task RegistrySnapshotTreatsOnlyAnUnsupportedLabelRegistryAsOptionalEnrichment()
    {
        using var unsupportedServer = new TestHomeAssistantServer { LabelRegistryErrorCode = "unknown_command" };
        using var unsupportedClient = TestClientFactory.Create(unsupportedServer);
        var snapshot = await unsupportedClient.Registries.GetSnapshotAsync();
        Assert.False(snapshot.IsLabelRegistryAvailable);
        Assert.Empty(snapshot.Labels);

        using var failedServer = new TestHomeAssistantServer { LabelRegistryErrorCode = "internal_error" };
        using var failedClient = TestClientFactory.Create(failedServer);
        await Assert.ThrowsAsync<HomeAssistantCommandException>(() => failedClient.Registries.GetSnapshotAsync());
    }

    [Fact]
    public async Task PersistentNotificationSubscriptionRejectsNullDictionaryValues()
    {
        using var server = new TestHomeAssistantServer
        {
            PersistentNotificationSubscriptionEventJson = "{\"type\":\"Current\",\"notifications\":{\"notice-1\":null}}"
        };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Notifications.SubscribePersistentAsync((_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
    }

    [Theory]
    [InlineData("{\"type\":\"Current\",\"notifications\":{\"notice-1\":{\"message\":\"Door open\"}}}")]
    [InlineData("{\"type\":\"Current\",\"notifications\":{\"notice-1\":{\"notification_id\":\"notice-1\"}}}")]
    public async Task PersistentNotificationSubscriptionRejectsIncompleteItems(string payload)
    {
        using var server = new TestHomeAssistantServer { PersistentNotificationSubscriptionEventJson = payload };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Notifications.SubscribePersistentAsync((_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
    }

    [Fact]
    public async Task CalendarReadsWritesAndStreamsTimedAndAllDayEvents()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var start = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(2);

        Assert.Equal("calendar.home", Assert.Single(await client.Calendars.GetAsync()).EntityId);
        var restEvent = Assert.Single(await client.Calendars.GetEventsAsync(" calendar.home ", start, end));
        Assert.Equal(18, restEvent.Start!.DateTime!.Value.Hour);
        Assert.Equal(1, restEvent.Start.AdditionalData["future"].GetInt32());
        Assert.Equal(2, restEvent.Start.AdditionalData["Future"].GetInt32());

        var updateReceived = new TaskCompletionSource<HomeAssistantCalendarEventUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.Calendars.SubscribeAsync(" calendar.home ", start, end, (update, _) =>
        {
            updateReceived.TrySetResult(update);
            return Task.CompletedTask;
        });
        var streamed = Assert.Single((await updateReceived.Task.WaitAsync(TimeSpan.FromSeconds(2))).Events);
        Assert.Equal("event-1", streamed.Uid);
        Assert.Equal("FREQ=WEEKLY", streamed.RecurrenceRule);
        Assert.Equal(18, streamed.Start!.DateTime!.Value.Hour);
        using (var subscribe = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("calendar/event/subscribe"))))
            Assert.Equal("calendar.home", subscribe.RootElement.GetProperty("entity_id").GetString());

        var timed = HomeAssistantCalendarEventInput.Timed(start.AddHours(10), start.AddHours(11), "Planning");
        timed.Description = "Weekly planning";
        timed.RecurrenceRule = "FREQ=WEEKLY";
        await client.Calendars.CreateEventAsync(" calendar.home ", timed);
        using (var create = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("calendar/event/create"))))
        {
            var eventData = create.RootElement.GetProperty("event");
            Assert.Equal("calendar.home", create.RootElement.GetProperty("entity_id").GetString());
            Assert.Equal("Planning", eventData.GetProperty("summary").GetString());
            Assert.Contains("T10:00:00", eventData.GetProperty("start").GetString());
            Assert.Equal("FREQ=WEEKLY", eventData.GetProperty("rrule").GetString());
        }

        var allDay = HomeAssistantCalendarEventInput.AllDay("2026-08-27", "2026-08-28", "Holiday");
        var reference = new HomeAssistantCalendarEventReference("event-1") { RecurrenceId = "20260827", RecurrenceRange = "THISANDFUTURE" };
        await client.Calendars.UpdateEventAsync(" calendar.home ", reference, allDay);
        using (var update = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("calendar/event/update"))))
        {
            Assert.Equal("2026-08-27", update.RootElement.GetProperty("event").GetProperty("start").GetString());
            Assert.Equal("calendar.home", update.RootElement.GetProperty("entity_id").GetString());
            Assert.Equal("THISANDFUTURE", update.RootElement.GetProperty("recurrence_range").GetString());
        }

        await client.Calendars.DeleteEventAsync(" calendar.home ", reference);
        using var delete = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("calendar/event/delete")));
        Assert.Equal("event-1", delete.RootElement.GetProperty("uid").GetString());
        Assert.Equal("calendar.home", delete.RootElement.GetProperty("entity_id").GetString());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("\"malformed\"")]
    [InlineData("1")]
    [InlineData("true")]
    public async Task CalendarSubscriptionRejectsMalformedNonNullPayloads(string payload)
    {
        using var server = new TestHomeAssistantServer { CalendarSubscriptionEventJson = payload };
        using var client = TestClientFactory.Create(server);
        var start = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        using var subscription = await client.Calendars.SubscribeAsync(
            "calendar.home",
            start,
            start.AddDays(1),
            (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
    }

    [Theory]
    [InlineData("[{}]")]
    [InlineData("[{\"summary\":\"Dinner\",\"start\":\"2026-08-26T18:00:00+02:00\"}]")]
    [InlineData("[{\"summary\":\" \",\"start\":\"2026-08-26T18:00:00+02:00\",\"end\":\"2026-08-26T20:00:00+02:00\"}]")]
    [InlineData("[{\"summary\":\"Dinner\",\"start\":\"2026-08-26\",\"end\":\"2026-08-26T20:00:00+02:00\"}]")]
    public async Task CalendarSubscriptionRejectsIncompleteEventEntries(string payload)
    {
        using var server = new TestHomeAssistantServer { CalendarSubscriptionEventJson = payload };
        using var client = TestClientFactory.Create(server);
        var start = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        using var subscription = await client.Calendars.SubscribeAsync(
            "calendar.home",
            start,
            start.AddDays(1),
            (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
    }

    [Fact]
    public void CalendarInputAcceptsOffsetTransitionsAndRejectsInvalidRangesBeforeNetworkUse()
    {
        var instant = new DateTimeOffset(2026, 8, 26, 12, 0, 0, TimeSpan.Zero);
        Assert.Throws<ArgumentOutOfRangeException>(() => HomeAssistantCalendarEventInput.Timed(instant, instant, "Invalid"));
        var crossingOffset = HomeAssistantCalendarEventInput.Timed(
            new DateTimeOffset(2026, 11, 1, 1, 30, 0, TimeSpan.FromHours(-4)),
            new DateTimeOffset(2026, 11, 1, 2, 30, 0, TimeSpan.FromHours(-5)),
            "Offset transition");
        Assert.EndsWith("-04:00", crossingOffset.Start, StringComparison.Ordinal);
        Assert.EndsWith("-05:00", crossingOffset.End, StringComparison.Ordinal);
        Assert.Throws<ArgumentException>(() => HomeAssistantCalendarEventInput.AllDay("26-08-2026", "2026-08-27", "Invalid"));
        Assert.Throws<ArgumentOutOfRangeException>(() => HomeAssistantCalendarEventInput.AllDay("2026-08-27", "2026-08-27", "Invalid"));
        var input = HomeAssistantCalendarEventInput.AllDay("2026-08-27", "2026-08-28", "Invalid recurrence");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=SECONDLY");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "freq=weekly");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;COUNT=abc");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;FREQ=DAILY");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;INTERVAL=0");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;BYDAY=MONDAY");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;COUNT=5;UNTIL=20261231");
        input.RecurrenceRule = "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR;COUNT=10";
        Assert.Equal("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR;COUNT=10", input.RecurrenceRule);
        Assert.Throws<ArgumentException>(() => new HomeAssistantCalendarEventReference("event-1") { RecurrenceRange = "THISANDFUTURE" }.Validate());
        Assert.Throws<ArgumentException>(() => new HomeAssistantCalendarEventReference("event-1") { RecurrenceId = "20260827", RecurrenceRange = "THIS" }.Validate());
        var recurrence = new HomeAssistantCalendarEventReference("event-1") { RecurrenceId = "20260827", RecurrenceRange = "thisandfuture" };
        recurrence.Validate();
    }

    [Fact]
    public async Task CalendarOperationsRejectMalformedOrWrongDomainEntityIdsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var input = HomeAssistantCalendarEventInput.AllDay("2026-08-27", "2026-08-28", "Event");

        await Assert.ThrowsAsync<ArgumentException>(() => client.Calendars.CreateEventAsync("light.kitchen", input));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Calendars.CreateEventAsync("calendar.", input));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Calendars.CreateEventAsync("calendar.home.extra", input));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.GetCalendarEventsAsync(
            "calendar.home.extra",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow.AddHours(1)));

        Assert.Null(server.GetLastWebSocketCommand("calendar/event/create"));
    }

    [Fact]
    public async Task RegistrySnapshotIncludesLabelsAndScopedCategoryCrudPreservesTriStateUpdates()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        var snapshot = await client.Registries.GetSnapshotAsync();
        Assert.Contains(snapshot.Labels, label => label.LabelId == "security");
        Assert.Contains(snapshot.Labels, label => label.LabelId == "security-name");

        var created = await client.Registries.CreateLabelAsync(new HomeAssistantLabelCreate("Security")
        {
            Color = "red",
            Icon = "mdi:shield"
        });
        Assert.Equal("security", created.LabelId);

        await client.Registries.UpdateLabelAsync("security", new HomeAssistantLabelUpdate().WithColor(null).WithDescription("Safety devices"));
        using (var labelUpdate = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("config/label_registry/update"))))
        {
            Assert.Equal(JsonValueKind.Null, labelUpdate.RootElement.GetProperty("color").ValueKind);
            Assert.False(labelUpdate.RootElement.TryGetProperty("icon", out _));
        }
        await client.Registries.DeleteLabelAsync("security");

        Assert.Equal("comfort", Assert.Single(await client.Registries.GetCategoriesAsync("automation")).CategoryId);
        await client.Registries.CreateCategoryAsync("automation", new HomeAssistantCategoryCreate("Comfort") { Icon = "mdi:sofa" });
        await client.Registries.UpdateCategoryAsync("automation", "comfort", new HomeAssistantCategoryUpdate().WithIcon(null));
        using (var categoryUpdate = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("config/category_registry/update"))))
        {
            Assert.Equal("automation", categoryUpdate.RootElement.GetProperty("scope").GetString());
            Assert.Equal(JsonValueKind.Null, categoryUpdate.RootElement.GetProperty("icon").ValueKind);
        }
        await client.Registries.DeleteCategoryAsync("automation", "comfort");
    }

    [Fact]
    public async Task EmptyRegistryUpdatesFailBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Registries.UpdateLabelAsync("security", new HomeAssistantLabelUpdate()));
        Assert.Null(server.GetLastWebSocketCommand("config/label_registry/update"));
    }

    [Theory]
    [InlineData("[{}]", true)]
    [InlineData("[{\"label_id\":\"security\"}]", true)]
    [InlineData("[{\"name\":\"Security\"}]", true)]
    [InlineData("[{}]", false)]
    [InlineData("[{\"category_id\":\"comfort\"}]", false)]
    [InlineData("[{\"name\":\"Comfort\"}]", false)]
    public async Task RegistryListsRejectIncompleteLabelsAndCategories(string response, bool labels)
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        if (labels) server.LabelRegistryResponseJson = response;
        else server.CategoryRegistryResponseJson = response;

        if (labels)
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetLabelsAsync());
        else
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetCategoriesAsync("automation"));
    }

    [Fact]
    public async Task RegistryMutationsRejectIncompleteLabelsAndCategories()
    {
        using var server = new TestHomeAssistantServer
        {
            LabelMutationResponseJson = "{}",
            CategoryMutationResponseJson = "{}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.CreateLabelAsync(new HomeAssistantLabelCreate("Security")));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.UpdateLabelAsync("security", new HomeAssistantLabelUpdate().WithName("Security")));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.CreateCategoryAsync("automation", new HomeAssistantCategoryCreate("Comfort")));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.UpdateCategoryAsync("automation", "comfort", new HomeAssistantCategoryUpdate().WithName("Comfort")));
    }

    [Fact]
    public async Task RegistryUpdatesCorrelateReturnedImmutableIdentifiers()
    {
        using var server = new TestHomeAssistantServer
        {
            LabelMutationResponseJson = "{\"label_id\":\"other\",\"name\":\"Security\"}",
            CategoryMutationResponseJson = "{\"category_id\":\"other\",\"name\":\"Comfort\"}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.UpdateLabelAsync("security", new HomeAssistantLabelUpdate().WithName("Security")));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.UpdateCategoryAsync("automation", "comfort", new HomeAssistantCategoryUpdate().WithName("Comfort")));
    }

    [Fact]
    public async Task EmptyNotificationTargetFailsBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(() => client.Notifications.SendAsync(HomeAssistantTarget.Create(), "Message"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Notifications.SendAsync(
            HomeAssistantTarget.ForEntity("sensor.kitchen"),
            "Message"));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Notifications.SendAsync(
            HomeAssistantTarget.ForEntity("notify.mobile.extra"),
            "Message"));
        Assert.Null(server.LastServiceCallBody);
    }

    private static void AssertServiceCall(TestHomeAssistantServer server, string domain, string service, Action<JsonElement> assertData)
    {
        using var body = JsonDocument.Parse(Assert.IsType<string>(server.LastServiceCallBody));
        Assert.Equal(domain, body.RootElement.GetProperty("domain").GetString());
        Assert.Equal(service, body.RootElement.GetProperty("service").GetString());
        var data = body.RootElement.TryGetProperty("service_data", out var serviceData) ? serviceData : default;
        assertData(data);
    }
}
#endif
