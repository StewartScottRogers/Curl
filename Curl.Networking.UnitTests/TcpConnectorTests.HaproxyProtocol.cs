using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="TcpConnector" /> writes the <see cref="HaproxyProtocolHeader" /> line first on a
/// new connection, before the TLS handshake and after any proxy tunnel, from the socket's own ends, as
/// curl 8.21.0 does (measured, BL-616 Notes: <c>--haproxy-protocol -k https://127.0.0.1:48637/</c> sent
/// <c>PROXY TCP4 127.0.0.1 127.0.0.1 55520 48637\r\n</c> and then the ClientHello, byte 22).
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WithHaproxyProtocolOverIPv4_WritesTheLineBeforeTheTlsHandshake()
    {
        var connection = new ScriptedConnection([]);
        var tls = new PlaintextSnapshottingTlsProvider();
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection, LocalEndPoint = new IPEndPoint(Loopback, 55520) };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, tls, new ManualTimeProvider(), haproxyProtocol: new HaproxyProtocolHeader(null));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 48637, UseTls: true));

        Diagnostics.Assert("connection returned", true, result.Connection is not null);
        Assert.IsNotNull(result.Connection);
        Assert.AreEqual("PROXY TCP4 127.0.0.1 127.0.0.1 55520 48637\r\n", tls.WrittenBeforeHandshake);
    }

    [TestMethod]
    public async Task ConnectAsync_WithHaproxyProtocolOverIPv6_WritesATcp6Line()
    {
        var connection = new ScriptedConnection([]);
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection, LocalEndPoint = new IPEndPoint(IPAddress.IPv6Loopback, 52934) };
        var connector = new TcpConnector(
            new FakeDnsResolver(IPAddress.IPv6Loopback), dialer, new FakeTlsProvider(), new ManualTimeProvider(), haproxyProtocol: new HaproxyProtocolHeader(null));

        await ConnectLoggedAsync(connector, new ConnectTarget("::1", 48617, UseTls: false));

        Diagnostics.Assert("written line", "PROXY TCP6 ::1 ::1 52934 48617\r\n", Encoding.ASCII.GetString(connection.Written.ToArray()));
        Assert.AreEqual("PROXY TCP6 ::1 ::1 52934 48617\r\n", Encoding.ASCII.GetString(connection.Written.ToArray()));
    }

    [TestMethod]
    public async Task ConnectAsync_WithAHaproxyClientIp_WritesItInPlaceOfBothAddressesBeforeTheTlsHandshake()
    {
        var connection = new ScriptedConnection([]);
        var tls = new PlaintextSnapshottingTlsProvider();
        var dialer = new FakeTcpDialer { DialOutcome = _ => connection, LocalEndPoint = new IPEndPoint(Loopback, 52946) };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, tls, new ManualTimeProvider(), haproxyProtocol: new HaproxyProtocolHeader("1.2.3.4"));

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 48618, UseTls: true));

        Diagnostics.Assert("written before handshake", "PROXY TCP4 1.2.3.4 1.2.3.4 52946 48618\r\n", tls.WrittenBeforeHandshake);
        Assert.AreEqual("PROXY TCP4 1.2.3.4 1.2.3.4 52946 48618\r\n", tls.WrittenBeforeHandshake);
    }

    [TestMethod]
    public async Task ConnectAsync_WithHaproxyProtocolThroughAnHttpProxy_WritesTheLineAfterTheTunnelFromTheProxyConnectionsEnds()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var tls = new PlaintextSnapshottingTlsProvider();
        var dialer = new FakeTcpDialer { DialOutcome = _ => proxyConnection, LocalEndPoint = new IPEndPoint(Loopback, 50000) };
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress), dialer, tls, new ManualTimeProvider(), haproxyProtocol: new HaproxyProtocolHeader(null));

        await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpProxy });

        Diagnostics.Assert("written before handshake ends with", true, tls.WrittenBeforeHandshake!.EndsWith("\r\n\r\nPROXY TCP4 127.0.0.1 192.0.2.10 50000 3128\r\n", StringComparison.Ordinal));
        Assert.EndsWith("\r\n\r\nPROXY TCP4 127.0.0.1 192.0.2.10 50000 3128\r\n", tls.WrittenBeforeHandshake);
        Assert.StartsWith("CONNECT example.com:443 HTTP/1.1\r\n", tls.WrittenBeforeHandshake);
    }

    [TestMethod]
    public async Task ConnectAsync_WithHaproxyProtocolOverAUnixSocket_WritesProxyUnknown()
    {
        var connection = new ScriptedConnection([]);
        var dialer = new FakeTcpDialer { UnixSocketDialOutcome = _ => connection };
        var connector = new TcpConnector(
            new FakeDnsResolver(), dialer, new FakeTlsProvider(), new ManualTimeProvider(),
            unixSocket: new UnixSocketAddress("/run/app.sock", IsAbstract: false), haproxyProtocol: new HaproxyProtocolHeader(null));

        await ConnectLoggedAsync(connector, new ConnectTarget("localhost", 80, UseTls: false));

        Diagnostics.Assert("written line", "PROXY UNKNOWN\r\n", Encoding.ASCII.GetString(connection.Written.ToArray()));
        Assert.AreEqual("PROXY UNKNOWN\r\n", Encoding.ASCII.GetString(connection.Written.ToArray()));
    }

    [TestMethod]
    public async Task ConnectAsync_WithoutHaproxyProtocol_WritesNothing()
    {
        var connection = new ScriptedConnection([]);
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => connection }, new FakeTlsProvider(), new ManualTimeProvider());

        await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 80, UseTls: false));

        Diagnostics.Assert("written byte count", 0, connection.Written.Count);
        Assert.IsEmpty(connection.Written);
        Assert.IsNull(connector.HaproxyProtocol);
    }

    [TestMethod]
    public void HaproxyProtocol_IsTheHeaderGiven()
    {
        var header = new HaproxyProtocolHeader("1.2.3.4");

        Diagnostics.Arrange("haproxy client ip", "1.2.3.4");
        var given = new TcpConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(), haproxyProtocol: header).HaproxyProtocol;
        Diagnostics.Act("header read back", given is not null);
        Diagnostics.Assert("same header", true, ReferenceEquals(header, given));
        Assert.AreSame(header, new TcpConnector(new FakeDnsResolver(), new FakeTcpDialer(), new FakeTlsProvider(), new ManualTimeProvider(), haproxyProtocol: header).HaproxyProtocol);
    }

    /// <summary>
    /// A TLS provider that keeps, as ASCII, every byte written on the plaintext connection by the
    /// time the handshake starts, and succeeds.
    /// </summary>
    private sealed class PlaintextSnapshottingTlsProvider : ITlsProvider
    {
        public string? WrittenBeforeHandshake { get; private set; }

        public ValueTask<ConnectResult> AuthenticateAsClientAsync(IConnection plaintext, string targetHost, CancellationToken cancellationToken)
        {
            WrittenBeforeHandshake = Encoding.ASCII.GetString(((ScriptedConnection)plaintext).Written.ToArray());
            return ValueTask.FromResult(ConnectResult.Connected(new FakeConnection { IsSecure = true }));
        }
    }
}
