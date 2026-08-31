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
        cancellationToken.ThrowIfCancellationRequested();
        var duration = timeout ?? TimeSpan.FromSeconds(3);
        if (duration <= TimeSpan.Zero || duration > TimeSpan.FromMinutes(1)) throw new ArgumentOutOfRangeException(nameof(timeout), "Discovery timeout must be between zero and one minute.");

        try
        {
            var interfaces = _transportFactory.GetLocalInterfaces()
                .Where(value => value.Addresses.Count > 0)
                .OrderBy(value => value.Id, StringComparer.Ordinal)
                .ToArray();
            if (interfaces.Length == 0)
            {
                return Array.Empty<HomeAssistantDiscoveredInstance>();
            }

            using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutSource.CancelAfter(duration);
            var results = await Task.WhenAll(interfaces.Select((network, index) =>
                DiscoverOnInterfaceAsync(
                    network,
                    DnsDiscoveryLimits.ForInterface(index, interfaces.Length),
                    timeoutSource.Token))).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            var errors = results.Where(result => result.Error is not null).Select(result => result.Error!).ToArray();
            if (errors.Length == interfaces.Length && results.All(result => result.Instances.Count == 0))
            {
                throw new HomeAssistantConnectionException(
                    "Home Assistant mDNS discovery failed on every eligible network interface.",
                    new AggregateException(errors));
            }

            // Service-instance names and .local host names are only unique on their
            // receiving link. Each per-interface aggregate already deduplicates alias
            // responses, so never merge identities across isolated interfaces here.
            return results.SelectMany(result => result.Instances)
                .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(value => value.ServiceInstanceName, DnsNameComparer.Instance)
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

    internal static IReadOnlyList<HomeAssistantDiscoveredInstance> MergeInstances(
        IEnumerable<HomeAssistantDiscoveredInstance> instances)
    {
        if (instances is null) throw new ArgumentNullException(nameof(instances));
        return instances
            .GroupBy(
                value => value.ServiceInstanceName + "\0" + (value.HostName ?? string.Empty) + "\0" + (value.Port?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty),
                DnsNameComparer.Instance)
            .Select(group =>
            {
                var ordered = group
                    .OrderBy(value => value.ServiceInstanceName, DnsNameComparer.Instance)
                    .ThenBy(value => value.ServiceInstanceName, StringComparer.Ordinal)
                    .ToArray();
                var first = ordered[0];
                var properties = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                foreach (var instance in ordered)
                {
                    foreach (var property in instance.Properties)
                    {
                        if (properties.Count >= 64 && !properties.ContainsKey(property.Key)) continue;
                        if (!properties.ContainsKey(property.Key)) properties[property.Key] = property.Value;
                    }
                }

                return new HomeAssistantDiscoveredInstance
                {
                    ServiceInstanceName = first.ServiceInstanceName,
                    Name = ordered.Select(value => value.Properties.TryGetValue("location_name", out var name) ? name : null)
                        .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))
                        ?? first.Name,
                    InstanceId = ordered.Select(value => value.InstanceId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    Version = ordered.Select(value => value.Version).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    HostName = first.HostName,
                    Port = first.Port,
                    InternalUri = ordered.Select(value => value.InternalUri).FirstOrDefault(value => value is not null),
                    ExternalUri = ordered.Select(value => value.ExternalUri).FirstOrDefault(value => value is not null),
                    BaseUri = ordered.Select(value => value.BaseUri).FirstOrDefault(value => value is not null),
                    RequiresApiPassword = ordered.Any(value => value.RequiresApiPassword),
                    Addresses = ordered.SelectMany(value => value.Addresses)
                        .Distinct()
                        .OrderBy(value => value.AddressFamily)
                        .ThenBy(value => value.ToString(), StringComparer.Ordinal)
                        .Take(16)
                        .ToArray(),
                    Properties = properties
                };
            })
            .ToArray();
    }

    private async Task<DiscoveryInterfaceResult> DiscoverOnInterfaceAsync(
        HomeAssistantDiscoveryInterface network,
        DnsDiscoveryLimits limits,
        CancellationToken cancellationToken)
    {
        var aggregate = new DnsDiscoveryAggregate(limits);
        IHomeAssistantDiscoveryTransport? transport = null;
        try
        {
            transport = _transportFactory.Create(network);
            var query = DnsDiscoveryPacket.CreateQuery();
            await transport.SendAsync(query, cancellationToken).ConfigureAwait(false);
            var retryDelays = new[] { TimeSpan.FromSeconds(1) };
            var retryIndex = 0;
            Task? retryTask = Task.Delay(retryDelays[retryIndex], cancellationToken);
            var receiveTask = transport.ReceiveAsync(cancellationToken);
            while (!cancellationToken.IsCancellationRequested)
            {
                if (retryTask is not null && await Task.WhenAny(receiveTask, retryTask).ConfigureAwait(false) == retryTask)
                {
                    await retryTask.ConfigureAwait(false);
                    await transport.SendAsync(query, cancellationToken).ConfigureAwait(false);
                    retryIndex++;
                    retryTask = retryIndex < retryDelays.Length ? Task.Delay(retryDelays[retryIndex], cancellationToken) : null;
                    continue;
                }

                var packet = await receiveTask.ConfigureAwait(false);
                if (!aggregate.TryConsumeDatagram()) break;
                DnsDiscoveryPacket.ReadInto(packet, aggregate);
                receiveTask = transport.ReceiveAsync(cancellationToken);
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
        finally { transport?.Dispose(); }
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

internal sealed class DnsNameComparer : IEqualityComparer<string>, IComparer<string>
{
    internal static readonly DnsNameComparer Instance = new();

    private DnsNameComparer()
    {
    }

    public bool Equals(string? x, string? y) => Compare(x, y) == 0;

    public int GetHashCode(string value)
    {
        if (value is null) throw new ArgumentNullException(nameof(value));
        unchecked
        {
            var hash = 17;
            for (var index = 0; index < value.Length; index++)
            {
                hash = hash * 31 + FoldAscii(value[index]);
            }
            return hash;
        }
    }

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;
        var length = Math.Min(x.Length, y.Length);
        for (var index = 0; index < length; index++)
        {
            var left = FoldAscii(x[index]);
            var right = FoldAscii(y[index]);
            if (left != right) return left < right ? -1 : 1;
        }
        return x.Length.CompareTo(y.Length);
    }

    internal static bool EndsWith(string value, string suffix)
    {
        if (value.Length < suffix.Length) return false;
        var offset = value.Length - suffix.Length;
        for (var index = 0; index < suffix.Length; index++)
        {
            if (FoldAscii(value[offset + index]) != FoldAscii(suffix[index])) return false;
        }
        return true;
    }

    internal static string NormalizeKey(string value)
    {
        var firstLower = -1;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] is >= 'a' and <= 'z')
            {
                firstLower = index;
                break;
            }
        }
        if (firstLower < 0) return value;
        var chars = value.ToCharArray();
        for (var index = firstLower; index < chars.Length; index++) chars[index] = FoldAscii(chars[index]);
        return new string(chars);
    }

    private static char FoldAscii(char value)
        => value is >= 'a' and <= 'z' ? (char)(value - ('a' - 'A')) : value;
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
    private static readonly TimeSpan PendingRecordLifetime = TimeSpan.FromSeconds(5);
    private const int MaximumPropertiesPerOwner = 64;
    private const int MaximumRecordsPerOwner = 8;
    private const int MaximumTransitionalRecordsPerOwner = 16;
    private const int MaximumAddressesPerHost = 16;
    private const int MaximumTransitionalAddressesPerHost = 32;
    private readonly DnsDiscoveryLimits _limits;
    private readonly Func<TimeSpan> _clock;
    private readonly Func<int, int> _weightedSelector;
    private static readonly Random WeightedRandom = new();
    private readonly object _gate = new();
    private readonly Dictionary<string, CachedPtr> _instances = new(DnsNameComparer.Instance);
    private readonly Dictionary<string, List<CachedService>> _services = new(DnsNameComparer.Instance);
    private readonly Dictionary<string, List<CachedText>> _text = new(DnsNameComparer.Instance);
    private readonly Dictionary<string, Dictionary<IPAddress, CachedAddress>> _addresses = new(DnsNameComparer.Instance);
    private readonly Dictionary<string, TimeSpan> _unavailableInstances = new(DnsNameComparer.Instance);
    private int _datagrams;

    internal DnsDiscoveryAggregate(
        DnsDiscoveryLimits? limits = null,
        Func<TimeSpan>? clock = null,
        Func<int, int>? weightedSelector = null)
    {
        _limits = limits ?? DnsDiscoveryLimits.Default;
        _clock = clock ?? MonotonicNow;
        _weightedSelector = weightedSelector ?? NextWeightedSelection;
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
            if (!_instances.ContainsKey(instance)) return;
            if (!_services.TryGetValue(instance, out var services))
            {
                if (_services.Count >= _limits.Services) return;
                _services[instance] = services = new List<CachedService>();
            }
            services.RemoveAll(value => string.Equals(value.DataKey, ServiceKey(host, port), StringComparison.Ordinal));
            var expiresAt = Expiry(now, 120);
            services.Add(new CachedService(host, port, 0, 0, ServiceKey(host, port), now, expiresAt, expiresAt, true));
        }
    }

    internal void AddText(string instance, string key, string? value)
    {
        lock (_gate)
        {
            var now = _clock();
            Prune(now);
            if (!_instances.ContainsKey(instance)) return;
            if (!_text.TryGetValue(instance, out var records))
            {
                if (_text.Count >= _limits.TextOwners) return;
                _text[instance] = records = new List<CachedText>();
            }
            var cached = records.OrderByDescending(value => value.ReceivedAt).FirstOrDefault();
            if (cached is null)
            {
                var expiresAt = Expiry(now, 120);
                cached = new CachedText(new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase), string.Empty, now, expiresAt, expiresAt, true);
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
            if (!_services.Values.SelectMany(value => value).Any(value =>
                    DnsNameComparer.Instance.Equals(value.Host, host)))
            {
                return;
            }

            if (!_addresses.TryGetValue(host, out var addresses))
            {
                if (_addresses.Count >= _limits.AddressHosts) return;
                _addresses[host] = addresses = new Dictionary<IPAddress, CachedAddress>();
            }

            if (addresses.ContainsKey(address) || addresses.Count < MaximumAddressesPerHost)
            {
                var advertisedExpiry = Expiry(now, 120);
                var isVerified = IsVerifiedHost(host);
                addresses[address] = new CachedAddress(
                    now,
                    advertisedExpiry,
                    isVerified ? advertisedExpiry : Earlier(advertisedExpiry, now + PendingRecordLifetime),
                    isVerified);
            }
        }
    }

    internal void ApplyPacket(IReadOnlyList<DnsDiscoveryUpdate> updates)
    {
        lock (_gate)
        {
            var now = _clock();
            Prune(now);
            var ptrUpdates = new List<DnsDiscoveryUpdate>();
            var serviceUpdates = new List<DnsDiscoveryUpdate>();
            var unavailableServiceUpdates = new List<DnsDiscoveryUpdate>();
            var textUpdates = new List<DnsDiscoveryUpdate>();
            var addressUpdates = new List<DnsDiscoveryUpdate>();
            var goodbyeInstances = new HashSet<string>(DnsNameComparer.Instance);
            var announcedServices = new Dictionary<string, HashSet<string>>(DnsNameComparer.Instance);
            var announcedText = new Dictionary<string, HashSet<string>>(DnsNameComparer.Instance);
            var announcedIpv4 = new Dictionary<string, HashSet<IPAddress>>(DnsNameComparer.Instance);
            var announcedIpv6 = new Dictionary<string, HashSet<IPAddress>>(DnsNameComparer.Instance);
            var unrelatedServiceHosts = new HashSet<string>(DnsNameComparer.Instance);
            foreach (var update in updates)
            {
                switch (update.Kind)
                {
                    case DnsDiscoveryRecordKind.Ptr when IsHomeAssistantPtr(update):
                        ptrUpdates.Add(update);
                        if (update.Ttl == 0) goodbyeInstances.Add(update.Target!);
                        break;
                    case DnsDiscoveryRecordKind.Srv when IsHomeAssistantInstanceName(update.Name) && update.NoService:
                        unavailableServiceUpdates.Add(update);
                        break;
                    case DnsDiscoveryRecordKind.Srv when IsHomeAssistantInstanceName(update.Name):
                        serviceUpdates.Add(update);
                        AddAnnouncement(announcedServices, update);
                        break;
                    case DnsDiscoveryRecordKind.Srv when update.Host is not null:
                        unrelatedServiceHosts.Add(update.Host);
                        break;
                    case DnsDiscoveryRecordKind.Txt when IsHomeAssistantInstanceName(update.Name):
                        textUpdates.Add(update);
                        AddAnnouncement(announcedText, update);
                        break;
                    case DnsDiscoveryRecordKind.A:
                        addressUpdates.Add(update);
                        AddAddressAnnouncement(announcedIpv4, update);
                        break;
                    case DnsDiscoveryRecordKind.Aaaa:
                        addressUpdates.Add(update);
                        AddAddressAnnouncement(announcedIpv6, update);
                        break;
                }
            }

            foreach (var update in serviceUpdates.Where(value => value.Ttl > 0))
                _unavailableInstances.Remove(update.Name);
            foreach (var update in unavailableServiceUpdates)
                ApplyUnavailableService(update, now);

            var acceptedInstances = new HashSet<string>(_instances.Keys, DnsNameComparer.Instance);
            foreach (var update in ptrUpdates.Where(value => !_unavailableInstances.ContainsKey(value.Target!)))
            {
                acceptedInstances.Add(update.Target!);
                ApplyPtr(update, now);
            }

            acceptedInstances.IntersectWith(_instances.Keys);
            var acceptedServiceUpdates = serviceUpdates
                .Where(value => !goodbyeInstances.Contains(value.Name) || acceptedInstances.Contains(value.Name))
                .ToArray();
            var rejectedServiceHosts = new HashSet<string>(
                serviceUpdates.Except(acceptedServiceUpdates).Select(value => value.Host!),
                DnsNameComparer.Instance);
            var homeAssistantServiceHosts = new HashSet<string>(
                serviceUpdates.Select(value => value.Host!),
                DnsNameComparer.Instance);
            foreach (var update in acceptedServiceUpdates)
                ApplyService(update, now, Announcement(announcedServices, update), acceptedInstances.Contains(update.Name));
            foreach (var update in textUpdates.Where(value => !goodbyeInstances.Contains(value.Name) || acceptedInstances.Contains(value.Name)))
                ApplyText(update, now, Announcement(announcedText, update), acceptedInstances.Contains(update.Name));

            var acceptedHosts = new HashSet<string>(
                _services.SelectMany(value => value.Value).Select(value => value.Host),
                DnsNameComparer.Instance);
            var verifiedHosts = new HashSet<string>(
                _services.Values.SelectMany(value => value).Where(value => value.IsVerified).Select(value => value.Host),
                DnsNameComparer.Instance);
            foreach (var update in addressUpdates.Where(value =>
                         acceptedHosts.Contains(value.Name)
                         || !homeAssistantServiceHosts.Contains(value.Name)
                            && !unrelatedServiceHosts.Contains(value.Name)
                            && !rejectedServiceHosts.Contains(value.Name)))
                ApplyAddress(update, now, update.Kind == DnsDiscoveryRecordKind.A
                    ? AddressAnnouncement(announcedIpv4, update)
                    : AddressAnnouncement(announcedIpv6, update),
                    verifiedHosts.Contains(update.Name));
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
                var service = SelectService(serviceRecords);
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
                .ThenBy(value => value.ServiceInstanceName, DnsNameComparer.Instance)
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
        {
            _instances[instance] = new CachedPtr(update.DataKey, now, Expiry(now, update.Ttl));
            if (_services.TryGetValue(instance, out var services))
                foreach (var service in services)
                {
                    service.IsVerified = true;
                    service.ExpiresAt = service.AdvertisedExpiresAt;
                    PromoteAddresses(service.Host);
                }
            if (_text.TryGetValue(instance, out var textRecords))
                foreach (var text in textRecords)
                {
                    text.IsVerified = true;
                    text.ExpiresAt = text.AdvertisedExpiresAt;
                }
        }
    }

    private void ApplyService(
        DnsDiscoveryUpdate update,
        TimeSpan now,
        HashSet<string>? announced,
        bool isVerified)
    {
        if (!_services.TryGetValue(update.Name, out var records))
        {
            if (update.Ttl == 0 || !EnsureServiceOwnerCapacity(isVerified)) return;
            _services[update.Name] = records = new List<CachedService>();
        }
        if (update.Ttl == 0)
        {
            foreach (var existing in records.Where(value => string.Equals(value.DataKey, update.DataKey, StringComparison.Ordinal)))
                Shorten(existing, now + TimeSpan.FromSeconds(1));
            return;
        }
        if (update.CacheFlush)
        {
            foreach (var old in records.Where(value => !announced!.Contains(value.DataKey) && now - value.ReceivedAt >= TimeSpan.FromSeconds(1)))
                Shorten(old, now + TimeSpan.FromSeconds(1));
        }
        var replaced = records.RemoveAll(value => string.Equals(value.DataKey, update.DataKey, StringComparison.Ordinal)) > 0;
        if (replaced || records.Count < MaximumRecordsPerOwner || (announced is not null && records.Count < MaximumTransitionalRecordsPerOwner))
        {
            var advertisedExpiry = Expiry(now, update.Ttl);
            records.Add(new CachedService(
                update.Host!,
                update.Port,
                update.Priority,
                update.Weight,
                update.DataKey,
                now,
                advertisedExpiry,
                isVerified ? advertisedExpiry : Earlier(advertisedExpiry, now + PendingRecordLifetime),
                isVerified));
            if (isVerified) PromoteAddresses(update.Host!);
        }
    }

    private void ApplyUnavailableService(DnsDiscoveryUpdate update, TimeSpan now)
    {
        if (update.Ttl == 0)
        {
            _unavailableInstances.Remove(update.Name);
            return;
        }

        if (!_unavailableInstances.ContainsKey(update.Name)
            && _unavailableInstances.Count >= _limits.Instances)
        {
            return;
        }

        _unavailableInstances[update.Name] = Expiry(now, update.Ttl);
        _instances.Remove(update.Name);
        _services.Remove(update.Name);
        _text.Remove(update.Name);
        RemoveUnreferencedAddresses();
    }

    private void ApplyText(
        DnsDiscoveryUpdate update,
        TimeSpan now,
        HashSet<string>? announced,
        bool isVerified)
    {
        if (!_text.TryGetValue(update.Name, out var records))
        {
            if (update.Ttl == 0 || !EnsureTextOwnerCapacity(isVerified)) return;
            _text[update.Name] = records = new List<CachedText>();
        }
        if (update.Ttl == 0)
        {
            foreach (var existing in records.Where(value => string.Equals(value.DataKey, update.DataKey, StringComparison.Ordinal)))
                Shorten(existing, now + TimeSpan.FromSeconds(1));
            return;
        }
        if (update.CacheFlush)
        {
            foreach (var old in records.Where(value => !announced!.Contains(value.DataKey) && now - value.ReceivedAt >= TimeSpan.FromSeconds(1)))
                Shorten(old, now + TimeSpan.FromSeconds(1));
        }
        var replaced = records.RemoveAll(value => string.Equals(value.DataKey, update.DataKey, StringComparison.Ordinal)) > 0;
        if (replaced || records.Count < MaximumRecordsPerOwner || (announced is not null && records.Count < MaximumTransitionalRecordsPerOwner))
        {
            var advertisedExpiry = Expiry(now, update.Ttl);
            records.Add(new CachedText(
                update.Properties!,
                update.DataKey,
                now,
                advertisedExpiry,
                isVerified ? advertisedExpiry : Earlier(advertisedExpiry, now + PendingRecordLifetime),
                isVerified));
        }
    }

    private void ApplyAddress(
        DnsDiscoveryUpdate update,
        TimeSpan now,
        HashSet<IPAddress>? announced,
        bool isVerifiedHost)
    {
        if (!_addresses.TryGetValue(update.Name, out var addresses))
        {
            if (update.Ttl == 0 || !EnsureAddressHostCapacity(isVerifiedHost)) return;
            _addresses[update.Name] = addresses = new Dictionary<IPAddress, CachedAddress>();
        }
        var address = update.Address!;
        if (update.Ttl == 0)
        {
            if (addresses.TryGetValue(address, out var existing))
                Shorten(existing, now + TimeSpan.FromSeconds(1));
            return;
        }
        if (update.CacheFlush)
        {
            foreach (var pair in addresses.Where(pair => pair.Key.AddressFamily == address.AddressFamily && !announced!.Contains(pair.Key) && now - pair.Value.ReceivedAt >= TimeSpan.FromSeconds(1)).ToArray())
                Shorten(pair.Value, now + TimeSpan.FromSeconds(1));
        }
        if (addresses.ContainsKey(address)
            || addresses.Count < MaximumAddressesPerHost
            || (announced is not null && addresses.Count < MaximumTransitionalAddressesPerHost))
        {
            var advertisedExpiry = Expiry(now, update.Ttl);
            addresses[address] = new CachedAddress(
                now,
                advertisedExpiry,
                isVerifiedHost ? advertisedExpiry : Earlier(advertisedExpiry, now + PendingRecordLifetime),
                isVerifiedHost);
        }
    }

    private bool EnsureServiceOwnerCapacity(bool isVerified)
    {
        if (_services.Count < _limits.Services) return true;
        if (!isVerified) return false;
        var pendingOwner = _services
            .Where(pair => pair.Value.Count > 0 && pair.Value.All(record => !record.IsVerified))
            .OrderBy(pair => pair.Value.Min(record => record.ReceivedAt))
            .ThenBy(pair => pair.Key, DnsNameComparer.Instance)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (pendingOwner is null) return false;
        _services.Remove(pendingOwner);
        RemoveUnreferencedAddresses();
        return true;
    }

    private bool EnsureTextOwnerCapacity(bool isVerified)
    {
        if (_text.Count < _limits.TextOwners) return true;
        if (!isVerified) return false;
        var pendingOwner = _text
            .Where(pair => pair.Value.Count > 0 && pair.Value.All(record => !record.IsVerified))
            .OrderBy(pair => pair.Value.Min(record => record.ReceivedAt))
            .ThenBy(pair => pair.Key, DnsNameComparer.Instance)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (pendingOwner is null) return false;
        _text.Remove(pendingOwner);
        return true;
    }

    private bool EnsureAddressHostCapacity(bool isVerifiedHost)
    {
        if (_addresses.Count < _limits.AddressHosts) return true;
        if (!isVerifiedHost) return false;
        var verifiedHosts = new HashSet<string>(
            _services.Values.SelectMany(value => value).Where(value => value.IsVerified).Select(value => value.Host),
            DnsNameComparer.Instance);
        var pendingHost = _addresses
            .Where(pair => pair.Value.Count > 0 && !verifiedHosts.Contains(pair.Key))
            .OrderBy(pair => pair.Value.Min(record => record.Value.ReceivedAt))
            .ThenBy(pair => pair.Key, DnsNameComparer.Instance)
            .ThenBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key)
            .FirstOrDefault();
        if (pendingHost is null) return false;
        _addresses.Remove(pendingHost);
        return true;
    }

    private void RemoveUnreferencedAddresses()
    {
        var retainedHosts = new HashSet<string>(
            _services.Values.SelectMany(value => value).Select(value => value.Host),
            DnsNameComparer.Instance);
        foreach (var owner in _addresses.Keys.Where(owner => !retainedHosts.Contains(owner)).ToArray())
        {
            var values = _addresses[owner];
            foreach (var address in values.Where(value => value.Value.IsVerified).Select(value => value.Key).ToArray())
                values.Remove(address);
            if (values.Count == 0) _addresses.Remove(owner);
        }
    }

    private static void AddAnnouncement(
        Dictionary<string, HashSet<string>> announcements,
        DnsDiscoveryUpdate update)
    {
        if (update.Ttl == 0) return;
        if (!announcements.TryGetValue(update.Name, out var values))
            announcements[update.Name] = values = new HashSet<string>(StringComparer.Ordinal);
        values.Add(update.DataKey);
    }

    private static void AddAddressAnnouncement(
        Dictionary<string, HashSet<IPAddress>> announcements,
        DnsDiscoveryUpdate update)
    {
        if (update.Ttl == 0 || update.Address is null) return;
        if (!announcements.TryGetValue(update.Name, out var values))
            announcements[update.Name] = values = new HashSet<IPAddress>();
        values.Add(update.Address);
    }

    private static HashSet<string>? Announcement(
        Dictionary<string, HashSet<string>> announcements,
        DnsDiscoveryUpdate update)
        => update.CacheFlush && announcements.TryGetValue(update.Name, out var values) ? values : null;

    private static HashSet<IPAddress>? AddressAnnouncement(
        Dictionary<string, HashSet<IPAddress>> announcements,
        DnsDiscoveryUpdate update)
        => update.CacheFlush && announcements.TryGetValue(update.Name, out var values) ? values : null;

    private static void Shorten(CachedService record, TimeSpan expiresAt)
    {
        record.AdvertisedExpiresAt = Earlier(record.AdvertisedExpiresAt, expiresAt);
        record.ExpiresAt = Earlier(record.ExpiresAt, expiresAt);
    }

    private static void Shorten(CachedText record, TimeSpan expiresAt)
    {
        record.AdvertisedExpiresAt = Earlier(record.AdvertisedExpiresAt, expiresAt);
        record.ExpiresAt = Earlier(record.ExpiresAt, expiresAt);
    }

    private static void Shorten(CachedAddress record, TimeSpan expiresAt)
    {
        record.AdvertisedExpiresAt = Earlier(record.AdvertisedExpiresAt, expiresAt);
        record.ExpiresAt = Earlier(record.ExpiresAt, expiresAt);
    }

    private bool IsVerifiedHost(string host)
        => _services.Values.SelectMany(value => value).Any(value =>
            value.IsVerified && DnsNameComparer.Instance.Equals(value.Host, host));

    private void PromoteAddresses(string host)
    {
        if (!_addresses.TryGetValue(host, out var addresses)) return;
        foreach (var address in addresses.Values)
        {
            address.IsVerified = true;
            address.ExpiresAt = address.AdvertisedExpiresAt;
        }
    }

    private void Prune(TimeSpan now)
    {
        foreach (var key in _unavailableInstances.Where(value => value.Value <= now).Select(value => value.Key).ToArray())
            _unavailableInstances.Remove(key);
        foreach (var key in _instances.Where(value => value.Value.ExpiresAt <= now).Select(value => value.Key).ToArray()) _instances.Remove(key);
        foreach (var owner in _services.Keys.ToArray()) { _services[owner].RemoveAll(value => value.ExpiresAt <= now || value.IsVerified && !_instances.ContainsKey(owner)); if (_services[owner].Count == 0) _services.Remove(owner); }
        foreach (var owner in _text.Keys.ToArray()) { _text[owner].RemoveAll(value => value.ExpiresAt <= now || value.IsVerified && !_instances.ContainsKey(owner)); if (_text[owner].Count == 0) _text.Remove(owner); }
        var retainedHosts = new HashSet<string>(_services.Values.SelectMany(value => value).Select(value => value.Host), DnsNameComparer.Instance);
        foreach (var owner in _addresses.Keys.ToArray())
        {
            var values = _addresses[owner];
            foreach (var address in values.Where(value =>
                         value.Value.ExpiresAt <= now
                         || value.Value.IsVerified && !retainedHosts.Contains(owner)).Select(value => value.Key).ToArray())
                values.Remove(address);
            if (values.Count == 0) _addresses.Remove(owner);
        }
    }

    private static bool IsHomeAssistantPtr(DnsDiscoveryUpdate update)
        => DnsNameComparer.Instance.Equals(update.Name, ServiceName)
            && update.Target is not null
            && IsHomeAssistantInstanceName(update.Target);
    private static bool IsHomeAssistantInstanceName(string value)
        => value.Length > ServiceName.Length + 1
            && DnsNameComparer.EndsWith(value, "." + ServiceName);
    private static string ServiceKey(string host, int port) => DnsNameComparer.NormalizeKey(host) + "\0" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);
    private static TimeSpan Expiry(TimeSpan now, uint ttl) => now + TimeSpan.FromSeconds(ttl == 0 ? 1 : ttl);
    private static TimeSpan Earlier(TimeSpan left, TimeSpan right) => left <= right ? left : right;
    private static TimeSpan MonotonicNow() => TimeSpan.FromSeconds((double)System.Diagnostics.Stopwatch.GetTimestamp() / System.Diagnostics.Stopwatch.Frequency);

    private CachedService? SelectService(IReadOnlyList<CachedService>? records)
    {
        if (records is null || records.Count == 0)
        {
            return null;
        }

        var priority = records.Min(value => value.Priority);
        var candidates = records.Where(value => value.Priority == priority)
            .OrderBy(value => value.Weight == 0 ? 0 : 1)
            .ThenBy(value => value.DataKey, StringComparer.Ordinal)
            .ToArray();
        var zeroWeightCount = candidates.TakeWhile(value => value.Weight == 0).Count();
        if (zeroWeightCount > 1)
        {
            var rotation = _weightedSelector(zeroWeightCount);
            if (rotation < 0 || rotation >= zeroWeightCount)
                throw new InvalidOperationException("The weighted service selector returned an out-of-range value.");
            var orderedZeroWeights = candidates.Take(zeroWeightCount).ToArray();
            for (var index = 0; index < zeroWeightCount; index++)
                candidates[index] = orderedZeroWeights[(index + rotation) % zeroWeightCount];
        }
        var totalWeight = candidates.Sum(value => value.Weight);
        if (totalWeight == 0)
        {
            return candidates[_weightedSelector(candidates.Length)];
        }

        var selected = _weightedSelector(checked(totalWeight + 1));
        var cumulativeWeight = 0;
        foreach (var candidate in candidates)
        {
            cumulativeWeight += candidate.Weight;
            if (cumulativeWeight >= selected)
            {
                return candidate;
            }
        }

        return candidates[candidates.Length - 1];
    }

    private static int NextWeightedSelection(int exclusiveMaximum)
    {
        lock (WeightedRandom)
        {
            return WeightedRandom.Next(exclusiveMaximum);
        }
    }

    private abstract class CachedRecord
    {
        protected CachedRecord(string dataKey, TimeSpan receivedAt, TimeSpan expiresAt) { DataKey = dataKey; ReceivedAt = receivedAt; ExpiresAt = expiresAt; }
        internal string DataKey { get; }
        internal TimeSpan ReceivedAt { get; }
        internal TimeSpan ExpiresAt { get; set; }
    }
    private sealed class CachedPtr : CachedRecord { internal CachedPtr(string key, TimeSpan receivedAt, TimeSpan expiresAt) : base(key, receivedAt, expiresAt) { } }
    private sealed class CachedService : CachedRecord { internal CachedService(string host, int port, int priority, int weight, string key, TimeSpan receivedAt, TimeSpan advertisedExpiresAt, TimeSpan expiresAt, bool isVerified) : base(key, receivedAt, expiresAt) { Host = host; Port = port; Priority = priority; Weight = weight; AdvertisedExpiresAt = advertisedExpiresAt; IsVerified = isVerified; } internal string Host { get; } internal int Port { get; } internal int Priority { get; } internal int Weight { get; } internal TimeSpan AdvertisedExpiresAt { get; set; } internal bool IsVerified { get; set; } }
    private sealed class CachedText : CachedRecord { internal CachedText(Dictionary<string, string?> properties, string key, TimeSpan receivedAt, TimeSpan advertisedExpiresAt, TimeSpan expiresAt, bool isVerified) : base(key, receivedAt, expiresAt) { Properties = properties; AdvertisedExpiresAt = advertisedExpiresAt; IsVerified = isVerified; } internal Dictionary<string, string?> Properties { get; } internal TimeSpan AdvertisedExpiresAt { get; set; } internal bool IsVerified { get; set; } }
    private sealed class CachedAddress
    {
        internal CachedAddress(TimeSpan receivedAt, TimeSpan advertisedExpiresAt, TimeSpan expiresAt, bool isVerified)
        {
            ReceivedAt = receivedAt;
            AdvertisedExpiresAt = advertisedExpiresAt;
            ExpiresAt = expiresAt;
            IsVerified = isVerified;
        }

        internal TimeSpan ReceivedAt { get; }
        internal TimeSpan AdvertisedExpiresAt { get; set; }
        internal TimeSpan ExpiresAt { get; set; }
        internal bool IsVerified { get; set; }
    }

    private static string FriendlyInstanceName(string instance)
    {
        var suffix = "." + ServiceName;
        var value = DnsNameComparer.EndsWith(instance, suffix) ? instance.Substring(0, instance.Length - suffix.Length) : instance;
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
    internal int Priority { get; set; }
    internal int Weight { get; set; }
    internal bool NoService { get; set; }
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
                        updates.Add(new DnsDiscoveryUpdate { Kind = DnsDiscoveryRecordKind.Ptr, Name = name, Target = instance, DataKey = DnsNameComparer.NormalizeKey(instance), Ttl = ttl, CacheFlush = cacheFlush });
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
                        updates.Add(new DnsDiscoveryUpdate { Kind = DnsDiscoveryRecordKind.Srv, Name = name, Host = host, Port = port, Priority = priority, Weight = weight, NoService = host.Length == 0, DataKey = priority.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0" + weight.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\0" + DnsNameComparer.NormalizeKey(host) + "\0" + port.ToString(System.Globalization.CultureInfo.InvariantCulture), Ttl = ttl, CacheFlush = cacheFlush });
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
                        // Discovery transports and their interface identity are explicitly
                        // IPv4. An AAAA record cannot be associated with the IPv6 scope that
                        // makes link-local addresses usable, so validate but do not expose it.
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
        var expandedWireLength = 1; // The terminating root-label byte.
        while (true)
        {
            Require(packet, position, 1);
            var length = packet[position++];
            if (length == 0) break;
            if ((length & 0xC0) == 0xC0)
            {
                var pointerLocation = position - 1;
                Require(packet, position, 1);
                var pointer = ((length & 0x3F) << 8) | packet[position++];
                if (pointer >= pointerLocation || ++hops > 32) throw new InvalidDataException("Invalid DNS compression pointer.");
                if (next < 0) next = position;
                position = pointer;
                continue;
            }
            if ((length & 0xC0) != 0 || length > 63) throw new InvalidDataException("Invalid DNS label.");
            Require(packet, position, length);
            expandedWireLength = checked(expandedWireLength + length + 1); // Length byte plus label bytes.
            if (expandedWireLength > 255 || labels.Count >= 127) throw new InvalidDataException("DNS name exceeds the protocol limit.");
            try
            {
                labels.Add(StrictUtf8.GetString(packet, position, length));
            }
            catch (DecoderFallbackException exception)
            {
                throw new InvalidDataException("DNS names must contain valid UTF-8 labels.", exception);
            }
            position += length;
        }
        offset = next < 0 ? position : next;
        return FormatName(labels);
    }

    internal static string FormatName(IReadOnlyList<string> labels)
    {
        if (labels.Count == 0) return string.Empty;
        var builder = new StringBuilder();
        for (var labelIndex = 0; labelIndex < labels.Count; labelIndex++)
        {
            if (labelIndex > 0) builder.Append('.');
            var label = labels[labelIndex];
            foreach (var character in label)
            {
                // DNS presentation syntax escapes separators that are literal
                // bytes inside a label. This keeps aggregate dictionary keys
                // distinct from an equivalent-looking sequence of labels.
                if (character == '.' || character == '\\') builder.Append('\\');
                builder.Append(character);
            }
        }
        return builder.ToString();
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
