using System.Net;

namespace HomeAssistantX.Discovery;

/// <summary>A Home Assistant instance advertised through DNS service discovery.</summary>
public sealed class HomeAssistantDiscoveredInstance
{
    public string ServiceInstanceName { get; internal set; } = string.Empty;

    public string Name { get; internal set; } = string.Empty;

    public string? InstanceId { get; internal set; }

    public string? Version { get; internal set; }

    public string? HostName { get; internal set; }

    public int? Port { get; internal set; }

    public Uri? InternalUri { get; internal set; }

    public Uri? ExternalUri { get; internal set; }

    public Uri? BaseUri { get; internal set; }

    public bool RequiresApiPassword { get; internal set; }

    public IReadOnlyList<IPAddress> Addresses { get; internal set; } = Array.Empty<IPAddress>();

    public IReadOnlyDictionary<string, string?> Properties { get; internal set; }
        = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
}
