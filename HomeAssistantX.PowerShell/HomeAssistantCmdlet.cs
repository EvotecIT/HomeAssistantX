using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Common explicit-or-default session contract for HomeAssistantX cmdlets.</summary>
public abstract class HomeAssistantCmdlet : AsyncPSCmdlet
{
    private Guid? _runspaceId;

    /// <summary>Optional explicit session returned by <c>Connect-HomeAssistant</c>. It also accepts pipeline input.</summary>
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public HomeAssistantConnection? Connection { get; set; }

    protected Guid CurrentRunspaceId => _runspaceId ??= HomeAssistantSession.GetCurrentRunspaceId();

    protected HomeAssistantConnection ActiveConnection => Connection ?? HomeAssistantSession.GetRequired(CurrentRunspaceId);

    /// <summary>Returns the operator-assigned connection name without disclosing its endpoint.</summary>
    protected string ConnectionDisplayName => RequireUsableConnection().ConfirmationName;

    protected HomeAssistantClient Client
    {
        get
        {
            return RequireUsableConnection().Client;
        }
    }

    /// <summary>Validates the current connection before an operation can display a confirmation prompt.</summary>
    public new bool ShouldProcess(string? target)
    {
        _ = RequireUsableConnection();
        return base.ShouldProcess(target);
    }

    /// <summary>Validates the current connection before an operation can display a confirmation prompt.</summary>
    public new bool ShouldProcess(string? target, string action)
    {
        _ = RequireUsableConnection();
        return base.ShouldProcess(target, action);
    }

    /// <summary>Validates the current connection before an operation can display a confirmation prompt.</summary>
    public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption)
    {
        _ = RequireUsableConnection();
        return base.ShouldProcess(verboseDescription, verboseWarning, caption);
    }

    /// <summary>Validates the current connection before an operation can display a confirmation prompt.</summary>
    public new bool ShouldProcess(
        string verboseDescription,
        string verboseWarning,
        string caption,
        out ShouldProcessReason shouldProcessReason)
    {
        _ = RequireUsableConnection();
        return base.ShouldProcess(verboseDescription, verboseWarning, caption, out shouldProcessReason);
    }

    private HomeAssistantConnection RequireUsableConnection()
    {
        var connection = ActiveConnection;
        if (connection.IsDisposed)
        {
            throw new ObjectDisposedException(nameof(HomeAssistantConnection));
        }

        return connection;
    }
}
