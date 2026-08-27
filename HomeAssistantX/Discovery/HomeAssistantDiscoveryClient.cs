using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using HomeAssistantX.Exceptions;

namespace HomeAssistantX.Discovery;

/// <summary>Discovers Home Assistant instances advertised as <c>_home-assistant._tcp.local</c>.</summary>
public sealed class HomeAssistantDiscoveryClient
{
    private readonly IHomeAssistantDiscoveryTransportFactory _transportFactory;

    public HomeAssistantDiscoveryClient()
        : this(new UdpHomeAssistantDiscoveryTransportFactory())
    {
    }

    internal HomeAssistantDiscoveryClient(IHomeAssistantDiscoveryTransportFactory transportFactory)
    {
        _transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
    }

    /// <summary>Performs a bounded IPv4 mDNS query. Results are untrusted discovery hints and are never connected automatically.</summary>
    public async Task<IReadOnlyList<HomeAssistantDiscoveredInstance>> DiscoverAsync(TimeSpan? timeout = null, CancellationToken cancellationToken = default)
    {
        var duration = timeout ?? TimeSpan.FromSeconds(3);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(timeout), "Discovery timeout must be between zero and one minute.");
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var localAddresses = _transportFactory.GetLocalAddresses()
                .Distinct()
                .OrderBy(address => address.ToString(), StringComparer.Ordinal)
                .ToArray();
            if (localAddresses.Length == 0)
            {
                return Array.Empty<HomeAssistantDiscoveredInstance>();
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(duration);
            var results = await Task.WhenAll(localAddresses.Select((address, index) =>
                DiscoverOnInterfaceAsync(
                    address,
                    DnsDiscoveryLimits.ForInterface(index, localAddresses.Length),
                    timeoutSource.Token))).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var errors = results.Where(result => result.Error is not null).Select(result => result.Error!).ToArray();
            if (errors.Length == localAddresses.Length)
            {
                throw new HomeAssistantConnectionException(
                    "Home Assistant mDNS discovery failed on every eligible network interface.",
                    new AggregateException(errors));
            }

            return results.SelectMany(result => result.Instances)
                .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ServiceInstanceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ServiceInstanceName, StringComparer.Ordinal)
                .ThenBy(value => value.Addresses.FirstOrDefault()?.ToString(), StringComparer.Ordinal)
                .ToArray();
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Array.Empty<HomeAssistantDiscoveredInstance>();
        }
        catch (SocketException ex)
        {
            throw new HomeAssistantConnectionException("Home Assistant mDNS discovery failed.", ex);
        }
        catch (NetworkInformationException ex)
        {
            throw new HomeAssistantConnectionException("Home Assistant network-interface discovery failed.", ex);
        }
    }

    private async Task<DiscoveryInterfaceResult> DiscoverOnInterfaceAsync(
        IPAddress localAddress,
        DnsDiscoveryLimits limits,
        CancellationToken cancellationToken)
    {
        IHomeAssistantDiscoveryTransport? transport = null;
        var aggregate = new DnsDiscoveryAggregate(limits);
        try
        {
            transport = _transportFactory.Create(localAddress);
            await transport.SendAsync(DnsDiscoveryPacket.CreateQuery(), cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                var packet = await transport.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                if (!aggregate.TryConsumeDatagram()) break;
                DnsDiscoveryPacket.ReadInto(packet, aggregate);
            }
            return new DiscoveryInterfaceResult(aggregate.Build(), null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new DiscoveryInterfaceResult(aggregate.Build(), null);
        }
        catch (Exception ex) when ((ex is ObjectDisposedException || ex is SocketException) && cancellationToken.IsCancellationRequested)
        {
            return new DiscoveryInterfaceResult(aggregate.Build(), null);
        }
        catch (Exception ex) when (ex is ObjectDisposedException || ex is SocketException)
        {
            return new DiscoveryInterfaceResult(aggregate.Build(), ex);
        }
        finally
        {
            transport?.Dispose();
        }
    }

    private sealed class DiscoveryInterfaceResult
    {
        internal DiscoveryInterfaceResult(IReadOnlyList<HomeAssistantDiscoveredInstance> instances, Exception? error)
        {
            Instances = instances;
            Error = error;
        }

        internal IReadOnlyList<HomeAssistantDiscoveredInstance> Instances { get; }

        internal Exception? Error { get; }
    }
}

internal sealed class DnsDiscoveryLimits
{
    internal static readonly DnsDiscoveryLimits Default = new(64, 128, 128, 128, 256);

    private DnsDiscoveryLimits(int instances, int services, int textOwners, int addressHosts, int datagrams)
    {
        Instances = instances;
        Services = services;
        TextOwners = textOwners;
        AddressHosts = addressHosts;
        Datagrams = datagrams;
    }

    internal int Instances { get; }
    internal int Services { get; }
    internal int TextOwners { get; }
    internal int AddressHosts { get; }
    internal int Datagrams { get; }

    internal static DnsDiscoveryLimits ForInterface(int index, int count)
    {
        if (index < 0 || count <= 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
        return new DnsDiscoveryLimits(
            Share(64, index, count),
            Share(128, index, count),
            Share(128, index, count),
            Share(128, index, count),
            Share(256, index, count));
    }

    private static int Share(int total, int index, int count)
        => total / count + (index < total % count ? 1 : 0);
}

internal sealed class DnsDiscoveryAggregate
{
    private const string ServiceName = "_home-assistant._tcp.local";
    private const int MaximumPropertiesPerOwner = 64;
    private const int MaximumAddressesPerHost = 16;
    private readonly DnsDiscoveryLimits _limits;
    private readonly object _gate = new();
    private readonly HashSet<string> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, (string Host, int Port)> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<string, string?>> _text = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, HashSet<IPAddress>> _addresses = new(StringComparer.OrdinalIgnoreCase);
    private int _datagrams;

    internal DnsDiscoveryAggregate(DnsDiscoveryLimits? limits = null)
    {
        _limits = limits ?? DnsDiscoveryLimits.Default;
    }

    internal int InstanceCount { get { lock (_gate) return _instances.Count; } }
    internal int ServiceCount { get { lock (_gate) return _services.Count; } }
    internal int TextOwnerCount { get { lock (_gate) return _text.Count; } }
    internal int AddressHostCount { get { lock (_gate) return _addresses.Count; } }
    internal int DatagramCount => Volatile.Read(ref _datagrams);

    internal bool TryConsumeDatagram()
    {
        while (true)
        {
            var current = Volatile.Read(ref _datagrams);
            if (current >= _limits.Datagrams) return false;
            if (Interlocked.CompareExchange(ref _datagrams, current + 1, current) == current) return true;
        }
    }

    internal void AddInstance(string instance)
    {
        lock (_gate)
        {
            if (_instances.Contains(instance) || _instances.Count < _limits.Instances) _instances.Add(instance);
        }
    }

    internal void AddService(string instance, string host, int port)
    {
        lock (_gate)
        {
            if (_services.ContainsKey(instance) || _services.Count < _limits.Services) _services[instance] = (host, port);
        }
    }

    internal void AddText(string instance, string key, string? value)
    {
        lock (_gate)
        {
            if (!_text.TryGetValue(instance, out var properties))
            {
                if (_text.Count >= _limits.TextOwners) return;
                _text[instance] = properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            }

            if (properties.ContainsKey(key) || properties.Count < MaximumPropertiesPerOwner) properties[key] = value;
        }
    }

    internal void AddAddress(string host, IPAddress address)
    {
        lock (_gate)
        {
            if (!_addresses.TryGetValue(host, out var addresses))
            {
                if (_addresses.Count >= _limits.AddressHosts) return;
                _addresses[host] = addresses = new HashSet<IPAddress>();
            }

            if (addresses.Contains(address) || addresses.Count < MaximumAddressesPerHost) addresses.Add(address);
        }
    }

    public IReadOnlyList<HomeAssistantDiscoveredInstance> Build()
    {
        lock (_gate)
        {
            return _instances.Select(instance =>
            {
                _services.TryGetValue(instance, out var service);
                _text.TryGetValue(instance, out var properties);
                properties ??= new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                var addressValues = !string.IsNullOrWhiteSpace(service.Host) && _addresses.TryGetValue(service.Host, out var found)
                    ? found.OrderBy(value => value.AddressFamily).ThenBy(value => value.ToString(), StringComparer.Ordinal).ToArray()
                    : Array.Empty<IPAddress>();
                return new HomeAssistantDiscoveredInstance
                {
                    ServiceInstanceName = instance,
                    Name = Value(properties, "location_name") ?? FriendlyInstanceName(instance),
                    InstanceId = Value(properties, "uuid"),
                    Version = Value(properties, "version"),
                    HostName = string.IsNullOrWhiteSpace(service.Host) ? null : service.Host,
                    Port = service.Port == 0 ? null : service.Port,
                    InternalUri = ReadHttpUri(Value(properties, "internal_url")),
                    ExternalUri = ReadHttpUri(Value(properties, "external_url")),
                    BaseUri = ReadHttpUri(Value(properties, "base_url")),
                    RequiresApiPassword = bool.TryParse(Value(properties, "requires_api_password"), out var required) && required,
                    Addresses = addressValues,
                    Properties = new Dictionary<string, string?>(properties, StringComparer.OrdinalIgnoreCase)
                };
            }).OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ServiceInstanceName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ServiceInstanceName, StringComparer.Ordinal)
                .ToArray();
        }
    }

    private static string FriendlyInstanceName(string instance)
    {
        var suffix = "." + ServiceName;
        var value = instance.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ? instance.Substring(0, instance.Length - suffix.Length) : instance;
        return string.IsNullOrWhiteSpace(value) ? instance : value;
    }

    private static string? Value(IReadOnlyDictionary<string, string?> properties, string name)
        => properties.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static Uri? ReadHttpUri(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? uri : null;
}

internal static class DnsDiscoveryPacket
{
    private const string ServiceName = "_home-assistant._tcp.local";

    internal static byte[] CreateQuery()
    {
        using var stream = new MemoryStream();
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 1);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteUInt16(stream, 0);
        WriteName(stream, ServiceName);
        WriteUInt16(stream, 12);
        WriteUInt16(stream, 0x8001);
        return stream.ToArray();
    }

    internal static void ReadInto(byte[] packet, DnsDiscoveryAggregate aggregate)
    {
        if (packet is null || packet.Length < 12) return;
        var offset = 4;
        var questionCount = ReadUInt16(packet, ref offset);
        var answerCount = ReadUInt16(packet, ref offset);
        var authorityCount = ReadUInt16(packet, ref offset);
        var additionalCount = ReadUInt16(packet, ref offset);
        try
        {
            for (var index = 0; index < questionCount; index++)
            {
                ReadName(packet, ref offset);
                Require(packet, offset, 4);
                offset += 4;
            }

            var recordCount = checked(answerCount + authorityCount + additionalCount);
            for (var index = 0; index < recordCount; index++)
            {
                var name = ReadName(packet, ref offset);
                var type = ReadUInt16(packet, ref offset);
                ReadUInt16(packet, ref offset);
                Require(packet, offset, 4);
                offset += 4;
                var length = ReadUInt16(packet, ref offset);
                Require(packet, offset, length);
                var dataOffset = offset;
                var end = offset + length;
                switch (type)
                {
                    case 12:
                    {
                        var instance = ReadName(packet, ref dataOffset);
                        if (dataOffset > end) throw new InvalidDataException("Invalid PTR record length.");
                        if (string.Equals(name, ServiceName, StringComparison.OrdinalIgnoreCase)) aggregate.AddInstance(instance);
                        break;
                    }
                    case 33 when length >= 6:
                    {
                        dataOffset += 4;
                        var port = ReadUInt16(packet, ref dataOffset);
                        var host = ReadName(packet, ref dataOffset);
                        if (dataOffset > end) throw new InvalidDataException("Invalid SRV record length.");
                        aggregate.AddService(name, host, port);
                        break;
                    }
                    case 16:
                    {
                        while (dataOffset < end)
                        {
                            var textLength = packet[dataOffset++];
                            if (dataOffset > end - textLength) throw new InvalidDataException("Invalid TXT record length.");
                            var item = Encoding.UTF8.GetString(packet, dataOffset, textLength);
                            dataOffset += textLength;
                            var separator = item.IndexOf('=');
                            aggregate.AddText(name, separator < 0 ? item : item.Substring(0, separator), separator < 0 ? null : item.Substring(separator + 1));
                        }
                        break;
                    }
                    case 1 when length == 4:
                        aggregate.AddAddress(name, new IPAddress(new[] { packet[offset], packet[offset + 1], packet[offset + 2], packet[offset + 3] }));
                        break;
                    case 28 when length == 16:
                    {
                        var bytes = new byte[16];
                        Buffer.BlockCopy(packet, offset, bytes, 0, 16);
                        aggregate.AddAddress(name, new IPAddress(bytes));
                        break;
                    }
                }
                offset = end;
            }
        }
        catch (InvalidDataException)
        {
            // mDNS is unauthenticated local-network input; malformed datagrams are ignored.
        }
        catch (OverflowException)
        {
        }
    }

    private static string ReadName(byte[] packet, ref int offset)
    {
        var labels = new List<string>();
        var position = offset;
        var next = -1;
        var hops = 0;
        var expandedLength = 0;
        while (true)
        {
            Require(packet, position, 1);
            var length = packet[position++];
            if (length == 0) break;
            if ((length & 0xC0) == 0xC0)
            {
                Require(packet, position, 1);
                var pointer = ((length & 0x3F) << 8) | packet[position++];
                if (pointer >= packet.Length || ++hops > 32) throw new InvalidDataException("Invalid DNS compression pointer.");
                if (next < 0) next = position;
                position = pointer;
                continue;
            }
            if ((length & 0xC0) != 0 || length > 63) throw new InvalidDataException("Invalid DNS label.");
            Require(packet, position, length);
            expandedLength = checked(expandedLength + length + (labels.Count == 0 ? 0 : 1));
            if (expandedLength > 255 || labels.Count >= 127) throw new InvalidDataException("DNS name exceeds the protocol limit.");
            labels.Add(Encoding.UTF8.GetString(packet, position, length));
            position += length;
        }
        offset = next < 0 ? position : next;
        return string.Join(".", labels);
    }

    private static ushort ReadUInt16(byte[] packet, ref int offset)
    {
        Require(packet, offset, 2);
        var value = (ushort)((packet[offset] << 8) | packet[offset + 1]);
        offset += 2;
        return value;
    }

    private static void Require(byte[] packet, int offset, int count)
    {
        if (offset < 0 || count < 0 || offset > packet.Length - count) throw new InvalidDataException("Truncated DNS packet.");
    }

    private static void WriteUInt16(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteName(Stream stream, string name)
    {
        foreach (var label in name.Split('.'))
        {
            var bytes = Encoding.UTF8.GetBytes(label);
            stream.WriteByte((byte)bytes.Length);
            stream.Write(bytes, 0, bytes.Length);
        }
        stream.WriteByte(0);
    }
}
