namespace HomeAssistantX.Services;

/// <summary>Identifies the transport used by typed Home Assistant controls.</summary>
public enum HomeAssistantServiceCallTransport
{
    /// <summary>Invokes typed controls through the Home Assistant WebSocket API.</summary>
    WebSocket,

    /// <summary>Invokes typed controls through the Home Assistant REST API.</summary>
    Rest
}
