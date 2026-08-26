using System.Management.Automation;

namespace HomeAssistantX.PowerShell;

/// <summary>Common explicit-session contract for HomeAssistantX cmdlets.</summary>
public abstract class HomeAssistantCmdlet : AsyncPSCmdlet
{
    /// <summary>Explicit session returned by <c>Connect-HomeAssistant</c>. It also accepts pipeline input.</summary>
    [Parameter(Mandatory = true, ValueFromPipeline = true)]
    [ValidateNotNull]
    public HomeAssistantConnection Connection { get; set; } = null!;

    protected HomeAssistantClient Client
    {
        get
        {
            if (Connection.IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Connection));
            }

            return Connection.Client;
        }
    }
}
