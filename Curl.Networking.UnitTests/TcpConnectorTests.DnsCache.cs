using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives curl 8.21.0's DNS cache in <see cref="TcpConnector" />: the
/// <c>Hostname H was found in DNS cache</c> line it reports before connecting to a host and
/// port it already resolved on the command line, or one a <c>--resolve</c> entry answers
/// (measured 2026-09-27, BL-481).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string TryingLoopback = "  Trying 127.0.0.1:47181...";

    [TestMethod]
    public async Task ConnectAsync_ToAHostAndPortAlreadyConnectedTo_ReportsFoundInDnsCacheBeforeTryingOnlyTheSecondTime()
    {
        // curl -s -v http://127.0.0.1:47181/a http://127.0.0.1:47181/b, each closed ->
        // *   Trying 127.0.0.1:47181... ... * shutting down connection #0
        // * Hostname 127.0.0.1 was found in DNS cache
        // *   Trying 127.0.0.1:47181...
        var resolver = new FakeDnsResolver(Loopback);
        var connector = CreateConnector(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider());
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = first }, CancellationToken.None);
        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = second }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { TryingLoopback }, first.Info);
        CollectionAssert.AreEqual(new[] { "Hostname 127.0.0.1 was found in DNS cache", TryingLoopback }, second.Info);
        CollectionAssert.AreEqual(new[] { "127.0.0.1" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_ToANameAlreadyResolved_ReportsFoundInDnsCacheAndDialsTheCachedAddresses()
    {
        // curl -s -v http://localhost:47181/a http://localhost:47181/b ->
        // * Hostname localhost was found in DNS cache before the second transfer's Trying lines.
        var resolver = new FakeDnsResolver(IPAddress.IPv6Loopback, Loopback);
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(resolver, dialer, new FakeTlsProvider());
        var second = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget("localhost", 47181, UseTls: false), CancellationToken.None);
        await connector.ConnectAsync(new ConnectTarget("localhost", 47181, UseTls: false) { Events = second }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Hostname localhost was found in DNS cache", "  Trying [::1]:47181..." }, second.Info);
        CollectionAssert.AreEqual(new[] { "localhost" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_ToAnotherHostOrPort_ResolvesItWithoutTheFoundInDnsCacheLine()
    {
        // curl -s -v http://127.0.0.1:47181/a http://localhost:47181/b -> no cache line for localhost;
        // curl -s -v http://127.0.0.1:1/a http://127.0.0.1:47181/b -> none for the second port either.
        var resolver = new FakeDnsResolver(Loopback);
        var connector = CreateConnector(resolver, new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider());
        var otherHost = new RecordingTransferEvents();
        var otherPort = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47181, UseTls: false), CancellationToken.None);
        await connector.ConnectAsync(new ConnectTarget("localhost", 47181, UseTls: false) { Events = otherHost }, CancellationToken.None);
        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = otherPort }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { TryingLoopback }, otherHost.Info);
        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:1..." }, otherPort.Info);
        CollectionAssert.AreEqual(new[] { "127.0.0.1", "localhost", "127.0.0.1" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterADialThatReachedNoAddress_StillReportsTheHostFoundInDnsCache()
    {
        // curl -s -v http://127.0.0.1:1/a http://127.0.0.1:1/b -> both refused, and
        // * Hostname 127.0.0.1 was found in DNS cache before the second Trying.
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());
        var second = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false), CancellationToken.None);
        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = second }, CancellationToken.None);

        Assert.AreEqual("Hostname 127.0.0.1 was found in DNS cache", second.Info[0]);
        Assert.AreEqual("  Trying 127.0.0.1:1...", second.Info[1]);
    }

    [TestMethod]
    public async Task ConnectAsync_AfterAHostThatDidNotResolve_ResolvesItAgainWithoutTheFoundInDnsCacheLine()
    {
        // curl -s -v http://nonexistent.invalid:47181/a http://nonexistent.invalid:47181/b ->
        // Could not resolve host twice, and no cache line.
        var resolver = new FakeDnsResolver();
        var connector = CreateConnector(resolver, new FakeTcpDialer(), new FakeTlsProvider());
        var second = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget("nonexistent.invalid", 47181, UseTls: false), CancellationToken.None);
        var result = await connector.ConnectAsync(new ConnectTarget("nonexistent.invalid", 47181, UseTls: false) { Events = second }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.IsEmpty(second.Info);
        CollectionAssert.AreEqual(new[] { "nonexistent.invalid", "nonexistent.invalid" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAResolveEntry_ReportsFoundInDnsCacheOnEveryConnectIncludingTheFirst()
    {
        // curl -s -v --resolve foo.example:47181:127.0.0.1 http://foo.example:47181/a http://foo.example:47181/b
        // -> * Hostname foo.example was found in DNS cache before each transfer's Trying.
        var connector = new TcpConnector(
            new FakeDnsResolver(), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["foo.example:47181:127.0.0.1"]));
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget("foo.example", 47181, UseTls: false) { Events = first }, CancellationToken.None);
        await connector.ConnectAsync(new ConnectTarget("foo.example", 47181, UseTls: false) { Events = second }, CancellationToken.None);

        var expected = new[] { "Hostname foo.example was found in DNS cache", TryingLoopback };
        CollectionAssert.AreEqual(expected, first.Info);
        CollectionAssert.AreEqual(expected, second.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_WithAConnectToMapping_KeysTheDnsCacheOnTheMappedHostAndPort()
    {
        // curl -s -v --connect-to 127.0.0.1:47182:127.0.0.1:47181 http://127.0.0.1:47181/a http://127.0.0.1:47182/b
        // -> * Hostname 127.0.0.1 was found in DNS cache before the second Trying 127.0.0.1:47181;
        // curl --connect-to foo.example:47181:localhost:47181 ... names localhost, the mapped host.
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, new FakeTlsProvider(), new ManualTimeProvider(),
            connectToMappings: new ConnectToMappings(["foo.example:47181:127.0.0.1:47181"]));
        var first = new RecordingTransferEvents();
        var second = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 47181, UseTls: false) { Events = first }, CancellationToken.None);
        await connector.ConnectAsync(new ConnectTarget("foo.example", 47181, UseTls: false) { Events = second }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { TryingLoopback }, first.Info);
        CollectionAssert.AreEqual(new[] { "Hostname 127.0.0.1 was found in DNS cache", TryingLoopback }, second.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxyAlreadyResolved_ReportsTheProxyHostFoundInDnsCache()
    {
        // curl -s -v -x http://127.0.0.1:47181 http://foo.example/a http://foo.example/b ->
        // * Hostname 127.0.0.1 was found in DNS cache before the second Trying: the proxy's host.
        var dialer = new FakeTcpDialer { DialOutcome = _ => throw new SocketException((int)SocketError.ConnectionRefused) };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());
        var target = new ConnectTarget("foo.example", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) };
        var second = new RecordingTransferEvents();

        await connector.ConnectAsync(target, CancellationToken.None);
        await connector.ConnectAsync(target with { Events = second }, CancellationToken.None);

        Assert.AreEqual("Hostname proxy.example was found in DNS cache", second.Info[0]);
    }
}
