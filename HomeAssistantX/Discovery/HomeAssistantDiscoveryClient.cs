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
    private readonly Func<TimeSpan> _clock;
    private readonly object _gate = new();
    private readonly Dictionary<string, CachedPtr> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<CachedService>> _services = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<CachedText>> _text = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Dictionary<IPAddress, CachedAddress>> _addresses = new(StringComparer.OrdinalIgnoreCase);
    private int _datagrams;

    internal DnsDiscoveryAggregate(DnsDiscoveryLimits? limits = null, Func<TimeSpan>? clock = null)
    {
        _limits = limits ?? DnsDiscoveryLimits.Default;
        _clock = clock ?? MonotonicNow;
    }

    internal int InstanceCount { get { lock (_gate) { Prune(_clock()); return _instances.Count; } } }
    internal int ServiceCount { get { lock (_gate) { Prune(_clock()); return _services.Count; } } }
    internal int TextOwnerCount { get { lock (_gate) { Prune(_clock()); return _text.Count; } } }
    internal int AddressHostCount { get { lock (_gate) { Prune(_clock()); return _addresses.Count; } } }
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
            var now = _clock();
            Prune(now);
            if (_instances.ContainsKey(instance) || _instances.Count < _limits.Instances)
                _instances[instance] = new CachedPtr(instance, now, Expiry(now, 120));
        }
    }

    internal void AddService(string instance, string host, int port)
    {
        lock (_gate)
        {
            var now = _clock();
            Prune(now);
            if (!_services.TryGetValue(instance, out var services))
            {
                if (_services.Count >= _limits.Services) return;
                _services[instance] = services = new List<CachedService>();
            }
            services.RemoveAll(value => string.Equals(value.DataKey, ServiceKey(host, port), StringComparison.Ordinal));
            services.Add(new CachedService(host, port, ServiceKey(host, port), now, Expiry(now, 120)));
        }
    }

    internal void AddText(string instance, string key, string? value)
    {
        lock (_gate)
        {
            var now = _clock();
            Prune(now);
            if (!_text.TryGetValue(instance, out var records))
            {
                if (_text.Count >= _limits.TextOwners) return;
                _text[instance] = records = new List<CachedText>();
            }
            var cached = records.OrderByDescending(value => value.ReceivedAt).FirstOrDefault();
            if (cached is null)
            {
                cached = new CachedText(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), string.Empty, now, Expiry(now, 120));
                records.Add(cached);
            }

            if (cached.Properties.ContainsKey(key) || cached.Properties.Count < MaximumPropertiesPerOwner)
                cached.Properties[key] = value;
        }
    }

    internal void AddAddress(string host, IPAddress address)
    {
        lock (_gate)
        {
            var now = _clock();
            Prune(now);
            if (!_addresses.TryGetValue(host, out var addresses))
            {
                if (_addresses.Count >= _limits.AddressHosts) return;
                _addresses[host] = addresses = new Dictionary<IPAddress, CachedAddress>();
            }

            if (addresses.ContainsKey(address) || addresses.Count < MaximumAddressesPerHost)
                addresses[address] = new CachedAddress(now, Expiry(now, 120));
        }
    }

    internal void ApplyPacket(IReadOnlyList<DnsDiscoveryUpdate> updates)
    {
        lock (_gate)
        {
            var now = _clock();
            Prune(now);
            var acceptedInstances = new HashSet<string>(_instances.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var update in updates.Where(value => value.Kind == DnsDiscoveryRecordKind.Ptr && IsHomeAssistantPtr(value)))
            {
                acceptedInstances.Add(update.Target!);
                ApplyPtr(update, now);
            }

            acceptedInstances.IntersectWith(_instances.Keys);
            foreach (var update in updates.Where(value => value.Kind == DnsDiscoveryRecordKind.Srv && acceptedInstances.Contains(value.Name))) ApplyService(update, now, updates);
            foreach (var update in updates.Where(value => value.Kind == DnsDiscoveryRecordKind.Txt && acceptedInstances.Contains(value.Name))) ApplyText(update, now, updates);

            var acceptedHosts = new HashSet<string>(
                _services.Where(value => acceptedInstances.Contains(value.Key)).SelectMany(value => value.Value).Select(value => value.Host),
                StringComparer.OrdinalIgnoreCase);
            foreach (var update in updates.Where(value => (value.Kind == DnsDiscoveryRecordKind.A || value.Kind == DnsDiscoveryRecordKind.Aaaa) && acceptedHosts.Contains(value.Name)))
                ApplyAddress(update, now, updates);
        }
    }

    public IReadOnlyList<HomeAssistantDiscoveredInstance> Build()
    {
        lock (_gate)
        {
            Prune(_clock());
            return _instances.Keys.Select(instance =>
            {
                _services.TryGetValue(instance, out var serviceRecords);
                var service = serviceRecords?.OrderByDescending(value => value.ReceivedAt).FirstOrDefault();
                _text.TryGetValue(instance, out var textRecords);
                var cachedText = textRecords?.OrderByDescending(value => value.ReceivedAt).FirstOrDefault();
                var properties = cachedText?.Properties ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                var addressValues = service is not null && !string.IsNullOrWhiteSpace(service.Host) && _addresses.TryGetValue(service.Host, out var found)
                    ? found.Keys.OrderBy(value => value.AddressFamily).ThenBy(value => value.ToString(), StringComparer.Ordinal).ToArray()
                    : Array.Empty<IPAddress>();
                return new HomeAssistantDiscoveredInstance
                {
                    ServiceInstanceName = instance,
                    Name = Value(properties, "location_name") ?? FriendlyInstanceName(instance),
                    InstanceId = Value(properties, "uuid"),
                    Version = Value(properties, "version"),
                    HostName = service is null || string.IsNullOrWhiteSpace(service.Host) ? null : service.Host,
                    Port = service is null || service.Port == 0 ? null : service.Port,
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

    private void ApplyPtr(DnsDiscoveryUpdate update, TimeSpan now)
    {
        var instance = update.Target!;
        if (update.Ttl == 0)
        {
            if (_instances.TryGetValue(instance, out var existing))
                existing.ExpiresAt = Earlier(existing.ExpiresAt, now + TimeSpan.FromSeconds(1));
            return;
        }
        if (_instances.ContainsKey(instance) || _instances.Count < _limits.Instances)
            _instances[instance] = new CachedPtr(update.DataKey, now, Expiry(now, update.Ttl));
    }

    private void ApplyService(DnsDiscoveryUpdate update, TimeSpan now, IReadOnlyList<DnsDiscoveryUpdate> packet)
    {
        if (!_services.TryGetValue(update.Name, out var records))
        {
            if (update.Ttl == 0 || _services.Count >= _limits.Services) return;
            _services[update.Name] = records = new List<CachedService>();
        }
        if (update.Ttl == 0)
        {
            foreach (var existing in records.Where(value => string.Equals(value.DataKey, update.DataKey, StringComparison.Ordinal)))
                existing.ExpiresAt = Earlier(existing.ExpiresAt, now + TimeSpan.FromSeconds(1));
            return;
        }
        if (update.CacheFlush)
        {
            var announced = new HashSet<string>(packet.Where(value => value.Kind == DnsDiscoveryRecordKind.Srv && value.Ttl > 0 && string.Equals(value.Name, update.Name, StringComparison.OrdinalIgnoreCase)).Select(value => value.DataKey), StringComparer.Ordinal);
            foreach (var old in records.Where(value => !announced.Contains(value.DataKey) && now - value.ReceivedAt >= TimeSpan.FromSeconds(1)))
                old.ExpiresAt = Earlier(old.ExpiresAt, now + TimeSpan.FromSeconds(1));
        }
        records.RemoveAll(value => string.Equals(value.DataKey, update.DataKey, StringComparison.Ordinal));
        if (records.Count < 8) records.Add(new CachedService(update.Host!, update.Port, update.DataKey, now, Expiry(now, update.Ttl)));
    }

    private void ApplyText(DnsDiscoveryUpdate update, TimeSpan now, IReadOnlyList<DnsDiscoveryUpdate> packet)
    {
        if (!_text.TryGetValue(update.Name, out var records))
        {
            if (update.Ttl == 0 || _text.Count >= _limits.TextOwners) return;
            _text[update.Name] = records = new List<CachedText>();
        }
        if (update.Ttl == 0)
        {
            foreach (var existing in records.Where(value => string.Equals(value.DataKey, update.DataKey, StringComparison.Ordinal)))
                existing.ExpiresAt = Earlier(existing.ExpiresAt, now + TimeSpan.FromSeconds(1));
            return;
        }
        if (update.CacheFlush)
        {
            var announced = new HashSet<string>(packet.Where(value => value.Kind == DnsDiscoveryRecordKind.Txt && value.Ttl > 0 && string.Equals(value.Name, update.Name, StringComparison.OrdinalIgnoreCase)).Select(value => value.DataKey), StringComparer.Ordinal);
            foreach (var old in records.Where(value => !announced.Contains(value.DataKey) && now - value.ReceivedAt >= TimeSpan.FromSeconds(1)))
                old.ExpiresAt = Earlier(old.ExpiresAt, now + TimeSpan.FromSeconds(1));
        }
        records.RemoveAll(value => string.Equals(value.DataKey, update.DataKey, StringComparison.Ordinal));
        if (records.Count < 8) records.Add(new CachedText(update.Properties!, update.DataKey, now, Expiry(now, update.Ttl)));
    }

    private void ApplyAddress(DnsDiscoveryUpdate update, TimeSpan now, IReadOnlyList<DnsDiscoveryUpdate> packet)
    {
        if (!_addresses.TryGetValue(update.Name, out var addresses))
        {
            if (update.Ttl == 0 || _addresses.Count >= _limits.AddressHosts) return;
            _addresses[update.Name] = addresses = new Dictionary<IPAddress, CachedAddress>();
        }
        var address = update.Address!;
        if (update.Ttl == 0)
        {
            if (addresses.TryGetValue(address, out var existing))
                existing.ExpiresAt = Earlier(existing.ExpiresAt, now + TimeSpan.FromSeconds(1));
            return;
        }
        if (update.CacheFlush)
        {
            var announced = new HashSet<IPAddress>(packet.Where(value => value.Kind == update.Kind && value.Ttl > 0 && string.Equals(value.Name, update.Name, StringComparison.OrdinalIgnoreCase)).Select(value => value.Address!));
            foreach (var pair in addresses.Where(pair => pair.Key.AddressFamily == address.AddressFamily && !announced.Contains(pair.Key) && now - pair.Value.ReceivedAt >= TimeSpan.FromSeconds(1)).ToArray())
                pair.Value.ExpiresAt = Earlier(pair.Value.ExpiresAt, now + TimeSpan.FromSeconds(1));
        }
        if (addresses.ContainsKey(address) || addresses.Count < MaximumAddressesPerHost)
            addresses[address] = new CachedAddress(now, Expiry(now, update.Ttl));
    }

    private void Prune(TimeSpan now)
    {
        foreach (var key in _instances.Where(value => value.Value.ExpiresAt <= now).Select(value => value.Key).ToArray()) _instances.Remove(key);
        foreach (var owner in _services.Keys.ToArray()) { _services[owner].RemoveAll(value => value.ExpiresAt <= now); if (_services[owner].Count == 0) _services.Remove(owner); }
        foreach (var owner in _text.Keys.ToArray()) { _text[owner].RemoveAll(value => value.ExpiresAt <= now); if (_text[owner].Count == 0) _text.Remove(owner); }
        foreach (var owner in _addresses.Keys.ToArray())
        {
            var values = _addresses[owner];
            foreach (var address in values.Where(value => value.Value.ExpiresAt <= now).Select(value => value.Key).ToArray()) values.Remove(address);
            if (values.Count == 0) _addresses.Remove(owner);
        }
    }

    private static bool IsHomeAssistantPtr(DnsDiscoveryUpdate update)
        => string.Equals(update.Name, ServiceName, StringComparison.OrdinalIgnoreCase)
            && update.Target is not null
            && update.Target.Length > ServiceName.Length + 1
            && update.Target.EndsWith("." + ServiceName, StringComparison.OrdinalIgnoreCase);
    private static string ServiceKey(string host, int port) => host.ToUpperInvariant() + "\0" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static TimeSpan Expiry(TimeSpan now, uint ttl) => now + TimeSpan.FromSeconds(ttl == 0 ? 1 : ttl);
    private static TimeSpan Earlier(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static TimeSpan MonotonicNow() => TimeSpan.FromSeconds((double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency);

    private abstract class CachedRecord
    {
        protected CachedRecord(string dataKey, TimeSpan receivedAt, TimeSpan expiresAt) { DataKey = dataKey; ReceivedAt = receivedAt; ExpiresAt = expiresAt; }
        internal string DataKey { get; }
        internal TimeSpan ReceivedAt { get; }
        internal TimeSpan ExpiresAt { get; set; }
    }
    private sealed class CachedPtr : CachedRecord { internal CachedPtr(string key, TimeSpan receivedAt, TimeSpan expiresAt) : base(key, receivedAt, expiresAt) { } }
    private sealed class CachedService : CachedRecord { internal CachedService(string host, int port, string key, TimeSpan receivedAt, TimeSpan expiresAt) : base(key, receivedAt, expiresAt) { Host = host; Port = port; } internal string Host { get; } internal int Port { get; } }
    private sealed class CachedText : CachedRecord { internal CachedText(Dictionary<string, string?> properties, string key, TimeSpan receivedAt, TimeSpan expiresAt) : base(key, receivedAt, expiresAt) { Properties = properties; } internal Dictionary<string, string?> Properties { get; } }
    private sealed class CachedAddress { internal CachedAddress(TimeSpan receivedAt, TimeSpan expiresAt) { ReceivedAt = receivedAt; ExpiresAt = expiresAt; } internal TimeSpan ReceivedAt { get; } internal TimeSpan ExpiresAt { get; set; } }

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

internal enum DnsDiscoveryRecordKind { Ptr, Srv, Txt, A, Aaaa }

internal sealed class DnsDiscoveryUpdate
{
    internal DnsDiscoveryRecordKind Kind { get; set; }
    internal string Name { get; set; } = string.Empty;
    internal string DataKey { get; set; } = string.Empty;
    internal uint Ttl { get; set; }
    internal bool CacheFlush { get; set; }
    internal string? Target { get; set; }
    internal string? Host { get; set; }
    internal int Port { get; set; }
    internal Dictionary<string, string?>? Properties { get; set; }
    internal IPAddress? Address { get; set; }
}

internal static class DnsDiscoveryPacket
{
    private const string ServiceName = "_home-assistant._tcp.local";
    private const int MaximumTextProperties = 64;
    private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);

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
        try
        {
            if (packet is null || packet.Length < 12) return;
            var offset = 0;
            ReadUInt16(packet, ref offset);
            var flags = ReadUInt16(packet, ref offset);
            if ((flags & 0x8000) == 0 || (flags & 0x7800) != 0 || (flags & 0x000F) != 0) return;
            var questionCount = ReadUInt16(packet, ref offset);
            var answerCount = ReadUInt16(packet, ref offset);
            var authorityCount = ReadUInt16(packet, ref offset);
            var additionalCount = ReadUInt16(packet, ref offset);
            for (var index = 0; index < questionCount; index++)
            {
                ReadName(packet, ref offset);
                Require(packet, offset, 4);
                offset += 4;
            }

            var recordCount = checked(answerCount + authorityCount + additionalCount);
            var updates = new List<DnsDiscoveryUpdate>();
            for (var index = 0; index < recordCount; index++)
            {
                var name = ReadName(packet, ref offset);
                var type = ReadUInt16(packet, ref offset);
                var recordClass = ReadUInt16(packet, ref offset);
                var ttl = ReadUInt32(packet, ref offset);
                var length = ReadUInt16(packet, ref offset);
                Require(packet, offset, length);
                var dataOffset = offset;
                var end = offset + length;
                if ((recordClass & 0x7FFF) != 1)
                {
                    offset = end;
                    continue;
                }
                var cacheFlush = (recordClass & 0x8000) != 0;
                switch (type)
                {
                    case 12:
                    {
                        var instance = ReadName(packet, ref dataOffset);
                        if (dataOffset != end) throw new InvalidDataException("Invalid PTR record length.");
                        updates.Add(new DnsDiscoveryUpdate { Kind = DnsDiscoveryRecordKind.Ptr, Name = name, Target = instance, DataKey = instance.ToUpperInvariant(), Ttl = ttl, CacheFlush = cacheFlush });
                        break;
                    }
                    case 33:
                    {
                        if (length < 6) throw new InvalidDataException("Invalid SRV record length.");
                        var priority = ReadUInt16(packet, ref dataOffset);
                        var weight = ReadUInt16(packet, ref dataOffset);
                        var port = ReadUInt16(packet, ref dataOffset);
                        var host = ReadName(packet, ref dataOffset);
                        if (dataOffset != end) throw new InvalidDataException("Invalid SRV record length.");
                        updates.Add(new DnsDiscoveryUpdate { Kind = DnsDiscoveryRecordKind.Srv, Name = name, Host = host, Port = port, DataKey = priority.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0" + weight.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0" + host.ToUpperInvariant() + "\0" + port.ToString(System.Globalization.CultureInfo.InvariantCulture), Ttl = ttl, CacheFlush = cacheFlush });
                        break;
                    }
                    case 16:
                    {
                        var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                        while (dataOffset < end)
                        {
                            var textLength = packet[dataOffset++];
                            if (dataOffset > end - textLength) throw new InvalidDataException("Invalid TXT record length.");
                            var itemOffset = dataOffset;
                            dataOffset += textLength;
                            var separator = -1;
                            for (var position = 0; position < textLength; position++) if (packet[itemOffset + position] == (byte)'=') { separator = position; break; }
                            var keyLength = separator < 0 ? textLength : separator;
                            if (!IsValidTextKey(packet, itemOffset, keyLength)) continue;
                            var key = Encoding.ASCII.GetString(packet, itemOffset, keyLength);
                            if (properties.ContainsKey(key) || properties.Count >= MaximumTextProperties) continue;
                            try
                            {
                                properties[key] = separator < 0 ? null : StrictUtf8.GetString(packet, itemOffset + separator + 1, textLength - separator - 1);
                            }
                            catch (DecoderFallbackException)
                            {
                            }
                        }
                        updates.Add(new DnsDiscoveryUpdate { Kind = DnsDiscoveryRecordKind.Txt, Name = name, Properties = properties, DataKey = Convert.ToBase64String(packet, offset, length), Ttl = ttl, CacheFlush = cacheFlush });
                        break;
                    }
                    case 1:
                    {
                        if (length != 4) throw new InvalidDataException("Invalid A record length.");
                        var address = new IPAddress(new[] { packet[offset], packet[offset + 1], packet[offset + 2], packet[offset + 3] });
                        updates.Add(new DnsDiscoveryUpdate { Kind = DnsDiscoveryRecordKind.A, Name = name, Address = address, DataKey = address.ToString(), Ttl = ttl, CacheFlush = cacheFlush });
                        break;
                    }
                    case 28:
                    {
                        if (length != 16) throw new InvalidDataException("Invalid AAAA record length.");
                        var bytes = new byte[16];
                        Buffer.BlockCopy(packet, offset, bytes, 0, 16);
                        var address = new IPAddress(bytes);
                        updates.Add(new DnsDiscoveryUpdate { Kind = DnsDiscoveryRecordKind.Aaaa, Name = name, Address = address, DataKey = address.ToString(), Ttl = ttl, CacheFlush = cacheFlush });
                        break;
                    }
                }
                offset = end;
            }
            if (offset != packet.Length) throw new InvalidDataException("Unexpected trailing DNS packet data.");
            aggregate.ApplyPacket(updates);
        }
        catch (InvalidDataException)
        {
            // mDNS is unauthenticated local-network input; malformed datagrams are ignored.
        }
        catch (OverflowException)
        {
        }
    }

    private static bool IsValidTextKey(byte[] packet, int offset, int length)
    {
        if (length <= 0) return false;
        for (var index = 0; index < length; index++)
        {
            var value = packet[offset + index];
            if (value < 0x20 || value > 0x7E || value == (byte)'=') return false;
        }
        return true;
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

    private static uint ReadUInt32(byte[] packet, ref int offset)
    {
        Require(packet, offset, 4);
        var value = ((uint)packet[offset] << 24)
            | ((uint)packet[offset + 1] << 16)
            | ((uint)packet[offset + 2] << 8)
            | packet[offset + 3];
        offset += 4;
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
