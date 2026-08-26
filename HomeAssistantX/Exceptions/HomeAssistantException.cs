namespace HomeAssistantX.Exceptions;

/// <summary>Base exception for failures classified by HomeAssistantX.</summary>
public class HomeAssistantException : Exception
{
    public HomeAssistantException(string message)
        : base(message)
    {
    }

    public HomeAssistantException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class HomeAssistantAuthenticationException : HomeAssistantException
{
    public HomeAssistantAuthenticationException(string message)
        : base(message)
    {
    }
}

public sealed class HomeAssistantConnectionException : HomeAssistantException
{
    public HomeAssistantConnectionException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class HomeAssistantProtocolException : HomeAssistantException
{
    public HomeAssistantProtocolException(string message)
        : base(message)
    {
    }

    public HomeAssistantProtocolException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public sealed class HomeAssistantCommandException : HomeAssistantException
{
    public HomeAssistantCommandException(string code, string message, string? translationKey = null)
        : base(message)
    {
        Code = code;
        TranslationKey = translationKey;
    }

    public string Code { get; }

    public string? TranslationKey { get; }
}

/// <summary>Raised when a friendly Home Assistant identifier cannot be resolved safely.</summary>
public sealed class HomeAssistantLookupException : HomeAssistantException
{
    public HomeAssistantLookupException(string message)
        : base(message)
    {
    }
}
