using HomeAssistantX.Authentication;
using HomeAssistantX.Configuration;

namespace HomeAssistantX.Tests.Infrastructure;

internal static class TestClientFactory
{
    public static HomeAssistantClient Create(
        TestHomeAssistantServer server,
        string? token = null,
        int subscriptionBufferCapacity = 16,
        TimeSpan? requestTimeout = null,
        int maximumRestResponseBytes = 64 * 1024 * 1024,
        IHomeAssistantAccessTokenProvider? accessTokenProvider = null,
        int maximumCoalescedWebSocketMessages = 4096)
    {
        var options = new HomeAssistantClientOptions(
            server.BaseUri,
            accessTokenProvider
                ?? new StaticAccessTokenProvider(token ?? TestHomeAssistantServer.AccessToken))
        {
            RequestTimeout = requestTimeout ?? TimeSpan.FromSeconds(3),
            ConnectTimeout = TimeSpan.FromSeconds(3),
            ReconnectMinimumDelay = TimeSpan.FromMilliseconds(10),
            ReconnectMaximumDelay = TimeSpan.FromMilliseconds(50),
            SubscriptionBufferCapacity = subscriptionBufferCapacity,
            MaximumRestResponseBytes = maximumRestResponseBytes,
            MaximumCoalescedWebSocketMessages = maximumCoalescedWebSocketMessages
        };
        return new HomeAssistantClient(options);
    }
}
