using System.Net;
using System.Net.Sockets;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins <c>-4</c> and <c>-6</c> in <see cref="TcpConnector" />: a host or proxy name is dialled
/// at the chosen family's addresses only, one with none fails as not resolved, and an IP address
/// literal is dialled as written (measured on curl 8.21.0, Schannel, 2026-09-28, BL-500).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_UnderIPv4ToANameWithBothFamilies_ReportsAndDialsOnlyItsIPv4Addresses()
    {
        // curl asks the system resolver for the one family, so its lines name no other:
        // curl -4 -v http://google.com:47500/ -> * IPv6: (none) / * IPv4: 172.217.75.102, ...
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.IPv6Loopback, Loopback), dialer, AddressFamily.InterNetwork);
        var events = new RecordingTransferEvents();

        var result = await connector.ConnectAsync(new ConnectTarget("dual.example", 47500, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 47500) }, dialer.DialedEndPoints);
        CollectionAssert.AreEqual(
            new[] { "Host dual.example:47500 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", "  Trying 127.0.0.1:47500..." },
            events.Info);
    }

    [TestMethod]
    [DataRow("localhost", AddressFamily.InterNetworkV6, "  Trying [::1]:47500...", DisplayName = "-6 localhost")]
    [DataRow("localhost", AddressFamily.InterNetwork, "  Trying 127.0.0.1:47500...", DisplayName = "-4 localhost")]
    [DataRow("app.LOCALHOST", AddressFamily.InterNetwork, "  Trying 127.0.0.1:47500...", DisplayName = "-4 a name under .localhost")]
    public async Task ConnectAsync_UnderOneFamilyToLocalhost_ReportsBothFamiliesAndDialsOnlyTheChosenOne(string host, AddressFamily family, string trying)
    {
        // curl -6 -v http://localhost:47500/ -> * Host localhost:47500 was resolved. / * IPv6: ::1 /
        // * IPv4: 127.0.0.1 / *   Trying [::1]:47500... and no other Trying; -4 tries 127.0.0.1 only.
        var dialer = new FakeTcpDialer();
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.IPv6Loopback, Loopback), dialer, family);
        var events = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget(host, 47500, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.HasCount(1, dialer.DialedEndPoints);
        Assert.AreEqual(family, dialer.DialedEndPoints[0].AddressFamily);
        CollectionAssert.AreEqual(
            new[] { $"Host {host}:47500 was resolved.", "IPv6: ::1", "IPv4: 127.0.0.1", trying },
            events.Info.Take(4).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_UnderIPv6ToANameWithOnlyIPv4Addresses_FailsWithCouldntResolveHostAndDialsNothing()
    {
        // curl -6 -v http://github.com:47500/ -> curl: (6) Could not resolve host: github.com
        var resolver = new FakeDnsResolver(Loopback);
        var dialer = new FakeTcpDialer();
        var connector = CreateConnector(resolver, dialer, AddressFamily.InterNetworkV6);
        var events = new RecordingTransferEvents();

        var first = await connector.ConnectAsync(new ConnectTarget("github.com", 47500, UseTls: false) { Events = events }, CancellationToken.None);
        await connector.ConnectAsync(new ConnectTarget("github.com", 47500, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, first.ExitCode);
        Assert.AreEqual("Could not resolve host: github.com", first.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
        CollectionAssert.AreEqual(
            new[] { "Could not resolve host: github.com", "Could not resolve host: github.com", "Could not resolve: github.com:47500" },
            events.Info.ToArray());
        CollectionAssert.AreEqual(new[] { "github.com", "github.com" }, resolver.ResolvedHosts);
    }

    [TestMethod]
    [DataRow("::1", AddressFamily.InterNetwork, DisplayName = "-4 http://[::1]:47500/")]
    [DataRow("[::1]", AddressFamily.InterNetwork, DisplayName = "-4 bracketed")]
    [DataRow("127.0.0.1", AddressFamily.InterNetworkV6, DisplayName = "-6 http://127.0.0.1:47500/")]
    public async Task ConnectAsync_UnderTheOtherFamilyToAnAddressLiteral_DialsTheLiteral(string host, AddressFamily family)
    {
        // curl -4 -v http://[::1]:47500/ -> *   Trying [::1]:47500... (exit 7, refused);
        // curl -6 -v http://127.0.0.1:47500/ -> *   Trying 127.0.0.1:47500... and exit 0.
        var literal = IPAddress.Parse(host);
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(literal), dialer, family);

        var result = await connector.ConnectAsync(new ConnectTarget(host, 47500, UseTls: false), CancellationToken.None);

        Assert.IsNotNull(result.Connection);
        CollectionAssert.AreEqual(new[] { new IPEndPoint(literal, 47500) }, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_UnderIPv6WithAResolveEntryOfOnlyIPv4_ReportsANegativeDnsEntryAndFailsWithCouldntResolveHost()
    {
        // curl -6 -v --resolve foo:47500:127.0.0.1 http://foo:47500/ ->
        // * Added foo:47500:127.0.0.1 to DNS cache / * Negative DNS entry / * Could not resolve host: foo /
        // * Could not resolve: foo:47500 / * Could not resolve: foo (measured, BL-1181 Notes)
        var dialer = new FakeTcpDialer();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["foo:47500:127.0.0.1"]),
            addressFamily: AddressFamily.InterNetworkV6);
        var events = new RecordingTransferEvents();

        var result = await connector.ConnectAsync(new ConnectTarget("foo", 47500, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, result.ExitCode);
        Assert.AreEqual("Could not resolve host: foo", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
        CollectionAssert.AreEqual(
            new[]
            {
                "Added foo:47500:127.0.0.1 to DNS cache",
                "Negative DNS entry",
                "Could not resolve host: foo",
                "Could not resolve: foo:47500",
                "Could not resolve: foo",
            },
            events.Info);
    }

    [TestMethod]
    public async Task ConnectAsync_UnderIPv6WithAResolveEntryOfBothFamilies_ReportsBothAndDialsOnlyTheIPv6Address()
    {
        // curl -6 -v --resolve foo:47500:127.0.0.1,[::1] http://foo:47500/ -> * Hostname foo was found
        // in DNS cache / * Host foo:47500 was resolved. / * IPv6: ::1 / * IPv4: 127.0.0.1 / *   Trying [::1]:47500...
        var dialer = new FakeTcpDialer();
        var connector = new TcpConnector(
            new FakeDnsResolver(), dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["foo:47500:127.0.0.1,[::1]"]),
            addressFamily: AddressFamily.InterNetworkV6);
        var events = new RecordingTransferEvents();

        await connector.ConnectAsync(new ConnectTarget("foo", 47500, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { new IPEndPoint(IPAddress.IPv6Loopback, 47500) }, dialer.DialedEndPoints);
        CollectionAssert.AreEqual(
            new[]
            {
                "Added foo:47500:127.0.0.1,[::1] to DNS cache", "Hostname foo was found in DNS cache", "Host foo:47500 was resolved.",
                "IPv6: ::1", "IPv4: 127.0.0.1", "  Trying [::1]:47500...",
            },
            events.Info.Take(6).ToArray());
    }

    [TestMethod]
    public async Task ConnectAsync_UnderIPv6ThroughAProxyNameWithOnlyIPv4_FailsWithCouldntResolveProxy()
    {
        // curl -6 -v -x http://bar:47500 --resolve bar:47500:127.0.0.1 http://example.com/ ->
        // curl: (5) Could not resolve proxy: bar
        var dialer = new FakeTcpDialer();
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, AddressFamily.InterNetworkV6);
        var target = new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "bar", 47500, null) };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveProxy, result.ExitCode);
        Assert.AreEqual("Could not resolve proxy: bar", result.ErrorMessage);
        Assert.IsEmpty(dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_UnderIPv6ThroughAnIPv4ProxyLiteral_DialsTheProxy()
    {
        // curl -6 -v -x http://127.0.0.1:47500 http://example.com/ -> *   Trying 127.0.0.1:47500... and exit 0.
        var dialer = new FakeTcpDialer();
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, AddressFamily.InterNetworkV6);
        var target = new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 47500, null) };

        await connector.ConnectAsync(target, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { new IPEndPoint(Loopback, 47500) }, dialer.DialedEndPoints);
    }

    private static TcpConnector CreateConnector(FakeDnsResolver resolver, FakeTcpDialer dialer, AddressFamily addressFamily) =>
        new(resolver, dialer, new FakeTlsProvider(), new ManualTimeProvider(), addressFamily: addressFamily);
}
