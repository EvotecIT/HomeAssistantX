using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;

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
}
