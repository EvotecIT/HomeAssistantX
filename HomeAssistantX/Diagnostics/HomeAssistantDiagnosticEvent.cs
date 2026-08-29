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
        Exception = exception is null
            ? null
            : HomeAssistantDiagnosticFailure.Sanitize(exception, message);
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

internal static class HomeAssistantDiagnosticFailure
{
    internal static Exception Sanitize(Exception failure, string message)
    {
        if (failure is Exceptions.HomeAssistantCommandException commandFailure)
        {
            return new Exceptions.HomeAssistantCommandException(
                IsSafeCommandCode(commandFailure.Code) ? commandFailure.Code : "unknown_error",
                message);
        }

        if (failure is Exceptions.HomeAssistantAuthenticationException)
        {
            return new Exceptions.HomeAssistantAuthenticationException(message);
        }

        if (failure is Exceptions.HomeAssistantProtocolException)
        {
            return new Exceptions.HomeAssistantProtocolException(message);
        }

        return new Exceptions.HomeAssistantException(message);
    }

    private static bool IsSafeCommandCode(string value)
    {
        if (value.Length is < 1 or > 64)
        {
            return false;
        }

        foreach (var character in value)
        {
            if ((character < 'a' || character > 'z')
                && (character < '0' || character > '9')
                && character != '_')
            {
                return false;
            }
        }

        return true;
    }
}
