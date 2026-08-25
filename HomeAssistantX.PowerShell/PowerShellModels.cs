namespace HomeAssistantX.PowerShell;

public sealed class HomeAssistantLogLine
{
    public string Source { get; set; } = string.Empty;

    public int LineNumber { get; set; }

    public string Text { get; set; } = string.Empty;
}

public enum HomeAssistantAppAction
{
    Install,
    Update,
    Start,
    Stop,
    Restart,
    Uninstall
}
