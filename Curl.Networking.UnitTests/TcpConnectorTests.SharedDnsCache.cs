using System.Net;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives one <see cref="DnsCache" /> shared by the connectors of two <c>-:</c>/<c>--next</c> option
/// groups, as curl 8.21.0 keeps one DNS cache for the whole command line (measured 2026-10-02,
/// BL-1053): the second group answers what the first resolved, or what the first's
/// <c>--resolve</c> entry added, from it.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const int SharedCachePort = 18531;

    [TestMethod]
    public async Task ConnectAsync_ThroughASharedDnsCache_AnswersAHostAnotherConnectorResolvedFromTheCache()
    {
        // curl -sv http://localhost:18531/a --next -sv http://localhost:18531/b, each closed ->
        // group 2: * Hostname localhost was found in DNS cache / * Host localhost:18531 was resolved.
        // / * IPv6: ::1 / * IPv4: 127.0.0.1 / *   Trying [::1]:18531...
        var resolver = new FakeDnsResolver(IPAddress.IPv6Loopback, Loopback);
        var dnsCache = new DnsCache();
        var firstGroup = CreateConnectorOver(resolver, dnsCache);
        var secondGroup = CreateConnectorOver(resolver, dnsCache);
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await firstGroup.ConnectAsync(new ConnectTarget("localhost", SharedCachePort, UseTls: false) { Events = first }, CancellationToken.None);
        await secondGroup.ConnectAsync(new ConnectTarget("localhost", SharedCachePort, UseTls: false) { Events = second }, CancellationToken.None);

        string[] resolvedLines = ["Host localhost:18531 was resolved.", "IPv6: ::1", "IPv4: 127.0.0.1", "  Trying [::1]:18531..."];
        CollectionAssert.AreEqual(resolvedLines, first.Info);
        CollectionAssert.AreEqual(new[] { "Hostname localhost was found in DNS cache" }.Concat(resolvedLines).ToArray(), second.Info);
        CollectionAssert.AreEqual(new[] { "localhost" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughASharedDnsCache_AnswersAnIpAddressAnotherConnectorDialledFromTheCache()
    {
        // curl -sv http://127.0.0.1:18531/a --next -sv http://127.0.0.1:18531/b -> group 2:
        // * Hostname 127.0.0.1 was found in DNS cache / *   Trying 127.0.0.1:18531...
        var resolver = new FakeDnsResolver(Loopback);
        var dnsCache = new DnsCache();
        var second = new RecordingTransferEvents();

        await CreateConnectorOver(resolver, dnsCache).ConnectAsync(new ConnectTarget("127.0.0.1", SharedCachePort, UseTls: false), CancellationToken.None);
        await CreateConnectorOver(resolver, dnsCache).ConnectAsync(new ConnectTarget("127.0.0.1", SharedCachePort, UseTls: false) { Events = second }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Hostname 127.0.0.1 was found in DNS cache", "  Trying 127.0.0.1:18531..." }, second.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughSeparateDnsCaches_ResolvesTheHostAgainWithoutTheFoundInDnsCacheLine()
    {
        var resolver = new FakeDnsResolver(Loopback);
        var second = new RecordingTransferEvents();

        await CreateConnectorOver(resolver, new DnsCache()).ConnectAsync(new ConnectTarget("127.0.0.1", SharedCachePort, UseTls: false), CancellationToken.None);
        await CreateConnectorOver(resolver, new DnsCache()).ConnectAsync(new ConnectTarget("127.0.0.1", SharedCachePort, UseTls: false) { Events = second }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:18531..." }, second.Info);
        CollectionAssert.AreEqual(new[] { "127.0.0.1", "127.0.0.1" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    [DataRow("foo.example:18531:127.0.0.1")]
    [DataRow("+foo.example:18531:127.0.0.1")]
    public async Task ConnectAsync_ThroughASharedDnsCache_AnswersFromAnotherConnectorsResolveEntry(string resolveEntry)
    {
        // curl -sv --resolve [+]foo.example:18531:127.0.0.1 http://foo.example:18531/a --next
        // -sv http://foo.example:18531/b -> group 2, which has no --resolve: * Hostname foo.example
        // was found in DNS cache / * Host foo.example:18531 was resolved. / * IPv6: (none) / * IPv4: 127.0.0.1
        var resolver = new FakeDnsResolver();
        var dnsCache = new DnsCache();
        var firstGroup = new TcpConnector(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider(), resolveOverrides: ResolveOverrides.Parse([resolveEntry]), dnsCache: dnsCache);
        var second = new RecordingTransferEvents();

        await firstGroup.ConnectAsync(new ConnectTarget("foo.example", SharedCachePort, UseTls: false), CancellationToken.None);
        await CreateConnectorOver(resolver, dnsCache).ConnectAsync(new ConnectTarget("foo.example", SharedCachePort, UseTls: false) { Events = second }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "Hostname foo.example was found in DNS cache", "Host foo.example:18531 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", "  Trying 127.0.0.1:18531..." },
            second.Info);
        Assert.IsEmpty(resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task LoadResolveEntries_OverAHostAnotherConnectorResolved_DiscardsTheOldAddressesAndAnswersWithTheEntry()
    {
        // curl -sv http://localhost:18531/a --next -sv --resolve localhost:18531:127.0.0.1 http://localhost:18531/b
        // -> group 2: * RESOLVE localhost:18531 - old addresses discarded / * Added localhost:18531:127.0.0.1
        // to DNS cache / * Hostname localhost was found in DNS cache / * Host localhost:18531 was resolved.
        // / * IPv6: (none) / * IPv4: 127.0.0.1 / *   Trying 127.0.0.1:18531...
        var resolver = new FakeDnsResolver(IPAddress.IPv6Loopback, Loopback);
        var dnsCache = new DnsCache();
        var secondGroup = new TcpConnector(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider(), resolveOverrides: ResolveOverrides.Parse(["localhost:18531:127.0.0.1"]), dnsCache: dnsCache);
        var second = new RecordingTransferEvents();

        await CreateConnectorOver(resolver, dnsCache).ConnectAsync(new ConnectTarget("localhost", SharedCachePort, UseTls: false), CancellationToken.None);
        secondGroup.LoadResolveEntries(second);
        await secondGroup.ConnectAsync(new ConnectTarget("localhost", SharedCachePort, UseTls: false) { Events = second }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "RESOLVE localhost:18531 - old addresses discarded",
                "Added localhost:18531:127.0.0.1 to DNS cache",
                "Hostname localhost was found in DNS cache",
                "Host localhost:18531 was resolved.",
                "IPv6: (none)",
                "IPv4: 127.0.0.1",
                "  Trying 127.0.0.1:18531...",
            },
            second.Info);
        CollectionAssert.AreEqual(new[] { "localhost" }, resolver.ResolvedHosts);
    }

    private static TcpConnector CreateConnectorOver(FakeDnsResolver resolver, DnsCache dnsCache) =>
        new(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider(), dnsCache: dnsCache);
}
