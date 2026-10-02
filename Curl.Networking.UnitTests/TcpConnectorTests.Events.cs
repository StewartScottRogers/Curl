using System.Net;
using System.Net.Sockets;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives the <c>-v</c> events <see cref="TcpConnector" /> reports on the target's
/// <see cref="ConnectTarget.Events" />: <c>Trying</c>, the connection opened, and curl
/// 8.21.0's connect failure lines (measured 2026-09-27, BL-408).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WhenTheDialSucceeds_ReportsTryingThenTheConnectionOpened()
    {
        // curl -v http://127.0.0.1:18441/ -> *   Trying 127.0.0.1:18441...
        // * Established connection to 127.0.0.1 (127.0.0.1 port 18441) from 127.0.0.1 port 55116
        var events = new RecordingTransferEvents();
        var localEndPoint = new IPEndPoint(Loopback, 55116);
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection(), LocalEndPoint = localEndPoint };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 18441, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "  Trying 127.0.0.1:18441..." }, events.Info);
        Assert.HasCount(1, events.Opened);
        Assert.AreEqual(
            new ConnectionOpenedEvent
            {
                HostName = "127.0.0.1",
                RemoteEndPoint = new IPEndPoint(Loopback, 18441),
                LocalEndPoint = localEndPoint,
                ConnectionNumber = 0,
            },
            events.Opened[0]);
        Assert.AreEqual(0, result.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectAsync_ForASecondConnection_NumbersItOne()
    {
        var events = new RecordingTransferEvents();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());
        var target = new ConnectTarget("localhost", 80, UseTls: false) { Events = events };

        await connector.ConnectAsync(target, CancellationToken.None);
        var second = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(1, second.ConnectionNumber);
        Assert.AreEqual(1, events.Opened[1].ConnectionNumber);
        Assert.AreEqual("localhost", events.Opened[1].HostName);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheDialIsRefused_ReportsTryingAndCurlsFailureLines()
    {
        // curl -v -s http://127.0.0.1:1/ -> *   Trying 127.0.0.1:1...
        // * connect to 127.0.0.1 port 1 from 0.0.0.0 port 56585 failed: Connection refused
        // * Failed to connect to 127.0.0.1:1 after 2025 ms: Could not connect to server
        var events = new RecordingTransferEvents();
        var timeProvider = new ManualTimeProvider();
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ =>
            {
                timeProvider.Advance(2025);
                throw new SocketException((int)SocketError.ConnectionRefused);
            },
        };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), timeProvider);

        var result = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[]
            {
                "  Trying 127.0.0.1:1...",
                "connect to 127.0.0.1 port 1 from 0.0.0.0 port 0 failed: Connection refused",
                "Failed to connect to 127.0.0.1:1 after 2025 ms: Could not connect to server",
            },
            events.Info);
        Assert.IsEmpty(events.Opened);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheDialIsRefusedAfterOneConnection_NumbersTheRefusedConnectOne()
    {
        var dials = 0;
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ => dials++ == 0
                ? new FakeConnection()
                : throw new SocketException((int)SocketError.ConnectionRefused),
        };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider());
        var target = new ConnectTarget("127.0.0.1", 1, UseTls: false);

        var first = await connector.ConnectAsync(target, CancellationToken.None);
        var refused = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual(1L, refused.ConnectionNumber);
        Assert.IsTrue(refused.IsConnectionRefused);
        Assert.IsNotNull(refused.Timings);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenARefusedConnectFollowsAnUnresolvedHost_NumbersThemZeroAndOne()
    {
        // curl -sv http://nohost.invalid/ http://127.0.0.1:1/ -> * closing connection #0 after
        // Could not resolve host, * closing connection #1 after the refused connect (measured
        // 2026-09-27, curl 8.21.0, ADR-0109).
        var connector = new TcpConnector(new LoopbackOnlyDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider());

        var unresolved = await connector.ConnectAsync(new ConnectTarget("nohost.invalid", 80, UseTls: false), CancellationToken.None);
        var refused = await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 1, UseTls: false), CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntResolveHost, unresolved.ExitCode);
        Assert.AreEqual("Could not resolve host: nohost.invalid", unresolved.ErrorMessage);
        Assert.AreEqual(0L, unresolved.ConnectionNumber);
        Assert.AreEqual(CurlExitCode.CouldntConnect, refused.ExitCode);
        Assert.AreEqual(1L, refused.ConnectionNumber);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAResolveEntryDoesNotParse_TakesNoConnectionNumber()
    {
        // curl fails the option before it creates a connection, so there is nothing to number.
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            new FakeTcpDialer { DialOutcome = _ => new FakeConnection() },
            new FakeTlsProvider(),
            new ManualTimeProvider(),
            resolveOverrides: ResolveOverrides.Parse(["garbage"]));
        var target = new ConnectTarget("a", 80, UseTls: false);

        var first = await connector.ConnectAsync(target, CancellationToken.None);
        var second = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SetoptOptionSyntax, first.ExitCode);
        Assert.AreEqual(0L, first.ConnectionNumber);
        Assert.AreEqual(0L, second.ConnectionNumber);
    }

    /// <summary>Resolves <c>127.0.0.1</c> to the loopback address and every other host to nothing.</summary>
    private sealed class LoopbackOnlyDnsResolver : IDnsResolver
    {
        public ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<IPAddress>>(host == "127.0.0.1" ? [Loopback] : []);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAnIPv6DialIsRefused_BracketsTheAddressInTryingAndNamesTheIPv6UnspecifiedAddress()
    {
        var events = new RecordingTransferEvents();
        var connector = CreateConnector(new FakeDnsResolver(IPAddress.IPv6Loopback), new FakeTcpDialer(), new FakeTlsProvider());

        await connector.ConnectAsync(new ConnectTarget("localhost", 1, UseTls: false) { Events = events }, CancellationToken.None);

        Assert.AreEqual("  Trying [::1]:1...", events.Info[3]);
        Assert.AreEqual("connect to ::1 port 1 from :: port 0 failed: Connection refused", events.Info[4]);
    }

    [TestMethod]
    public async Task ConnectAsync_OverTls_ReportsTheConnectionOpenedAfterTheHandshake()
    {
        var events = new RecordingTransferEvents();
        var tlsProvider = new FakeTlsProvider();
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, tlsProvider);

        var result = await connector.ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { Events = events }, CancellationToken.None);

        Assert.AreSame(tlsProvider.SecuredConnection, result.Connection);
        Assert.HasCount(1, events.Opened);
        Assert.AreEqual(new IPEndPoint(Loopback, 443), events.Opened[0].RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHandshakeFails_ReportsNoConnectionOpened()
    {
        var events = new RecordingTransferEvents();
        var tlsProvider = new FakeTlsProvider { FailureToReturn = ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake failed") };
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), dialer, tlsProvider);

        await connector.ConnectAsync(new ConnectTarget("localhost", 443, UseTls: true) { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Host localhost:443 was resolved.", "IPv6: (none)", "IPv4: 127.0.0.1", "  Trying 127.0.0.1:443..." }, events.Info);
        Assert.IsEmpty(events.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxy_ReportsTryingTheProxyAndTheConnectionOpenedToIt()
    {
        var events = new RecordingTransferEvents();
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var dialer = new FakeTcpDialer { DialOutcome = _ => proxyConnection };
        var connector = CreateConnector(new FakeDnsResolver(ProxyAddress), dialer, new FakeTlsProvider());

        await connector.ConnectAsync(PlainTarget with { Events = events }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "Host proxy.example:3128 was resolved.", "IPv6: (none)", "IPv4: 192.0.2.10", "  Trying 192.0.2.10:3128...", "Establishing HTTP proxy tunnel to example.com:80" }, events.Info);
        Assert.AreEqual("proxy.example", events.Opened[0].HostName);
        Assert.AreEqual(new IPEndPoint(ProxyAddress, 3128), events.Opened[0].RemoteEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProxyRefuses_ReportsTheFailureMessage()
    {
        var events = new RecordingTransferEvents();
        var connector = CreateConnector(new FakeDnsResolver(ProxyAddress), new FakeTcpDialer(), new FakeTlsProvider());

        await connector.ConnectAsync(PlainTarget with { Events = events }, CancellationToken.None);

        Assert.AreEqual(
            "Failed to connect to example.com:80 over proxy proxy.example after 0 ms: Could not connect to server",
            events.Info[^1]);
    }
}
