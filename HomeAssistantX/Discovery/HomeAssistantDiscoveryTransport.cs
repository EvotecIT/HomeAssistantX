using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HomeAssistantX.Discovery;

internal interface IHomeAssistantDiscoveryTransportFactory
{
    IReadOnlyList<IPAddress> GetLocalAddresses();

    IHomeAssistantDiscoveryTransport Create(IPAddress localAddress);
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
    private const int MaximumInterfaces = 32;

    public IReadOnlyList<IPAddress> GetLocalAddresses()
    {
        var addressReaders = new List<Func<IReadOnlyList<IPAddress>>>();
        foreach (var network in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                if (network.OperationalStatus != OperationalStatus.Up
                    || !network.SupportsMulticast
                    || network.NetworkInterfaceType == NetworkInterfaceType.Loopback
                    || network.NetworkInterfaceType == NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                addressReaders.Add(() => network.GetIPProperties().UnicastAddresses
                    .Select(address => address.Address)
                    .ToArray());
            }
            catch (NetworkInformationException)
            {
                // Interfaces can disappear while the operating system snapshot is being enumerated.
            }
        }

        return CollectLocalAddresses(addressReaders);
    }

    internal static IReadOnlyList<IPAddress> CollectLocalAddresses(
        IEnumerable<Func<IReadOnlyList<IPAddress>>> addressReaders)
    {
        var addresses = new List<IPAddress>();
        foreach (var readAddresses in addressReaders)
        {
            try
            {
                addresses.AddRange(readAddresses());
            }
            catch (NetworkInformationException)
            {
                // Retain healthy interfaces when one adapter disappears or becomes unreadable.
            }
            catch (SocketException)
            {
                // A failed adapter must not suppress discovery on unrelated healthy adapters.
            }
        }

        return addresses
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork
                && !IPAddress.IsLoopback(address)
                && !address.Equals(IPAddress.Any))
            .Distinct()
            .OrderBy(address => address.ToString(), StringComparer.Ordinal)
            .Take(MaximumInterfaces)
            .ToArray();
    }

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
