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

    protected HomeAssistantClient Client
    {
        get
        {
            var connection = ActiveConnection;
            if (connection.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(HomeAssistantConnection));
            }

            return connection.Client;
        }
    }
}
