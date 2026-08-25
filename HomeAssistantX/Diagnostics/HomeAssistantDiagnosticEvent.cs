namespace HomeAssistantX.Diagnostics;

public enum HomeAssistantDiagnosticLevel
{
    Trace,
    Information,
    Warning,
    Error
}

/// <summary>A token-safe diagnostic emitted at a transport or lifecycle boundary.</summary>
public sealed class HomeAssistantDiagnosticEvent
{
    public HomeAssistantDiagnosticEvent(
        HomeAssistantDiagnosticLevel level,
        string name,
        string message,
        Exception? exception = null)
    {
        Level = level;
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Message = message ?? throw new ArgumentNullException(nameof(message));
        Exception = exception;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public DateTimeOffset Timestamp { get; }

    public HomeAssistantDiagnosticLevel Level { get; }

    public string Name { get; }

    public string Message { get; }

    public Exception? Exception { get; }
}

public interface IHomeAssistantDiagnosticsSink
{
    void Write(HomeAssistantDiagnosticEvent diagnosticEvent);
}

internal sealed class NullHomeAssistantDiagnosticsSink : IHomeAssistantDiagnosticsSink
{
    public static NullHomeAssistantDiagnosticsSink Instance { get; } = new();

    public void Write(HomeAssistantDiagnosticEvent diagnosticEvent)
    {
    }
}
