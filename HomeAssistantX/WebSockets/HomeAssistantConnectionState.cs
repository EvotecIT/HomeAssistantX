namespace HomeAssistantX.WebSockets;

public enum HomeAssistantConnectionState
{
    Disconnected,
    Connecting,
    Connected,
    Reconnecting,
    Faulted
}

public sealed class HomeAssistantConnectionStateChangedEventArgs : EventArgs
{
    public HomeAssistantConnectionStateChangedEventArgs(
        HomeAssistantConnectionState previousState,
        HomeAssistantConnectionState currentState,
        Exception? exception = null)
    {
        PreviousState = previousState;
        CurrentState = currentState;
        Exception = exception;
    }

    public HomeAssistantConnectionState PreviousState { get; }

    public HomeAssistantConnectionState CurrentState { get; }

    public Exception? Exception { get; }
}
