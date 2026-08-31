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
    public async Task RawEventTriggerAndTemplatePayloadsFailBeforeTransport()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;

        await Assert.ThrowsAsync<ArgumentException>(() => client.Events.FireAsync("homeassistantx_test", cyclic));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Events.SubscribeTriggerAsync(cyclic, (_, _) => Task.CompletedTask));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.FireEventAsync("homeassistantx_test", cyclic));
        await Assert.ThrowsAsync<ArgumentException>(() => client.Rest.RenderTemplateAsync("{{ value }}", cyclic));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Events.FireAsync("homeassistantx_test", cyclic, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Events.SubscribeTriggerAsync(cyclic, (_, _) => Task.CompletedTask, cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Rest.FireEventAsync("homeassistantx_test", cyclic, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.Rest.RenderTemplateAsync("{{ value }}", cyclic, cancellation.Token));
        Assert.Null(server.LastServiceCallBody);
        Assert.Null(server.LastRequestBody);
    }

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
        await subscription.StopAsync();
        Assert.True(update.Notifications.ContainsKey("notice-1"));
        Assert.Equal("Door open", update.Notifications["notice-1"].Message);

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
    public async Task PersistentNotificationReadsRejectDuplicateIdentifiers()
    {
        using var server = new TestHomeAssistantServer
        {
            PersistentNotificationResponseJson = "[{\"notification_id\":\"notice-1\",\"message\":\"First\"},{\"notification_id\":\"notice-1\",\"message\":\"Second\"}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Notifications.GetPersistentAsync());
    }

    [Theory]
    [InlineData("[{\"notification_id\":\"notice-1\",\"notification_id\":\"notice-1\",\"message\":\"Door open\"}]")]
    public async Task PersistentNotificationReadsRejectDuplicateRecognizedFields(string response)
    {
        using var server = new TestHomeAssistantServer { PersistentNotificationResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Notifications.GetPersistentAsync());
    }

    [Fact]
    public async Task PersistentNotificationSubscriptionPreservesCaseDistinctExtensionFields()
    {
        using var server = new TestHomeAssistantServer
        {
            PersistentNotificationSubscriptionEventJson =
                "{\"type\":\"Current\",\"notifications\":{\"notice-1\":{\"notification_id\":\"notice-1\",\"message\":\"Door open\",\"Message\":\"provider value\"}}}"
        };
        using var client = TestClientFactory.Create(server);
        var received = new TaskCompletionSource<HomeAssistantPersistentNotificationUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.Notifications.SubscribePersistentAsync((update, _) =>
        {
            received.TrySetResult(update);
            return Task.CompletedTask;
        });

        var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal("Door open", update.Notifications["notice-1"].Message);
        Assert.Equal("provider value", update.Notifications["notice-1"].AdditionalData["Message"].GetString());
    }

    [Fact]
    public void PersistentNotificationValidationStopsWhenCancellationArrivesDuringTraversal()
    {
        using var cancellation = new CancellationTokenSource();
        var notifications = new CancellingRegistryEnumerable<HomeAssistantPersistentNotification>(
            cancellation,
            () => new HomeAssistantPersistentNotification { NotificationId = "notice", Message = "Message" });

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantNotificationClient.ValidateNotifications(
                notifications,
                "The test notification response",
                cancellation.Token));
        Assert.InRange(notifications.ReadCount, 1, 2);
    }

    [Fact]
    public async Task PersistentNotificationPropertyDecodingIsCancellationIsolated()
    {
        var json = "[{\"notification_id\":\"notice\",\"message\":\"Message\",\"future_"
            + new string('a', 16_000_000) + "\":true}]";
        using var document = JsonDocument.Parse(json);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Task.Factory.StartNew(
            () =>
            {
                started.TrySetResult(true);
                HomeAssistantNotificationClient.ValidateNotificationObjects(
                    document.RootElement,
                    dictionary: false,
                    "The test notification response",
                    cancellation.Token);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        await started.Task;
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => operation);
    }

    [Fact]
    public async Task RegistrySnapshotTreatsUnsupportedOrUnauthorizedLabelsAsOptionalEnrichment()
    {
        foreach (var errorCode in new[] { "unknown_command", "unauthorized" })
        {
            using var unavailableServer = new TestHomeAssistantServer { LabelRegistryErrorCode = errorCode };
            using var unavailableClient = TestClientFactory.Create(unavailableServer);
            var snapshot = await unavailableClient.Registries.GetSnapshotAsync();
            Assert.False(snapshot.IsLabelRegistryAvailable);
            Assert.Empty(snapshot.Labels);
        }

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
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" current")]
    [InlineData("current ")]
    public async Task PersistentNotificationSubscriptionRejectsInvalidUpdateTypes(string type)
    {
        using var server = new TestHomeAssistantServer
        {
            PersistentNotificationSubscriptionEventJson =
                "{\"type\":" + JsonSerializer.Serialize(type) + ",\"notifications\":{}}"
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
    public async Task PersistentNotificationSubscriptionRejectsMismatchedDictionaryIdentity()
    {
        using var server = new TestHomeAssistantServer
        {
            PersistentNotificationSubscriptionEventJson = "{\"type\":\"Current\",\"notifications\":{\"notice-a\":{\"notification_id\":\"notice-b\",\"message\":\"Door open\"}}}"
        };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Notifications.SubscribePersistentAsync((_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
    }

    [Fact]
    public async Task PersistentNotificationSubscriptionRejectsDuplicateObjectKeys()
    {
        using var server = new TestHomeAssistantServer
        {
            PersistentNotificationSubscriptionEventJson = "{\"type\":\"Current\",\"notifications\":{\"notice\":{\"notification_id\":\"notice\",\"message\":\"First\"},\"notice\":{\"notification_id\":\"notice\",\"message\":\"Second\"}}}"
        };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Notifications.SubscribePersistentAsync((_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
    }

    [Theory]
    [InlineData("{\"type\":\"Current\",\"type\":\"Removed\",\"notifications\":{}}")]
    [InlineData("{\"type\":\"Current\",\"notifications\":{},\"notifications\":{}}")]
    public async Task PersistentNotificationSubscriptionRejectsDuplicateEnvelopeFields(string payload)
    {
        using var server = new TestHomeAssistantServer { PersistentNotificationSubscriptionEventJson = payload };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Notifications.SubscribePersistentAsync((_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
    }

    [Theory]
    [InlineData("{\"type\":\"Current\",\"notifications\":{\"notice\":{\"notification_id\":\"notice\",\"notification_id\":\"notice\",\"message\":\"First\"}}}")]
    public async Task PersistentNotificationSubscriptionRejectsDuplicateRecognizedItemFields(string payload)
    {
        using var server = new TestHomeAssistantServer { PersistentNotificationSubscriptionEventJson = payload };
        using var client = TestClientFactory.Create(server);
        using var subscription = await client.Notifications.SubscribePersistentAsync((_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(async () => await subscription.Completion);
    }

    [Fact]
    public async Task PersistentNotificationCreateRejectsBlankIdentifierBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<ArgumentException>(
            () => client.Notifications.CreatePersistentAsync("Window open", notificationId: " "));

        Assert.Null(server.LastServiceCallBody);
    }

    [Fact]
    public async Task PersistentNotificationMutationValidationHonorsCancellationBeforeDispatch()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Notifications.CreatePersistentAsync(
                new string(' ', 1_000_000),
                notificationId: new string(' ', 1_000_000),
                cancellationToken: cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Notifications.DismissPersistentAsync(
                new string(' ', 1_000_000),
                cancellation.Token));

        Assert.Null(server.LastServiceCallBody);
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
    [InlineData("[{}]")]
    [InlineData("[{\"entity_id\":\"light.kitchen\",\"name\":\"Wrong\"}]")]
    [InlineData("[{\"entity_id\":\"calendar.Home\",\"name\":\"Noncanonical\"}]")]
    [InlineData("[{\"entity_id\":\" calendar.home \",\"name\":\"Padded\"}]")]
    [InlineData("[{\"entity_id\":\"calendar.home\"}]")]
    [InlineData("[{\"entity_id\":\"calendar.home\",\"name\":\"   \"}]")]
    [InlineData("[{\"entity_id\":\"calendar.home\",\"name\":\"First\"},{\"entity_id\":\"calendar.home\",\"name\":\"Duplicate\"}]")]
    public async Task CalendarDiscoveryRejectsInvalidEntityIdentifiers(string response)
    {
        using var server = new TestHomeAssistantServer { CalendarListResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Calendars.GetAsync());
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

    [Fact]
    public async Task CalendarSubscriptionRoutesNullAsUnavailableUpdate()
    {
        using var server = new TestHomeAssistantServer { CalendarSubscriptionEventJson = "null" };
        using var client = TestClientFactory.Create(server);
        var start = new DateTimeOffset(2026, 8, 26, 0, 0, 0, TimeSpan.Zero);
        var received = new TaskCompletionSource<HomeAssistantCalendarEventUpdate>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = await client.Calendars.SubscribeAsync(
            "calendar.home",
            start,
            start.AddDays(1),
            (update, _) =>
            {
                received.TrySetResult(update);
                return Task.CompletedTask;
            });

        var update = await received.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(update.IsAvailable);
        Assert.Empty(update.Events);
        Assert.Equal(JsonValueKind.Null, update.Raw.ValueKind);
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
        input.RecurrenceRule = "freq=weekly;byday=mo,we;wkst=su";
        Assert.Equal("freq=weekly;byday=mo,we;wkst=su", input.RecurrenceRule);
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;COUNT=abc");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;FREQ=DAILY");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;INTERVAL=0");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;BYDAY=MONDAY");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=WEEKLY;COUNT=5;UNTIL=20261231");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=DAILY;BYSECOND=30");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=DAILY;BYMINUTE=15");
        Assert.Throws<ArgumentException>(() => input.RecurrenceRule = "FREQ=DAILY;BYHOUR=8");
        input.RecurrenceRule = "FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR;COUNT=10";
        Assert.Equal("FREQ=WEEKLY;INTERVAL=2;BYDAY=MO,WE,FR;COUNT=10", input.RecurrenceRule);
        Assert.Throws<ArgumentException>(() => new HomeAssistantCalendarEventReference("event-1") { RecurrenceRange = "THISANDFUTURE" }.Validate());
        Assert.Throws<ArgumentException>(() => new HomeAssistantCalendarEventReference("event-1") { RecurrenceId = "20260827", RecurrenceRange = "THIS" }.Validate());
        var recurrence = new HomeAssistantCalendarEventReference("event-1") { RecurrenceId = "20260827", RecurrenceRange = "thisandfuture" };
        recurrence.Validate();
    }

    [Fact]
    public void CalendarReferenceValidationHonorsPreCanceledTokens()
    {
        var reference = new HomeAssistantCalendarEventReference("event-1")
        {
            RecurrenceId = new string('x', 1_000_000),
            RecurrenceRange = "THISANDFUTURE"
        };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => reference.Validate(cancellation.Token));
    }

    [Fact]
    public void CalendarReferenceDispatchUsesOneValidatedSnapshot()
    {
        var reference = new HomeAssistantCalendarEventReference("event-1");
        var payload = new MutatingCalendarPayload(() =>
        {
            reference.RecurrenceId = "changed-occurrence";
            reference.RecurrenceRange = "THISANDFUTURE";
        });

        reference.AddTo(payload, CancellationToken.None);

        Assert.Equal("event-1", payload["uid"]);
        Assert.False(payload.ContainsKey("recurrence_id"));
        Assert.False(payload.ContainsKey("recurrence_range"));
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
    public async Task CalendarOperationsHonorCancellationBeforeEntityNormalization()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var entityId = " calendar." + new string('x', 1_000_000);
        var start = DateTimeOffset.Parse("2026-08-27T00:00:00Z");
        var input = HomeAssistantCalendarEventInput.AllDay("2026-08-27", "2026-08-28", "Event");

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Calendars.GetEventsAsync(entityId, start, start.AddDays(1), cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Calendars.CreateEventAsync(entityId, input, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Rest.GetCalendarEventsAsync(entityId, start, start.AddDays(1), cancellation.Token));

        Assert.Null(server.LastRequestPath);
    }

    [Fact]
    public async Task RegistryMutationIdentifiersHonorCallerCancellationBeforeNormalization()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var paddedIdentifier = " " + new string('x', 1_000_000) + " ";

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Registries.UpdateLabelAsync(
                paddedIdentifier,
                new HomeAssistantLabelUpdate().WithDescription("updated"),
                cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Registries.DeleteCategoryAsync(
                paddedIdentifier,
                paddedIdentifier,
                cancellation.Token));

        Assert.Null(server.GetLastWebSocketCommand("config/label_registry/update"));
        Assert.Null(server.GetLastWebSocketCommand("config/category_registry/delete"));
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
    public void RegistrySnapshotValidationRejectsNullAssignmentCollections()
    {
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantArea { Aliases = null! } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantArea { Labels = null! } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantFloor { Aliases = null! } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantDeviceRegistryEntry { ConfigEntries = null! } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantDeviceRegistryEntry { Labels = null! } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantEntityRegistryEntry { Aliases = null! } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantEntityRegistryEntry { Labels = null! } }, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" security ")]
    public void RegistrySnapshotValidationRejectsMalformedLabelAssignments(string labelId)
    {
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantArea { Labels = new[] { labelId } } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantDeviceRegistryEntry { Labels = new[] { labelId } } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantEntityRegistryEntry { Labels = new[] { labelId } } }, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" entry-id ")]
    public void RegistrySnapshotValidationRejectsMalformedConfigurationEntryAssignments(string entryId)
    {
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantDeviceRegistryEntry { ConfigEntries = new[] { entryId } } }, CancellationToken.None));
    }

    [Fact]
    public void RegistrySnapshotValidationRejectsDuplicateLabelAssignments()
    {
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantEntityRegistryEntry { Labels = new[] { "security", "security" } } }, CancellationToken.None));
    }

    [Fact]
    public void RegistrySnapshotValidationRejectsDuplicateConfigurationEntryAssignments()
    {
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateAssignmentCollections(
            new[] { new HomeAssistantDeviceRegistryEntry { ConfigEntries = new[] { "entry-id", "entry-id" } } }, CancellationToken.None));
    }

    [Fact]
    public void RegistryResponsesRejectPaddedDisplayNames()
    {
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateLabels(
            new[] { new HomeAssistantLabel { LabelId = "security", Name = " Security " } }, CancellationToken.None));
        Assert.Throws<HomeAssistantProtocolException>(() => HomeAssistantRegistryClient.ValidateCategories(
            new[] { new HomeAssistantCategory { CategoryId = "comfort", Name = " Comfort " } }, CancellationToken.None));
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
    [InlineData("[{\"label_id\":\" security \",\"name\":\"Security\"}]", true)]
    [InlineData("[{}]", false)]
    [InlineData("[{\"category_id\":\"comfort\"}]", false)]
    [InlineData("[{\"name\":\"Comfort\"}]", false)]
    [InlineData("[{\"category_id\":\" comfort \",\"name\":\"Comfort\"}]", false)]
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
    public async Task RegistryListsAndSnapshotsRejectDuplicateLabelAndCategoryIdentities()
    {
        using var server = new TestHomeAssistantServer
        {
            LabelRegistryResponseJson = "[{\"label_id\":\"security\",\"name\":\"Security\"},{\"label_id\":\"security\",\"name\":\"Duplicate\"}]",
            CategoryRegistryResponseJson = "[{\"category_id\":\"comfort\",\"name\":\"Comfort\"},{\"category_id\":\"comfort\",\"name\":\"Duplicate\"}]"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetLabelsAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetCategoriesAsync("automation"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetSnapshotAsync());
    }

    [Fact]
    public async Task RegistryListsAndMutationsRejectDuplicateRecognizedFields()
    {
        using var server = new TestHomeAssistantServer
        {
            LabelRegistryResponseJson = "[{\"label_id\":\"security\",\"label_id\":\"other\",\"name\":\"Security\"}]",
            CategoryRegistryResponseJson = "[{\"category_id\":\"comfort\",\"category_id\":\"other\",\"name\":\"Comfort\"}]",
            LabelMutationResponseJson = "{\"label_id\":\"security\",\"name\":\"Security\",\"name\":\"Other\"}",
            CategoryMutationResponseJson = "{\"category_id\":\"comfort\",\"name\":\"Comfort\",\"name\":\"Other\"}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetLabelsAsync());
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetCategoriesAsync("automation"));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.CreateLabelAsync(new HomeAssistantLabelCreate("Security")));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.CreateCategoryAsync("automation", new HomeAssistantCategoryCreate("Comfort")));
    }

    [Fact]
    public async Task RegistrySnapshotRejectsDuplicateExtendedEntitiesAndConfigEntryWrappers()
    {
        using (var extendedServer = new TestHomeAssistantServer
        {
            ExtendedEntityRegistryResponseJson =
                "{\"sensor.kitchen_temperature\":{\"entity_id\":\"sensor.kitchen_temperature\",\"entity_id\":\"sensor.other\"}}"
        })
        using (var client = TestClientFactory.Create(extendedServer))
        {
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetSnapshotAsync());
        }

        using (var configServer = new TestHomeAssistantServer
        {
            ConfigEntriesResponseJson = "{\"entries\":[],\"entries\":[]}"
        })
        using (var client = TestClientFactory.Create(configServer))
        {
            await Assert.ThrowsAsync<HomeAssistantProtocolException>(() => client.Registries.GetSnapshotAsync());
        }
    }

    [Theory]
    [InlineData("[{\"entity_id\":\"Sensor.NotCanonical\",\"aliases\":[],\"labels\":[]}]")]
    [InlineData("[{\"entity_id\":\"sensor.duplicate\",\"aliases\":[],\"labels\":[]},{\"entity_id\":\"sensor.duplicate\",\"aliases\":[],\"labels\":[]}]")]
    public async Task RegistrySnapshotRejectsInvalidPartialEntityIdsBeforeEnrichment(string response)
    {
        using var server = new TestHomeAssistantServer { EntityRegistryResponseJson = response };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.GetSnapshotAsync());

        Assert.Null(server.GetLastWebSocketCommand("config/entity_registry/get_entries"));
    }

    [Fact]
    public async Task RegistryResponsesPreserveNestedProviderDuplicateProperties()
    {
        const string duplicateProviderObject = "{\"key\":1,\"key\":2}";
        using var server = new TestHomeAssistantServer
        {
            LabelRegistryResponseJson =
                "[{\"label_id\":\"security\",\"name\":\"Security\",\"provider\":" + duplicateProviderObject + "}]",
            LabelMutationResponseJson =
                "{\"label_id\":\"security\",\"name\":\"Security\",\"provider\":" + duplicateProviderObject + "}",
            ExtendedEntityRegistryResponseJson =
                "{\"sensor.kitchen_temperature\":{\"entity_id\":\"sensor.kitchen_temperature\",\"provider\":" + duplicateProviderObject + "}}",
            ConfigEntriesResponseJson =
                "{\"entries\":[{\"entry_id\":\"entry-test\",\"domain\":\"test\",\"provider\":" + duplicateProviderObject + "}]}"
        };
        using var client = TestClientFactory.Create(server);

        var label = Assert.Single(await client.Registries.GetLabelsAsync());
        var created = await client.Registries.CreateLabelAsync(new HomeAssistantLabelCreate("Security"));
        var snapshot = await client.Registries.GetSnapshotAsync();

        Assert.Equal(2, label.AdditionalData["provider"].EnumerateObject().Count());
        Assert.Equal(2, created.AdditionalData["provider"].EnumerateObject().Count());
        Assert.Equal(
            2,
            Assert.Single(snapshot.Entities, value => value.EntityId == "sensor.kitchen_temperature")
                .AdditionalData["provider"].EnumerateObject().Count());
        Assert.Equal(
            2,
            Assert.Single(snapshot.ConfigEntries, value => value.EntryId == "entry-test")
                .AdditionalData["provider"].EnumerateObject().Count());
    }

    [Fact]
    public async Task RestEventTypeValidationHonorsPreCanceledCallers()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Rest.FireEventAsync(new string(' ', 1_000_000), cancellationToken: cancellation.Token));

        Assert.Null(server.LastRequestPath);
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
    public async Task RegistryCreatesCorrelateReturnedNames()
    {
        using var server = new TestHomeAssistantServer
        {
            LabelMutationResponseJson = "{\"label_id\":\"security\",\"name\":\"Other\"}",
            CategoryMutationResponseJson = "{\"category_id\":\"comfort\",\"name\":\"Other\"}"
        };
        using var client = TestClientFactory.Create(server);

        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.CreateLabelAsync(new HomeAssistantLabelCreate("Security")));
        await Assert.ThrowsAsync<HomeAssistantProtocolException>(() =>
            client.Registries.CreateCategoryAsync("automation", new HomeAssistantCategoryCreate("Comfort")));
    }

    [Fact]
    public async Task RegistryIdentifiersAreNormalizedBeforeDispatchAndCorrelation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);

        await client.Registries.UpdateLabelAsync(" security ", new HomeAssistantLabelUpdate().WithName(" Security "));
        using (var labelCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("config/label_registry/update"))))
        {
            Assert.Equal("security", labelCommand.RootElement.GetProperty("label_id").GetString());
            Assert.Equal("Security", labelCommand.RootElement.GetProperty("name").GetString());
        }

        await client.Registries.UpdateCategoryAsync(" automation ", " comfort ", new HomeAssistantCategoryUpdate().WithName(" Comfort "));
        using var categoryCommand = JsonDocument.Parse(Assert.IsType<string>(server.GetLastWebSocketCommand("config/category_registry/update")));
        Assert.Equal("automation", categoryCommand.RootElement.GetProperty("scope").GetString());
        Assert.Equal("comfort", categoryCommand.RootElement.GetProperty("category_id").GetString());
        Assert.Equal("Comfort", categoryCommand.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void RegistrySemanticValidationStopsAfterCancellation()
    {
        using var labelCancellation = new CancellationTokenSource();
        var labels = new CancellingRegistryEnumerable<HomeAssistantLabel>(
            labelCancellation,
            () => new HomeAssistantLabel { LabelId = "security", Name = "Security" });
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRegistryClient.ValidateLabels(labels, labelCancellation.Token));
        Assert.InRange(labels.ReadCount, 1, 2);

        using var categoryCancellation = new CancellationTokenSource();
        var categories = new CancellingRegistryEnumerable<HomeAssistantCategory>(
            categoryCancellation,
            () => new HomeAssistantCategory { CategoryId = "comfort", Name = "Comfort" });
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRegistryClient.ValidateCategories(categories, categoryCancellation.Token));
        Assert.InRange(categories.ReadCount, 1, 2);
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

    [Fact]
    public async Task NotificationTargetNormalizationHonorsPreCancellation()
    {
        using var server = new TestHomeAssistantServer();
        using var client = TestClientFactory.Create(server);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var target = new HomeAssistantTarget
        {
            EntityIds = new[] { "notify.mobile", "not-an-entity" }
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            client.Notifications.SendAsync(target, "Message", cancellationToken: cancellation.Token));

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

    private sealed class CancellingRegistryEnumerable<T> : IEnumerable<T>
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly Func<T> _factory;

        internal CancellingRegistryEnumerable(CancellationTokenSource cancellation, Func<T> factory)
        {
            _cancellation = cancellation;
            _factory = factory;
        }

        internal int ReadCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            for (var index = 0; index < 1000; index++)
            {
                ReadCount++;
                if (ReadCount == 1) _cancellation.Cancel();
                if (ReadCount > 2) throw new InvalidOperationException("Registry validation continued after cancellation.");
                yield return _factory();
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class MutatingCalendarPayload : IDictionary<string, object?>
    {
        private readonly Dictionary<string, object?> _inner = new(StringComparer.Ordinal);
        private readonly Action _mutate;
        private bool _mutated;

        internal MutatingCalendarPayload(Action mutate) => _mutate = mutate;

        public object? this[string key]
        {
            get => _inner[key];
            set
            {
                if (!_mutated && string.Equals(key, "uid", StringComparison.Ordinal))
                {
                    _mutated = true;
                    _mutate();
                }
                _inner[key] = value;
            }
        }

        public ICollection<string> Keys => _inner.Keys;
        public ICollection<object?> Values => _inner.Values;
        public int Count => _inner.Count;
        public bool IsReadOnly => false;
        public void Add(string key, object? value) => _inner.Add(key, value);
        public void Add(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)_inner).Add(item);
        public void Clear() => _inner.Clear();
        public bool Contains(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)_inner).Contains(item);
        public bool ContainsKey(string key) => _inner.ContainsKey(key);
        public void CopyTo(KeyValuePair<string, object?>[] array, int arrayIndex) => ((ICollection<KeyValuePair<string, object?>>)_inner).CopyTo(array, arrayIndex);
        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _inner.GetEnumerator();
        public bool Remove(string key) => _inner.Remove(key);
        public bool Remove(KeyValuePair<string, object?> item) => ((ICollection<KeyValuePair<string, object?>>)_inner).Remove(item);
        public bool TryGetValue(string key, out object? value) => _inner.TryGetValue(key, out value);
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
#endif
