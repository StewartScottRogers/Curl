using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Pins what <see cref="TcpConnector" /> measures on a success: each
/// <see cref="ConnectTimings" /> timestamp from the injected <see cref="TimeProvider" />,
/// the local end point the dialer reports and the remote end point of the connection.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private static readonly IPEndPoint DialerLocalEndPoint = new(IPAddress.Parse("192.0.2.99"), 54321);

    [TestMethod]
    public async Task ConnectAsync_WithoutUseTls_RecordsStartResolveAndConnectAndBothEndPoints()
    {
        var dialer = new FakeTcpDialer
        {
            DialOutcome = endPoint => new FakeConnection { RemoteEndPoint = endPoint },
            LocalEndPoint = DialerLocalEndPoint,
        };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new SteppingTimeProvider(100));

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 8080, UseTls: false), CancellationToken.None);

        Assert.AreEqual(new ConnectTimings(100, 110, 120, null), result.Timings);
        Assert.AreSame(DialerLocalEndPoint, result.LocalEndPoint);
        Assert.AreEqual(new IPEndPoint(Loopback, 8080), result.Connection!.RemoteEndPoint);
        Assert.AreEqual(0, result.ProxyConnectResponseCode);
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_WhenTheProviderRecordsNoTimings_TakesTheHandshakeTimeWhenItReturns()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection(), LocalEndPoint = DialerLocalEndPoint };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new SteppingTimeProvider(100));

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true), CancellationToken.None);

        Assert.AreEqual(new ConnectTimings(100, 110, 120, 130), result.Timings);
        Assert.AreSame(DialerLocalEndPoint, result.LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_KeepsOnlyTheHandshakeCompletionTheProviderRecorded()
    {
        var tlsProvider = new FakeTlsProvider { TimingsToReturn = new ConnectTimings(1, null, 2, 125) };
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection(), LocalEndPoint = DialerLocalEndPoint };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, tlsProvider, new SteppingTimeProvider(100));

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true), CancellationToken.None);

        Assert.AreSame(tlsProvider.SecuredConnection, result.Connection);
        Assert.AreEqual(new ConnectTimings(100, 110, 120, 125), result.Timings);
        Assert.AreSame(DialerLocalEndPoint, result.LocalEndPoint);
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTls_CarriesThePeerCertificatesTheProviderReported()
    {
        ReadOnlyMemory<byte>[] certificates = [new byte[] { 0x30, 0x01 }, new byte[] { 0x30, 0x02 }];
        var tlsProvider = new FakeTlsProvider { PeerCertificatesToReturn = certificates };
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, tlsProvider, new SteppingTimeProvider(100));

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true), CancellationToken.None);

        Assert.AreSame(certificates, result.PeerCertificates);
    }

    [TestMethod]
    public async Task ConnectAsync_WithoutUseTls_ReportsNoPeerCertificates()
    {
        var dialer = new FakeTcpDialer { DialOutcome = _ => new FakeConnection() };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new SteppingTimeProvider(100));

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 80, UseTls: false), CancellationToken.None);

        Assert.IsEmpty(result.PeerCertificates);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAProxy_RecordsConnectWhenTheTunnelIsOpenAndTheProxyConnectionsEndPoints()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var dialer = new FakeTcpDialer { DialOutcome = _ => proxyConnection, LocalEndPoint = DialerLocalEndPoint };
        var timeProvider = new SteppingTimeProvider(100);
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), timeProvider);

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 80, UseTls: false) { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) },
            CancellationToken.None);

        Assert.AreEqual(new ConnectTimings(100, 110, 120, null), result.Timings);
        Assert.AreSame(DialerLocalEndPoint, result.LocalEndPoint);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
    }

    [TestMethod]
    public async Task ConnectAsync_WithUseTlsThroughAProxy_RecordsTheHandshakeAfterTheTunnel()
    {
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var dialer = new FakeTcpDialer { DialOutcome = _ => proxyConnection, LocalEndPoint = DialerLocalEndPoint };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), new SteppingTimeProvider(100));

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 443, UseTls: true) { Proxy = new ProxyEndpoint(ProxyKind.Http, "proxy.example", 3128, null) },
            CancellationToken.None);

        Assert.AreEqual(new ConnectTimings(100, 110, 120, 130), result.Timings);
        Assert.AreSame(DialerLocalEndPoint, result.LocalEndPoint);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
    }
}
