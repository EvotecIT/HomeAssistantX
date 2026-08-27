using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;
using HomeAssistantX.Rest;

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
    [InlineData("\"2026-08-27T18:00:00\"")]
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
}
