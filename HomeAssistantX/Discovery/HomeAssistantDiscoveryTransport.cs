using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace HomeAssistantX.Discovery;

internal interface IHomeAssistantDiscoveryTransportFactory
{
    IReadOnlyList<HomeAssistantDiscoveryInterface> GetLocalInterfaces();

    IHomeAssistantDiscoveryTransport Create(HomeAssistantDiscoveryInterface network);
}

internal sealed class HomeAssistantDiscoveryInterface
{
    internal HomeAssistantDiscoveryInterface(string id, IReadOnlyList<IPAddress> addresses, int interfaceIndex = 0)
    {
        Id = id;
        Addresses = addresses;
        InterfaceIndex = interfaceIndex;
    }

    internal string Id { get; }
    internal IReadOnlyList<IPAddress> Addresses { get; }
    internal int InterfaceIndex { get; }
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

                readers.Add(() =>
                {
                    var properties = network.GetIPProperties();
                    return new HomeAssistantDiscoveryInterface(
                        network.Id,
                        properties.UnicastAddresses.Select(address => address.Address).ToArray(),
                        properties.GetIPv4Properties().Index);
                });
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
                if (addresses.Count > 0) interfaces.Add(new HomeAssistantDiscoveryInterface(network.Id, addresses, network.InterfaceIndex));
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
            .Select(value => new HomeAssistantDiscoveryInterface(value.Id, selected[value.Id].ToArray(), value.InterfaceIndex))
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

    public IHomeAssistantDiscoveryTransport Create(HomeAssistantDiscoveryInterface network)
    {
        if (network is null) throw new ArgumentNullException(nameof(network));
        return new UdpHomeAssistantDiscoveryTransport(
            network.Addresses,
            network.InterfaceIndex,
            MulticastAddress,
            MulticastPort);
    }
}

internal sealed class UdpHomeAssistantDiscoveryTransport : IHomeAssistantDiscoveryTransport
{
    private const int MaximumDatagramBytes = 65535;
    private readonly UdpClient?[] _queryClients;
    private readonly UdpClient? _multicastClient;
    private readonly IPEndPoint _endpoint;
    private readonly int _interfaceIndex;
    private readonly SemaphoreSlim _receiveGate = new(1, 1);
    private readonly Task<byte[]>?[] _queryReceiveTasks;
    private Task<byte[]>? _multicastReceiveTask;
    private bool _multicastAvailable;
    private int _disposed;

    internal UdpHomeAssistantDiscoveryTransport(
        IReadOnlyList<IPAddress> localAddresses,
        int interfaceIndex,
        IPAddress multicastAddress,
        int multicastPort,
        Func<UdpClient>? clientFactory = null)
    {
        if (localAddresses is null) throw new ArgumentNullException(nameof(localAddresses));
        if (localAddresses.Count == 0) throw new ArgumentException("At least one IPv4 local address is required.", nameof(localAddresses));
        if (interfaceIndex <= 0) throw new ArgumentOutOfRangeException(nameof(interfaceIndex));
        var createClient = clientFactory ?? (() => new UdpClient(AddressFamily.InterNetwork));
        var queryClients = new List<UdpClient>();
        var queryAddresses = new List<IPAddress>();
        UdpClient? multicastClient = null;
        try
        {
            foreach (var localAddress in localAddresses)
            {
                UdpClient? queryClient = null;
                try
                {
                    queryClient = createClient();
                    queryClient.Client.Bind(new IPEndPoint(localAddress, 0));
                    ConfigureOutboundInterface(queryClient.Client, localAddress);
                    ConfigureMulticastTimeToLive(queryClient.Client);
                    queryClients.Add(queryClient);
                    queryAddresses.Add(localAddress);
                }
                catch (SocketException)
                {
                    queryClient?.Dispose();
                }
            }

            if (queryClients.Count == 0)
                throw new SocketException((int)SocketError.AddressNotAvailable);

            try
            {
                multicastClient = createClient();
                multicastClient.Client.ExclusiveAddressUse = false;
                multicastClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                multicastClient.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.PacketInformation, true);
                multicastClient.Client.Bind(CreateMulticastListenerEndpoint(queryAddresses[0], multicastPort));
                multicastClient.JoinMulticastGroup(multicastAddress, queryAddresses[0]);
            }
            catch (Exception ex) when (IsOptionalMulticastListenerFailure(ex))
            {
                multicastClient?.Dispose();
                multicastClient = null;
            }
            _queryClients = queryClients.Cast<UdpClient?>().ToArray();
            _queryReceiveTasks = new Task<byte[]>?[_queryClients.Length];
            _multicastClient = multicastClient;
            _multicastAvailable = multicastClient is not null;
            _endpoint = new IPEndPoint(multicastAddress, multicastPort);
            _interfaceIndex = interfaceIndex;
        }
        catch
        {
            foreach (var queryClient in queryClients) queryClient.Dispose();
            multicastClient?.Dispose();
            throw;
        }
    }

    internal static IPEndPoint CreateMulticastListenerEndpoint(IPAddress localAddress, int multicastPort)
    {
        if (localAddress is null) throw new ArgumentNullException(nameof(localAddress));
        if (localAddress.AddressFamily != AddressFamily.InterNetwork)
            throw new ArgumentException("An IPv4 local address is required.", nameof(localAddress));
        if (multicastPort <= IPEndPoint.MinPort || multicastPort > IPEndPoint.MaxPort)
            throw new ArgumentOutOfRangeException(nameof(multicastPort));
        return new IPEndPoint(IPAddress.Any, multicastPort);
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

    internal static bool IsExpectedInterface(int expectedInterfaceIndex, int receivedInterfaceIndex)
        => expectedInterfaceIndex > 0 && receivedInterfaceIndex == expectedInterfaceIndex;

    internal bool IsMulticastAvailable => _multicastAvailable;

    private static bool IsOptionalMulticastListenerFailure(Exception exception)
        => exception is SocketException
            || exception is ObjectDisposedException
            || exception is PlatformNotSupportedException
            || exception is NotSupportedException
            || exception is InvalidOperationException;

    public async Task SendAsync(byte[] query, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        using var registration = cancellationToken.Register(state => ((UdpHomeAssistantDiscoveryTransport)state!).Dispose(), this);
        try
        {
            SocketException? failure = null;
            var sent = false;
            foreach (var queryClient in _queryClients)
            {
                if (queryClient is null) continue;
                try
                {
                    _ = await queryClient.SendAsync(query, query.Length, _endpoint).ConfigureAwait(false);
                    sent = true;
                }
                catch (SocketException ex)
                {
                    failure ??= ex;
                }
            }

            if (!sent) throw failure ?? new SocketException((int)SocketError.NetworkDown);
        }
        catch (Exception ex) when ((ex is ObjectDisposedException || ex is SocketException) && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The mDNS send was canceled.", ex, cancellationToken);
        }
    }

    public async Task<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await _receiveGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        using var registration = cancellationToken.Register(state => ((UdpHomeAssistantDiscoveryTransport)state!).Dispose(), this);
        try
        {
            ThrowIfDisposed();
            while (true)
            {
                for (var index = 0; index < _queryClients.Length; index++)
                {
                    if (_queryClients[index] is not null && _queryReceiveTasks[index] is null)
                    {
                        _queryReceiveTasks[index] = ReceiveQueryAsync(_queryClients[index]!);
                    }
                }
                if (_multicastAvailable && _multicastClient is not null) _multicastReceiveTask ??= ReceiveMulticastAsync();
                var active = _queryReceiveTasks.Where(task => task is not null).Cast<Task<byte[]>>().ToList();
                if (_multicastReceiveTask is not null) active.Add(_multicastReceiveTask);
                if (active.Count == 0) throw new SocketException((int)SocketError.NetworkDown);
                var completed = await Task.WhenAny(active).ConfigureAwait(false);
                var queryIndex = Array.FindIndex(_queryReceiveTasks, task => ReferenceEquals(task, completed));
                if (queryIndex >= 0)
                {
                    _queryReceiveTasks[queryIndex] = null;
                }
                else
                {
                    _multicastReceiveTask = null;
                }

                try
                {
                    return await completed.ConfigureAwait(false);
                }
                catch (SocketException) when (!cancellationToken.IsCancellationRequested && queryIndex >= 0)
                {
                    _queryClients[queryIndex]!.Dispose();
                    _queryClients[queryIndex] = null;
                }
                catch (SocketException) when (!cancellationToken.IsCancellationRequested)
                {
                    _multicastAvailable = false;
                    _multicastClient?.Dispose();
                }
            }
        }
        catch (Exception ex) when ((ex is ObjectDisposedException || ex is SocketException) && cancellationToken.IsCancellationRequested)
        {
            throw new OperationCanceledException("The mDNS receive was canceled.", ex, cancellationToken);
        }
        finally
        {
            _receiveGate.Release();
        }
    }

    private static async Task<byte[]> ReceiveQueryAsync(UdpClient queryClient)
        => (await queryClient.ReceiveAsync().ConfigureAwait(false)).Buffer;

    private async Task<byte[]> ReceiveMulticastAsync()
    {
        var multicastClient = _multicastClient ?? throw new SocketException((int)SocketError.NetworkDown);
        var buffer = new byte[MaximumDatagramBytes];
        while (true)
        {
            EndPoint remote = new IPEndPoint(IPAddress.Any, 0);
            var result = await multicastClient.Client.ReceiveMessageFromAsync(
                new ArraySegment<byte>(buffer),
                SocketFlags.None,
                remote).ConfigureAwait(false);
            if (!IsExpectedInterface(_interfaceIndex, result.PacketInformation.Interface))
            {
                continue;
            }

            var packet = new byte[result.ReceivedBytes];
            Buffer.BlockCopy(buffer, 0, packet, 0, result.ReceivedBytes);
            return packet;
        }
    }

    private void ThrowIfDisposed()
    {
        if (Volatile.Read(ref _disposed) != 0)
            throw new ObjectDisposedException(nameof(UdpHomeAssistantDiscoveryTransport));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            foreach (var queryClient in _queryClients) queryClient?.Dispose();
            _multicastClient?.Dispose();
            foreach (var queryTask in _queryReceiveTasks) ObserveFailure(queryTask);
            ObserveFailure(_multicastReceiveTask);
        }
    }

    private static void ObserveFailure(Task? task)
    {
        if (task is null) return;
        _ = task.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }
}
