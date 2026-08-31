using HomeAssistantX.Authentication;
using HomeAssistantX.Diagnostics;
using HomeAssistantX.Services;

namespace HomeAssistantX.Configuration;

/// <summary>Configures the Home Assistant connection and bounded transport behavior.</summary>
public sealed class HomeAssistantClientOptions
{
    public HomeAssistantClientOptions(Uri baseUri, IHomeAssistantAccessTokenProvider accessTokenProvider)
    {
        BaseUri = HomeAssistantUri.NormalizeBaseUri(baseUri);
        AccessTokenProvider = accessTokenProvider ?? throw new ArgumentNullException(nameof(accessTokenProvider));
    }

    public Uri BaseUri { get; }

    public IHomeAssistantAccessTokenProvider AccessTokenProvider { get; }

    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ConnectTimeout { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan KeepAliveInterval { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan ReconnectMinimumDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan ReconnectMaximumDelay { get; set; } = TimeSpan.FromSeconds(30);

    public int MaximumWebSocketMessageBytes { get; set; } = 64 * 1024 * 1024;

    /// <summary>Maximum number of messages accepted in one Home Assistant coalesced WebSocket batch.</summary>
    public int MaximumCoalescedWebSocketMessages { get; set; } = 4096;

    /// <summary>Enables Home Assistant WebSocket message coalescing during feature negotiation.</summary>
    public bool EnableWebSocketMessageCoalescing { get; set; } = true;

    /// <summary>
    /// Gets or sets the transport used by typed clients under <c>HomeAssistantClient.Controls</c>.
    /// Explicit calls through <c>HomeAssistantClient.Services</c> retain their selected method.
    /// </summary>
    public HomeAssistantServiceCallTransport ControlServiceCallTransport { get; set; }
        = HomeAssistantServiceCallTransport.WebSocket;

    public int MaximumRestResponseBytes { get; set; } = 64 * 1024 * 1024;

    public int SubscriptionBufferCapacity { get; set; } = 256;

    public IHomeAssistantDiagnosticsSink Diagnostics { get; set; } = NullHomeAssistantDiagnosticsSink.Instance;

    internal void Validate()
    {
        if (RequestTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(RequestTimeout));
        }

        if (ConnectTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(ConnectTimeout));
        }

        if (MaximumWebSocketMessageBytes < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumWebSocketMessageBytes));
        }

        if (MaximumRestResponseBytes < 1024)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumRestResponseBytes));
        }

        if (MaximumCoalescedWebSocketMessages < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(MaximumCoalescedWebSocketMessages));
        }

        if (SubscriptionBufferCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(SubscriptionBufferCapacity));
        }

        if (!Enum.IsDefined(typeof(HomeAssistantServiceCallTransport), ControlServiceCallTransport))
        {
            throw new ArgumentOutOfRangeException(nameof(ControlServiceCallTransport));
        }

        if (ReconnectMinimumDelay < TimeSpan.Zero || ReconnectMaximumDelay < ReconnectMinimumDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(ReconnectMaximumDelay));
        }
    }
}
