using System.Collections.Concurrent;
using System.Management.Automation;
using System.Management.Automation.Runspaces;

namespace HomeAssistantX.PowerShell;

/// <summary>Owns the default Home Assistant connection for each PowerShell runspace.</summary>
public static class HomeAssistantSession
{
    private static readonly ConcurrentDictionary<Guid, HomeAssistantConnection> Connections = new();

    public static HomeAssistantConnection? Current
    {
        get
        {
            var runspaceId = GetCurrentRunspaceId();
            return Connections.TryGetValue(runspaceId, out var connection) && !connection.IsDisposed
                ? connection
                : null;
        }
    }

    internal static Guid GetCurrentRunspaceId()
    {
        return Runspace.DefaultRunspace?.InstanceId
            ?? throw new InvalidOperationException("HomeAssistantX could not identify the current PowerShell runspace.");
    }

    internal static HomeAssistantConnection GetRequired(Guid runspaceId)
    {
        if (Connections.TryGetValue(runspaceId, out var connection) && !connection.IsDisposed)
        {
            return connection;
        }

        throw new InvalidOperationException(
            "No default Home Assistant connection exists in this PowerShell runspace. Run Connect-HomeAssistant or pass -Connection.");
    }

    internal static void Set(Guid runspaceId, HomeAssistantConnection connection)
    {
        if (connection is null)
        {
            throw new ArgumentNullException(nameof(connection));
        }

        Connections.AddOrUpdate(
            runspaceId,
            connection,
            (_, previous) =>
            {
                if (!ReferenceEquals(previous, connection))
                {
                    previous.Dispose();
                }

                return connection;
            });
    }

    internal static bool Clear(Guid runspaceId, HomeAssistantConnection? expected = null, bool dispose = false)
    {
        if (!Connections.TryGetValue(runspaceId, out var current)
            || (expected is not null && !ReferenceEquals(current, expected))
            || !Connections.TryRemove(runspaceId, out current))
        {
            return false;
        }

        if (dispose)
        {
            current.Dispose();
        }

        return true;
    }
}

/// <summary>Releases the runspace's default connection when the binary module is removed.</summary>
public sealed class HomeAssistantModuleLifecycle : IModuleAssemblyCleanup
{
    public void OnRemove(PSModuleInfo psModuleInfo)
    {
        var runspace = Runspace.DefaultRunspace;
        if (runspace is not null)
        {
            HomeAssistantSession.Clear(runspace.InstanceId, dispose: true);
        }
    }
}
