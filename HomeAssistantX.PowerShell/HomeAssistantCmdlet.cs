using System.Management.Automation;
using System.Security.Cryptography;
using System.Text;

namespace HomeAssistantX.PowerShell;

/// <summary>Common explicit-or-default session contract for HomeAssistantX cmdlets.</summary>
public abstract class HomeAssistantCmdlet : AsyncPSCmdlet
{
    private static readonly byte[] ConfirmationFingerprintKey = CreateConfirmationFingerprintKey();
    private Guid? _runspaceId;

    /// <summary>Optional explicit session returned by <c>Connect-HomeAssistant</c>. It also accepts pipeline input.</summary>
    [Parameter(ValueFromPipeline = true)]
    [ValidateNotNull]
    public HomeAssistantConnection? Connection { get; set; }

    protected Guid CurrentRunspaceId => _runspaceId ??= HomeAssistantSession.GetCurrentRunspaceId();

    protected HomeAssistantConnection ActiveConnection => Connection ?? HomeAssistantSession.GetRequired(CurrentRunspaceId);

    /// <summary>Returns the operator-assigned connection name without disclosing its endpoint.</summary>
    protected string ConnectionDisplayName => RequireUsableConnection().ConfirmationName;

    /// <summary>Returns a process-scoped privacy-safe descriptor for confirmation action details.</summary>
    protected string ConfirmationAction(string? value) => ConfirmationTarget(RequireUsableConnection(), value);

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
        var connection = RequireUsableConnection();
        return base.ShouldProcess(ConfirmationTarget(connection, target));
    }

    /// <summary>Validates the current connection before an operation can display a confirmation prompt.</summary>
    public new bool ShouldProcess(string? target, string action)
    {
        var connection = RequireUsableConnection();
        return base.ShouldProcess(ConfirmationTarget(connection, target), action);
    }

    /// <summary>Validates the current connection before an operation can display a confirmation prompt.</summary>
    public new bool ShouldProcess(string verboseDescription, string verboseWarning, string caption)
    {
        var connection = RequireUsableConnection();
        return base.ShouldProcess(
            "Perform the requested Home Assistant operation on " + connection.ConfirmationName + ".",
            "Perform the requested Home Assistant operation on " + connection.ConfirmationName + "?",
            "Confirm Home Assistant operation");
    }

    /// <summary>Validates the current connection before an operation can display a confirmation prompt.</summary>
    public new bool ShouldProcess(
        string verboseDescription,
        string verboseWarning,
        string caption,
        out ShouldProcessReason shouldProcessReason)
    {
        var connection = RequireUsableConnection();
        return base.ShouldProcess(
            "Perform the requested Home Assistant operation on " + connection.ConfirmationName + ".",
            "Perform the requested Home Assistant operation on " + connection.ConfirmationName + "?",
            "Confirm Home Assistant operation",
            out shouldProcessReason);
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

    private static string ConfirmationTarget(HomeAssistantConnection connection, string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return "Home Assistant target on " + connection.ConfirmationName;
        }

        // Confirmation output can be collected in CI logs or transcripts. A process-scoped
        // keyed fingerprint distinguishes targets without making predictable entity, app,
        // dashboard, statistic, or local-path names recoverable from an unsalted hash.
        var bytes = Encoding.UTF8.GetBytes(target!.Trim());
        byte[] hash;
        using (var algorithm = new HMACSHA256(ConfirmationFingerprintKey))
        {
            hash = algorithm.ComputeHash(bytes);
        }

        var fingerprint = new StringBuilder(8);
        for (var index = 0; index < 4; index++)
        {
            fingerprint.Append(hash[index].ToString("X2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return "Home Assistant target [" + fingerprint + "] on " + connection.ConfirmationName;
    }

    private static byte[] CreateConfirmationFingerprintKey()
    {
        var key = new byte[32];
        using (var random = RandomNumberGenerator.Create())
        {
            random.GetBytes(key);
        }

        return key;
    }
}
