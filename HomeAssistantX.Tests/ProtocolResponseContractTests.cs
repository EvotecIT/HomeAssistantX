using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Calendars;
using HomeAssistantX.Registries;
using HomeAssistantX.States;
using HomeAssistantX.Rest;
using HomeAssistantX.Notifications;

namespace HomeAssistantX.Tests;

public sealed class ProtocolResponseContractTests
{
    [Fact]
    public void NativeEntityIdentifierNormalizerTrimsCanonicalIdsAndRejectsUppercase()
    {
        Assert.True(HomeAssistantEntityId.TryNormalize(" light.kitchen ", out var normalized));
        Assert.Equal("light.kitchen", normalized);
        Assert.True(HomeAssistantEntityId.TryNormalize("light.kitchen__ceiling", out _));
        Assert.True(HomeAssistantEntityId.TryNormalizeDomain(" light ", out var domain));
        Assert.Equal("light", domain);
        Assert.False(HomeAssistantEntityId.TryNormalize("light.Kitchen", out _));
        Assert.False(HomeAssistantEntityId.TryNormalizeForDomain("light.kitchen", "LIGHT", out _));
        Assert.False(HomeAssistantEntityId.TryNormalizeDomain("li__ght", out _));
        Assert.False(HomeAssistantEntityId.TryNormalizeDomain("1sensor", out _));
        Assert.False(HomeAssistantEntityId.TryNormalize("1sensor.room", out _));
        Assert.True(HomeAssistantEntityId.TryNormalize("sensor." + new string('a', 248), out _));
        Assert.False(HomeAssistantEntityId.TryNormalize("sensor." + new string('a', 249), out _));
        foreach (var invalid in new[]
        {
            "_light.kitchen",
            "light_.kitchen",
            "li__ght.kitchen",
            "light._kitchen",
            "light.kitchen_"
        })
        {
            Assert.False(HomeAssistantEntityId.TryNormalize(invalid, out _));
        }
    }

    [Fact]
    public void BuiltInResponseDecoderClassifiesTypedJsonMismatches()
    {
        using var document = JsonDocument.Parse("{\"components\":\"api\"}");

        var exception = Assert.Throws<HomeAssistantProtocolException>(() =>
            HomeAssistantJson.DeserializeResponse<HomeAssistantConfiguration>(
                document.RootElement,
                "Configuration response failed."));

        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Theory]
    [InlineData("2026-08-27T18:00:00")]
    [InlineData("2026-08-27")]
    [InlineData("08/27/2026 18:00:00")]
    public void BuiltInResponseDecoderRejectsTimestampsWithoutAnExplicitOffset(string timestamp)
    {
        using var document = JsonDocument.Parse(
            "{\"entity_id\":\"sensor.clock\",\"state\":\"on\",\"attributes\":{},\"last_changed\":\""
            + timestamp
            + "\"}");

        var exception = Assert.Throws<HomeAssistantProtocolException>(() =>
            HomeAssistantJson.DeserializeResponse<HomeAssistantState>(
                document.RootElement,
                "State response failed."));

        Assert.IsType<JsonException>(exception.InnerException);
    }

    [Theory]
    [InlineData("2026-08-27T18:00:00Z")]
    [InlineData("2026-08-27T18:00:00.1234567+02:00")]
    public void BuiltInResponseDecoderAcceptsExactWireTimestamps(string timestamp)
    {
        using var document = JsonDocument.Parse(
            "{\"entity_id\":\"sensor.clock\",\"state\":\"on\",\"attributes\":{},\"last_changed\":\""
            + timestamp
            + "\"}");

        var state = HomeAssistantJson.DeserializeResponse<HomeAssistantState>(
            document.RootElement,
            "State response failed.");

        Assert.NotNull(state.LastChanged);
    }

    [Fact]
    public void RawStateAttributesRetainSystemTextJsonTimestampCompatibility()
    {
        using var document = JsonDocument.Parse("\"2026-08-27T18:00:00\"");
        var state = new HomeAssistantState();
        state.Attributes["provider_timestamp"] = document.RootElement.Clone();

        Assert.True(state.TryGetAttribute<DateTimeOffset>("provider_timestamp", out var timestamp));
        Assert.Equal(2026, timestamp.Year);
    }

    [Fact]
    public void JsonSnapshotHelpersClassifyInvalidValuesBeforeTransport()
    {
        var undefined = new Dictionary<string, object?> { ["value"] = default(JsonElement) };
        var cyclic = new Dictionary<string, object?>();
        cyclic["self"] = cyclic;

        Assert.Throws<ArgumentException>(() => HomeAssistantJson.FreezeObject(undefined, "value", "Value"));
        Assert.Throws<ArgumentException>(() => HomeAssistantJson.FreezeValue(cyclic, "value", "Value"));
    }

    [Fact]
    public void JsonSnapshotHelpersStopCallerGraphTraversalAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var values = new CancellationProbeEnumerable(cancellation);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantJson.FreezeValue(values, "value", "Value", cancellation.Token));
        Assert.InRange(values.ReadCount, 1, 16);

        using var objectCancellation = new CancellationTokenSource();
        var objectValues = new CancellationProbeEnumerable(objectCancellation);
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantJson.FreezeObject(
                new Dictionary<string, object?> { ["values"] = objectValues },
                "value",
                "Value",
                objectCancellation.Token));
        Assert.InRange(objectValues.ReadCount, 1, 16);
    }

    [Fact]
    public void TransportSerializationStopsCallerGraphTraversalAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var values = new CancellationProbeEnumerable(cancellation);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantJson.SerializeToUtf8Bytes(values, cancellation.Token));

        Assert.InRange(values.ReadCount, 1, 16);
    }

    [Fact]
    public void JsonSnapshotHelpersHonorCancellationForJsonDomValues()
    {
        using var document = JsonDocument.Parse("[1]");

        using (var rootCancellation = new CancellationTokenSource())
        {
            rootCancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                HomeAssistantJson.FreezeValue(
                    document.RootElement,
                    "value",
                    "Value",
                    rootCancellation.Token));
        }

        using (var nestedCancellation = new CancellationTokenSource())
        {
            nestedCancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() =>
                HomeAssistantJson.FreezeObject(
                    new Dictionary<string, object?> { ["value"] = document },
                    "value",
                    "Value",
                    nestedCancellation.Token));
        }
    }

    [Fact]
    public async Task ResponseSnapshotPrioritizesAPreCanceledToken()
    {
        var json = "[" + string.Join(",", Enumerable.Repeat("0", 1_000_000)) + "]";
        using var document = JsonDocument.Parse(json);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HomeAssistantJson.SnapshotResponseAsync(
                document.RootElement,
                "The response could not be snapshotted.",
                cancellation.Token));
    }

    [Fact]
    public async Task JsonStringDecodingCanBeCanceledWhileTheDecoderIsRunning()
    {
        using var document = JsonDocument.Parse("\"Current\"");
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var decoding = HomeAssistantJson.GetStringAsync(
            document.RootElement,
            cancellation.Token,
            element =>
            {
                try
                {
                    started.Set();
                    release.Wait();
                    return element.GetString();
                }
                finally
                {
                    finished.Set();
                }
            });

        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => decoding);
        }
        finally
        {
            release.Set();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(2)));
        }
    }

    [Fact]
    public void SynchronousResponseValidationUsesOneCancelableWorkerForTheWholeOperation()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var finished = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var validation = Task.Run(() => HomeAssistantJson.RunCancellationIsolated(
            () =>
            {
                try
                {
                    started.Set();
                    release.Wait();
                    return 42;
                }
                finally
                {
                    finished.Set();
                }
            },
            cancellation.Token));

        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(2)));
            cancellation.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => validation.GetAwaiter().GetResult());
        }
        finally
        {
            release.Set();
            Assert.True(finished.Wait(TimeSpan.FromSeconds(2)));
        }
    }

    [Fact]
    public async Task CalendarEventExtensionProjectionStopsAfterCancellation()
    {
        var json = "[{\"summary\":\"Planning\",\"start\":{\"dateTime\":\"2026-08-27T18:00:00Z\"},"
            + "\"end\":{\"dateTime\":\"2026-08-27T19:00:00Z\"},\"provider_payload\":["
            + string.Join(",", Enumerable.Repeat("0", 1_000_000))
            + "]}]";
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancelAfterReadStream(
            System.Text.Encoding.UTF8.GetBytes(json),
            cancellation,
            cancelAfterBytes: 1_024);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JsonSerializer.DeserializeAsync<HomeAssistantCalendarEvent[]>(
                    stream,
                    HomeAssistantJson.CreateCancellationAwareResponseOptions(cancellation.Token),
                    cancellation.Token)
                .AsTask());
        Assert.True(stream.CancellationTriggered);
    }

    [Fact]
    public async Task CalendarKnownStringProjectionPrioritizesAPreCanceledToken()
    {
        var json = JsonSerializer.Serialize(new string('x', 16_000_000));
        var payload = System.Text.Encoding.UTF8.GetBytes(json);
        using var cancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim(false);
        var operation = Task.Factory.StartNew(
            () =>
            {
                var reader = new Utf8JsonReader(payload);
                Assert.True(reader.Read());
                started.TrySetResult(true);
                release.Wait();
                return HomeAssistantCancellationJsonValueReader.ReadString(ref reader, cancellation.Token);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);

        await started.Task;
        cancellation.Cancel();
        release.Set();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await operation);
    }

    [Fact]
    public void CalendarKnownStringsPreserveEscapedJsonText()
    {
        const string json = "{\"summary\":\"Planning \\\"A\\\" \\u263A\",\"start\":{\"date\":\"2026-08-27\"},\"end\":{\"date\":\"2026-08-28\"}}";

        var value = JsonSerializer.Deserialize<HomeAssistantCalendarEvent>(
            json,
            HomeAssistantJson.CreateCancellationAwareResponseOptions(CancellationToken.None));

        Assert.NotNull(value);
        Assert.Equal("Planning \"A\" ☺", value.Summary);
    }

    [Fact]
    public async Task GenericExtensionProjectionStopsAfterCancellation()
    {
        var json = "[{\"notification_id\":\"notice\",\"message\":\"Ready\",\"provider_payload\":["
            + string.Join(",", Enumerable.Repeat("0", 1_000_000))
            + "]}]";
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancelAfterReadStream(
            System.Text.Encoding.UTF8.GetBytes(json),
            cancellation,
            cancelAfterBytes: 1_024);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            JsonSerializer.DeserializeAsync<HomeAssistantPersistentNotification[]>(
                    stream,
                    HomeAssistantJson.CreateCancellationAwareResponseOptions(cancellation.Token),
                    cancellation.Token)
                .AsTask());
        Assert.True(stream.CancellationTriggered);
    }

    [Fact]
    public void GenericScalarExtensionProjectionHonorsPreCancellation()
    {
        using var document = JsonDocument.Parse(
            "{\"notification_id\":\"notice\",\"message\":\"Ready\",\"provider_payload\":\""
            + new string('x', 1_000_000)
            + "\"}");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantJson.DeserializeResponse<HomeAssistantPersistentNotification>(
                document.RootElement,
                "The notification could not be decoded.",
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public void BuiltInResponseValidationStopsCollectionTraversalAfterCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var values = new CancellationProbeEnumerable(cancellation);

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantJson.RequireNoNullCollectionEntries(
                values,
                "The response was invalid.",
                cancellationToken: cancellation.Token));

        Assert.InRange(values.ReadCount, 1, 16);
    }

    [Fact]
    public void BuiltInResponseDecoderHonorsCallerCancellation()
    {
        using var document = JsonDocument.Parse("[{\"entity_id\":\"light.kitchen\",\"state\":\"on\",\"attributes\":{}}]");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantJson.DeserializeResponse<HomeAssistantState[]>(
                document.RootElement,
                "State response failed.",
                cancellationToken: cancellation.Token));
    }

    [Fact]
    public void BuiltInResponseSemanticTransformsHonorCallerCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;
        using var extendedRegistry = JsonDocument.Parse("{}");

        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantStateClient.ValidateSnapshotStates(
                new[] { new HomeAssistantState { EntityId = "light.kitchen", State = "on" } },
                token));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantRegistryClient.DeserializeExtendedEntities(
                extendedRegistry.RootElement,
                new[] { new HomeAssistantEntityRegistryEntry { EntityId = "light.kitchen" } },
                token));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantCalendarClient.ValidateEvents(
                new[] { new HomeAssistantCalendarEvent() },
                token));
        Assert.ThrowsAny<OperationCanceledException>(() =>
            HomeAssistantCalendarClient.ValidateEvents(
                Array.Empty<HomeAssistantCalendarEvent>(),
                token));
    }

    [Fact]
    public void DomainTargetValidationHonorsCallerCancellation()
    {
        var target = HomeAssistantX.Services.HomeAssistantTarget.ForEntity("light.kitchen");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() =>
            target.NormalizeRequiredForDomain("light", nameof(target), cancellation.Token));
    }

    [Fact]
    public void NonEntityTargetNormalizationObservesCancellationDuringTraversal()
    {
        using var cancellation = new CancellationTokenSource();
        var target = new HomeAssistantX.Services.HomeAssistantTarget
        {
            AreaIds = new CancellationProbeStringList(cancellation, "kitchen")
        };

        Assert.ThrowsAny<OperationCanceledException>(() =>
            target.NormalizeRequiredForDomain("light", nameof(target), cancellation.Token));
    }

    [Fact]
    public void StateAttributeDictionaryConverterHonorsResponseCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var scope = HomeAssistantAttributeDictionaryConverter.UseCancellationToken(cancellation.Token);
        var options = new JsonSerializerOptions();
        options.Converters.Add(new HomeAssistantAttributeDictionaryConverter());

        Assert.ThrowsAny<OperationCanceledException>(() =>
            JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
                "{\"provider_payload\":[1,2,3]}",
                options));
    }

    [Theory]
    [InlineData("sensor.kitchen")]
    [InlineData(" media_player.kitchen")]
    [InlineData("media_player.kitchen ")]
    [InlineData("MEDIA_PLAYER.kitchen")]
    public void ServerEntityDomainMismatchOrNoncanonicalIdIsAProtocolFailure(string entityId)
    {
        var state = new HomeAssistantState { EntityId = entityId, State = "on" };

        Assert.Throws<HomeAssistantProtocolException>(() =>
            HomeAssistantEntityId.RequireResponseDomain(state, "media_player"));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("{\"dateTime\":\"not-a-timestamp\"}")]
    [InlineData("{\"dateTime\":\"2026-08-27\"}")]
    [InlineData("{\"dateTime\":\"08/27/2026\"}")]
    [InlineData("{\"date\":42}")]
    [InlineData("\"\"")]
    [InlineData("\"2026-08-27T18:00:00\"")]
    [InlineData("{\"date\":\"not-a-date\"}")]
    [InlineData("{}")]
    [InlineData("{\"date\":\"2026-08-27\",\"dateTime\":\"2026-08-27T18:00:00Z\"}")]
    [InlineData("{\"date\":\"2026-08-26\",\"date\":\"2026-08-27\"}")]
    [InlineData("{\"dateTime\":\"2026-08-27T18:00:00Z\",\"dateTime\":\"2026-08-28T18:00:00Z\"}")]
    public void CalendarBoundaryConverterRejectsInvalidTypedShapesWithJsonException(string json)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<HomeAssistantCalendarBoundary>(json));
    }

    [Theory]
    [InlineData("\"2026-08-27\"")]
    [InlineData("\"2026-08-27T18:00:00Z\"")]
    [InlineData("\"2026-08-27T18:00:00+02:00\"")]
    public void CalendarBoundaryConverterAcceptsUnambiguousStringForms(string json)
    {
        Assert.NotNull(JsonSerializer.Deserialize<HomeAssistantCalendarBoundary>(json));
    }

    private sealed class CancellationProbeEnumerable : IEnumerable<string>
    {
        private readonly CancellationTokenSource _cancellation;

        internal CancellationProbeEnumerable(CancellationTokenSource cancellation)
        {
            _cancellation = cancellation;
        }

        internal int ReadCount { get; private set; }

        public IEnumerator<string> GetEnumerator()
        {
            for (var index = 0; index < 1000; index++)
            {
                ReadCount++;
                if (ReadCount == 1) _cancellation.Cancel();
                if (ReadCount > 16) throw new InvalidOperationException("Serialization continued after cancellation.");
                yield return new string('x', 4096);
            }
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CancellationProbeStringList : IReadOnlyList<string>
    {
        private readonly CancellationTokenSource _cancellation;
        private readonly string _value;

        internal CancellationProbeStringList(CancellationTokenSource cancellation, string value)
        {
            _cancellation = cancellation;
            _value = value;
        }

        public int Count => 1;

        public string this[int index]
        {
            get
            {
                if (index != 0) throw new ArgumentOutOfRangeException(nameof(index));
                _cancellation.Cancel();
                return _value;
            }
        }

        public IEnumerator<string> GetEnumerator()
        {
            yield return this[0];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class CancelAfterReadStream : Stream
    {
        private const int MaximumReadSize = 128;
        private readonly MemoryStream _inner;
        private readonly CancellationTokenSource _cancellation;
        private readonly int _cancelAfterBytes;
        private int _bytesRead;
        private int _cancellationTriggered;

        internal CancelAfterReadStream(
            byte[] content,
            CancellationTokenSource cancellation,
            int cancelAfterBytes)
        {
            _inner = new MemoryStream(content, writable: false);
            _cancellation = cancellation;
            _cancelAfterBytes = cancelAfterBytes;
        }

        internal bool CancellationTriggered => Volatile.Read(ref _cancellationTriggered) != 0;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, Math.Min(count, MaximumReadSize));
            Observe(read);
            return read;
        }

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await _inner.ReadAsync(
                buffer,
                offset,
                Math.Min(count, MaximumReadSize),
                cancellationToken).ConfigureAwait(false);
            Observe(read);
            return read;
        }

#if NET10_0
        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await _inner.ReadAsync(
                buffer.Slice(0, Math.Min(buffer.Length, MaximumReadSize)),
                cancellationToken).ConfigureAwait(false);
            Observe(read);
            return read;
        }
#endif

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }

        private void Observe(int count)
        {
            if (count <= 0) return;
            var total = Interlocked.Add(ref _bytesRead, count);
            if (total >= _cancelAfterBytes
                && Interlocked.Exchange(ref _cancellationTriggered, 1) == 0)
            {
                _cancellation.Cancel();
            }
        }
    }
}
