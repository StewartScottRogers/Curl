using System.Net;
using Curl.Networking.Fakes;

namespace Curl.Networking;

[TestClass]
public sealed class TcpConnectorResolveShuffleTests
{
    private const string Entry = "foo.example:80:192.0.2.1,192.0.2.2,[2001:db8::3]";
    private const string CacheKey = "foo.example:80";

    private static readonly IPAddress First = IPAddress.Parse("192.0.2.1");
    private static readonly IPAddress Second = IPAddress.Parse("192.0.2.2");
    private static readonly IPAddress Third = IPAddress.Parse("2001:db8::3");

    [TestMethod]
    public void LoadResolveEntries_ShuffleOn_CachesTheAddressesShuffledAndWritesTheEntryAsWritten()
    {
        // curl 8.22.0's dnsc_entry_assign_addr shuffles every address entry under CURLOPT_DNS_SHUFFLE_ADDRESSES,
        // a --resolve entry's included: Fisher-Yates from the last address down, here each swap with index 0.
        var dnsCache = new DnsCache();
        var connector = CreateConnector(dnsCache, shufflesResolveEntryAddresses: true);
        var events = new RecordingTransferEvents();

        connector.LoadResolveEntries(events);

        CollectionAssert.AreEqual(new[] { Second, Third, First }, dnsCache.Find(CacheKey)!.Addresses.ToArray());
        CollectionAssert.AreEqual(new[] { $"[DNS] added {Entry} to cache" }, events.Info);
    }

    [TestMethod]
    public void LoadResolveEntries_ShuffleOff_CachesTheAddressesInTheWrittenOrder()
    {
        var dnsCache = new DnsCache();
        var connector = CreateConnector(dnsCache, shufflesResolveEntryAddresses: false);

        connector.LoadResolveEntries(new RecordingTransferEvents());

        CollectionAssert.AreEqual(new[] { First, Second, Third }, dnsCache.Find(CacheKey)!.Addresses.ToArray());
    }

    private static TcpConnector CreateConnector(DnsCache dnsCache, bool shufflesResolveEntryAddresses) =>
        new(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(), resolveOverrides: ResolveOverrides.Parse([Entry]), dnsCache: dnsCache)
        {
            ShufflesResolveEntryAddresses = shufflesResolveEntryAddresses,
            ResolveEntryShuffleRandomBelow = _ => 0,
        };
}
