using System.Net.Sockets;
using System.Security.Cryptography;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// Drives the second dial <see cref="TcpConnector" /> makes under <c>-k</c> when .NET on Linux
/// throws from the chain build <see cref="System.Net.Security.SslStream" /> makes inside its
/// handshake, for OpenSSL's error 1: the redialled handshake runs on the hand-built client
/// (ADR-1459, BL-6066). The first handshake step is replaced by one that throws and the hand-built
/// client by a fake, so every platform runs it.
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string UnspecifiedChainMessage = "OpenSSL mapping function MapOpenSsl30Code reported unhandled error code '1'.";

    [TestMethod]
    public async Task ConnectAsync_ChainBuildThrowingForOpenSslError1UnderInsecure_DialsAgainAndConnectsOnTheHandBuiltRoute()
    {
        var events = new RecordingTransferEvents();
        var handBuilt = HandBuiltRouteReportingVerifyResult1();
        var dialer = FakeDialer();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, ThrowingChainProvider(insecure: true, handBuilt), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 443, UseTls: true) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreSame(handBuilt.SecuredConnection, result.Connection);
        Assert.HasCount(2, dialer.DialedEndPoints);
        Assert.AreEqual(1, handBuilt.HandshakeCount);
        Assert.AreEqual(0, result.ConnectionNumber);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.IsFalse(handshake.Failed);
        Assert.AreEqual(1L, handshake.CertificateVerifyResult);
        CollectionAssert.AreEqual(new[] { (1L, false) }, events.VerifyResults);
    }

    [TestMethod]
    public async Task ConnectAsync_ARedialledDirectConnectUnderInsecure_ReportsEachLineBeforeTheHandshakeOnce()
    {
        var events = new RecordingTransferEvents();
        var handBuilt = HandBuiltRouteReportingVerifyResult1();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), FakeDialer(), ThrowingChainProvider(insecure: true, handBuilt), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("example.com", 443, UseTls: true) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                "Host example.com:443 was resolved.",
                "IPv6: (none)",
                "IPv4: 127.0.0.1",
                "  Trying 127.0.0.1:443...",
            },
            events.Info);
        Assert.HasCount(2, events.TlsEvents);
        Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents[0]);
        Assert.IsInstanceOfType<TlsHandshakeEvent>(events.TlsEvents[1]);
        Assert.HasCount(1, events.Opened);
    }

    [TestMethod]
    public async Task ConnectAsync_ARedialThatCannotDial_ReportsTheLinesOfTheFailedDial()
    {
        var events = new RecordingTransferEvents();
        var dials = 0;
        var dialer = new FakeTcpDialer
        {
            DialOutcome = _ => ++dials == 1 ? new FakeConnection() : throw new SocketException((int)SocketError.ConnectionRefused),
        };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, ThrowingChainProvider(insecure: true, HandBuiltRouteReportingVerifyResult1()), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 443, UseTls: true) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual(2, events.Info.Count(line => line == "  Trying 127.0.0.1:443..."));
        Assert.AreEqual(result.ErrorMessage, events.Info[^1]);
    }

    [TestMethod]
    public async Task ConnectAsync_AProxysAndThenTheOriginsHandshakeRedialled_ReportsEachHandshakesTrustOnce()
    {
        var events = new RecordingTransferEvents();
        // The tunnel is opened twice over the proxy's secured connection, by the second and third attempts.
        var proxyHandBuilt = HandBuiltRouteReportingVerifyResult1(
            new ScriptedConnection("HTTP/1.1 200 OK\r\n\r\nHTTP/1.1 200 OK\r\n\r\n"u8.ToArray()));
        var originHandBuilt = HandBuiltRouteReportingVerifyResult1();
        var dialer = FakeDialer();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            dialer,
            ThrowingChainProvider(insecure: true, originHandBuilt),
            new ManualTimeProvider(),
            proxyTlsProvider: ThrowingChainProvider(insecure: true, proxyHandBuilt));
        var target = new ConnectTarget("example.com", 443, UseTls: true)
        {
            Proxy = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null),
            Events = events,
        };

        var result = await ConnectLoggedAsync(connector, target);

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(3, dialer.DialedEndPoints);
        Assert.AreEqual(2, events.TlsEvents.OfType<TlsTrustEvent>().Count());
        CollectionAssert.AreEqual(new[] { true, true }, proxyHandBuilt.ReceivedIsProxy);
        Assert.AreEqual(1, originHandBuilt.HandshakeCount);
        Assert.AreEqual(1, events.Info.Count(line => line == "  Trying 127.0.0.1:443..."));
        Assert.AreEqual(1, events.Transcript.Count(line => line.StartsWith("> CONNECT ", StringComparison.Ordinal)));
    }

    [TestMethod]
    public async Task ConnectAsync_ASecondConnectToAHostAlreadyRedialled_DialsOnceOnTheHandBuiltRoute()
    {
        var handBuilt = HandBuiltRouteReportingVerifyResult1();
        var dialer = FakeDialer();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, ThrowingChainProvider(insecure: true, handBuilt), new ManualTimeProvider());
        var target = new ConnectTarget("127.0.0.1", 443, UseTls: true);
        await ConnectLoggedAsync(connector, target);

        var result = await ConnectLoggedAsync(connector, target);

        Diagnostics.Assert("dials", 3, dialer.DialedEndPoints.Count);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.HasCount(3, dialer.DialedEndPoints);
        Assert.AreEqual(2, handBuilt.HandshakeCount);
    }

    [TestMethod]
    public async Task ConnectAsync_ChainBuildThrowingForOpenSslError1WithoutInsecure_IsExit60WithNoSecondDial()
    {
        var events = new RecordingTransferEvents();
        var handBuilt = HandBuiltRouteReportingVerifyResult1();
        var dialer = FakeDialer();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), dialer, ThrowingChainProvider(insecure: false, handBuilt), new ManualTimeProvider());

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 443, UseTls: true) { Events = events });

        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: unspecified certificate verification error (1)", result.ErrorMessage);
        Assert.HasCount(1, dialer.DialedEndPoints);
        Assert.AreEqual(0, handBuilt.HandshakeCount);
        CollectionAssert.AreEqual(new[] { (1L, false) }, events.VerifyResults);
    }

    [TestMethod]
    public async Task ConnectAsync_AForwardProxysChainBuildThrowingUnderProxyInsecure_DialsTheProxyAgainOnTheHandBuiltRoute()
    {
        var handBuilt = HandBuiltRouteReportingVerifyResult1();
        var origin = new FakeTlsProvider();
        var dialer = FakeDialer();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            dialer,
            origin,
            new ManualTimeProvider(),
            proxyTlsProvider: ThrowingChainProvider(insecure: true, handBuilt));

        var result = await ConnectLoggedAsync(
            connector, new ConnectTarget("127.0.0.1", 443, UseTls: true) { IsForwardProxy = true });

        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreSame(handBuilt.SecuredConnection, result.Connection);
        Assert.HasCount(2, dialer.DialedEndPoints);
        CollectionAssert.AreEqual(new[] { true }, handBuilt.ReceivedIsProxy);
        Assert.AreEqual(0, origin.HandshakeCount);
    }

    [TestMethod]
    public async Task ConnectMultiplexedAsync_AnHttpsProxysChainBuildThrowingUnderProxyInsecure_DialsTheProxyAgainOnTheHandBuiltRoute()
    {
        var handBuilt = new FakeTlsProvider
        {
            Route = TlsClientRoute.HandBuilt,
            FailureToReturn = ConnectResult.Failed(CurlExitCode.SslConnectError, "the redialled handshake"),
        };
        var dialer = FakeDialer();
        var time = new ManualTimeProvider();
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            dialer,
            new FakeTlsProvider(),
            time,
            proxyTlsProvider: ThrowingChainProvider(insecure: true, handBuilt),
            quicDialer: new QuicDialer(new QuicServerChannelOpener(), new TlsClientOptions(Insecure: true), true, time, SystemTlsRandomSource.Instance));
        var target = new ConnectTarget("example.com", 443, UseTls: true)
        {
            Proxy = new ProxyEndpoint(ProxyKind.Https, "proxy.example", 443, null),
        };

        var result = await connector.ConnectMultiplexedAsync(target, CancellationToken.None);

        Diagnostics.Assert("error message", "the redialled handshake", result.ErrorMessage);
        Assert.AreEqual("the redialled handshake", result.ErrorMessage);
        Assert.HasCount(2, dialer.DialedEndPoints);
    }

    [TestMethod]
    public async Task ConnectAsync_ARedialledHandshakeStalling_TimesOutAtTheFirstDialsConnectLimit()
    {
        var time = new ManualTimeProvider();
        var handBuilt = new StallingTlsProvider { OnStalled = () => time.Advance(6000) };
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild: false)
        {
            HandBuiltRedialProvider = handBuilt,
            AuthenticateSslStreamAsClientAsync = (_, _, _) =>
            {
                time.Advance(4000);
                return Task.FromException(new CryptographicException(UnspecifiedChainMessage));
            },
        };
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback), FakeDialer(), provider, time, connectTimeout: TimeSpan.FromSeconds(10));

        var result = await ConnectLoggedAsync(connector, new ConnectTarget("127.0.0.1", 443, UseTls: true));

        Diagnostics.Assert("exit code", CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual(CurlExitCode.OperationTimedOut, result.ExitCode);
        Assert.AreEqual("Connection timed out after 10000 milliseconds", result.ErrorMessage);
    }

    private static FakeTcpDialer FakeDialer() => new() { DialOutcome = _ => new FakeConnection() };

    // The OpenSSL build's provider, its SslStream handshake throwing as .NET's chain build does,
    // redialling on the given stand-in for the hand-built client.
    private static SslStreamTlsProvider ThrowingChainProvider(bool insecure, ITlsProvider handBuilt) =>
        new(new TlsClientOptions(Insecure: insecure), matchesSchannelBuild: false)
        {
            HandBuiltRedialProvider = handBuilt,
            AuthenticateSslStreamAsClientAsync = (_, _, _) =>
                Task.FromException(new CryptographicException(UnspecifiedChainMessage)),
        };

    private static FakeTlsProvider HandBuiltRouteReportingVerifyResult1(IConnection? secured = null) => new()
    {
        Route = TlsClientRoute.HandBuilt,
        ReportsTrust = true,
        ConnectionToReturn = secured,
        HandshakeToReport = new TlsHandshakeEvent
        {
            ProtocolVersion = System.Security.Authentication.SslProtocols.Tls13,
            CipherSuite = System.Net.Security.TlsCipherSuite.TLS_AES_128_GCM_SHA256,
            OfferedApplicationProtocols = [],
            NegotiatedApplicationProtocol = null,
            ServerCertificate = null,
            CertificateVerified = false,
            CertificateVerifyResult = 1,
        },
    };
}
