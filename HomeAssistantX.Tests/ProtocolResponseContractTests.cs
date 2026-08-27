using System.Text.Json;
using HomeAssistantX.Exceptions;
using HomeAssistantX.Models;
using HomeAssistantX.Protocol;

namespace HomeAssistantX.Tests;

public sealed class ProtocolResponseContractTests
{
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
