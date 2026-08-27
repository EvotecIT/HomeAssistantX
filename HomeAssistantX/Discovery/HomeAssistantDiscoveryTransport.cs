using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HomeAssistantX.Discovery;

internal interface IHomeAssistantDiscoveryTransportFactory
{
    IReadOnlyList<HomeAssistantDiscoveryInterface> GetLocalInterfaces();

    IHomeAssistantDiscoveryTransport Create(IPAddress localAddress);
}

internal sealed class HomeAssistantDiscoveryInterface
{
    internal HomeAssistantDiscoveryInterface(string id, IReadOnlyList<IPAddress> addresses)
    {
        Id = id;
        Addresses = addresses;
    }

    internal string Id { get; }
    internal IReadOnlyList<IPAddress> Addresses { get; }
}

internal interface IHomeAssistantDiscoveryTransport : IDisposable
{
    Task SendAsync(byte[] query, CancellationToken cancellationToken);

    Task<byte[]> ReceiveAsync(CancellationToken cancellationToken);
}

internal sealed class UdpHomeAssistantDiscoveryTransportFactory : IHomeAssistantDiscoveryTransportFactory
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.0.251");
    private const int MulticastPort = 5353;
    private const int MaximumAddresses = 32;

    public IReadOnlyList<HomeAssistantDiscoveryInterface> GetLocalInterfaces()
    {
        var readers = new List<Func<HomeAssistantDiscoveryInterface?>>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                if (!IsEligible(network.OperationalStatus, network.SupportsMulticast, network.NetworkInterfaceType))
                {
                    continue;
                }

                readers.Add(() => new HomeAssistantDiscoveryInterface(
                    network.Id,
                    network.GetIPProperties().UnicastAddresses.Select(address => address.Address).ToArray()));
            }
            catch (NetworkInformationException)
            {
                // Interfaces can disappear while the operating system snapshot is being enumerated.
            }
        }

        return CollectLocalInterfaces(readers);
    }

    internal static IReadOnlyList<HomeAssistantDiscoveryInterface> CollectLocalInterfaces(
        IEnumerable<Func<HomeAssistantDiscoveryInterface?>> readers)
    {
        var interfaces = new List<HomeAssistantDiscoveryInterface>();
        foreach (var reader in readers)
        {
            try
            {
                var network = reader();
                if (network is null) continue;
                var addresses = FilterAddresses(network.Addresses);
                if (addresses.Count > 0) interfaces.Add(new HomeAssistantDiscoveryInterface(network.Id, addresses));
            }
            catch (NetworkInformationException) { }
            catch (SocketException) { }
        }

        var ordered = interfaces
            .OrderBy(value => value.Id, StringComparer.Ordinal)
            .ToArray();
        var selected = ordered.ToDictionary(value => value.Id, _ => new List<IPAddress>(), StringComparer.Ordinal);
        for (var addressIndex = 0; selected.Sum(value => value.Value.Count) < MaximumAddresses; addressIndex++)
        {
            var added = false;
            foreach (var network in ordered)
            {
                if (addressIndex >= network.Addresses.Count) continue;
                selected[network.Id].Add(network.Addresses[addressIndex]);
                added = true;
                if (selected.Sum(value => value.Value.Count) >= MaximumAddresses) break;
            }
            if (!added) break;
        }

        return ordered
            .Where(value => selected[value.Id].Count > 0)
            .Select(value => new HomeAssistantDiscoveryInterface(value.Id, selected[value.Id].ToArray()))
            .ToArray();
    }

    internal static bool IsEligible(
        OperationalStatus operationalStatus,
        bool supportsMulticast,
        NetworkInterfaceType interfaceType)
        => operationalStatus == OperationalStatus.Up
            && supportsMulticast
            && interfaceType != NetworkInterfaceType.Loopback;

    internal static IReadOnlyList<IPAddress> CollectLocalAddresses(
        IEnumerable<Func<IReadOnlyList<IPAddress>>> addressReaders)
    {
        return CollectLocalInterfaces(addressReaders.Select((reader, index) =>
                new Func<HomeAssistantDiscoveryInterface?>(() => new HomeAssistantDiscoveryInterface(index.ToString(System.Globalization.CultureInfo.InvariantCulture), reader()))))
            .SelectMany(value => value.Addresses)
            .ToArray();
    }

    private static IReadOnlyList<IPAddress> FilterAddresses(IEnumerable<IPAddress> addresses)
        => addresses
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(address)
                && !address.Equals(IPAddress.Any))
            .Distinct()
            .OrderBy(address => address.ToString(), StringComparer.Ordinal)
            .ToArray();

    public IHomeAssistantDiscoveryTransport Create(IPAddress localAddress)
        => new UdpHomeAssistantDiscoveryTransport(localAddress, MulticastAddress, MulticastPort);
}

internal sealed class UdpHomeAssistantDiscoveryTransport : IHomeAssistantDiscoveryTransport
{
    private readonly UdpClient _client;
    private readonly IPEndPoint _endpoint;
    private int _disposed;

    internal UdpHomeAssistantDiscoveryTransport(IPAddress localAddress, IPAddress multicastAddress, int multicastPort)
    {
        _client = new UdpClient(AddressFamily.InterNetwork);
        try
        {
            _client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            _client.Client.Bind(new IPEndPoint(localAddress, 0));
            ConfigureOutboundInterface(_client.Client, localAddress);
            ConfigureMulticastTimeToLive(_client.Client);
            _client.JoinMulticastGroup(multicastAddress, localAddress);
            _endpoint = new IPEndPoint(multicastAddress, multicastPort);
        }
        catch
        {
            _client.Dispose();
            throw;
        }
    }

    internal static void ConfigureOutboundInterface(Socket socket, IPAddress localAddress)
    {
        if (socket is null) throw new ArgumentNullException(nameof(socket));
        if (localAddress is null) throw new ArgumentNullException(nameof(localAddress));
        if (localAddress.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("An IPv4 local address is required.", nameof(localAddress));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, localAddress.GetAddressBytes());
    }

    internal static void ConfigureMulticastTimeToLive(Socket socket)
    {
        if (socket is null) throw new ArgumentNullException(nameof(socket));
        socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 255);
    }

    public async Task SendAsync(byte[] query, CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(state => ((UdpHomeAssistantDiscoveryTransport)state!).Dispose(), this);
        try
        {
            _ = await _client.SendAsync(query, query.Length, _endpoint).ConfigureAwait(false);
        }
        catch (Exception ex) when ((ex is ObjectDisposedException || ex is SocketException) && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The mDNS send was canceled.", ex, cancellationToken);
        }
    }

    public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        using var registration = cancellationToken.Register(state => ((UdpHomeAssistantDiscoveryTransport)state!).Dispose(), this);
        try
        {
            return (await _client.ReceiveAsync().ConfigureAwait(false)).Buffer;
        }
        catch (Exception ex) when ((ex is ObjectDisposedException || ex is SocketException) && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The mDNS receive was canceled.", ex, cancellationToken);
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _client.Dispose();
        }
    }
}
