using System.Net;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Drives <see cref="TcpConnector" /> through an HTTPS proxy with fakes: TLS to the proxy,
/// CONNECT over it, then TLS to the target inside the tunnel, as curl 8.21.0 does
/// (measured; the commands are in BL-266's Notes).
/// </summary>
public sealed partial class TcpConnectorTests
{
    private const string EstablishedReply = "HTTP/1.1 200 Connection established\r\n\r\n";

    private static readonly ProxyEndpoint HttpsProxy = new(ProxyKind.Https, "localhost", 18405, null);

    private static readonly string[] ProxyResolvedAndTriedLines =
        ["Host localhost:18405 was resolved.", "IPv6: (none)", "IPv4: 192.0.2.10", "  Trying 192.0.2.10:18405..."];

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxy_RunsTlsToTheProxyThenConnectThenTlsToTheTarget()
    {
        // curl -s -S --proxy-insecure -x https://localhost:18405 https://example.com/ sent, inside the proxy's TLS:
        // CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n
        var dialedConnection = new ScriptedConnection([]);
        var proxyTls = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var targetTls = new FakeConnection { IsSecure = true };
        byte[] certificate = [1, 2, 3];
        var tlsProvider = new SequencedTlsProvider(
            ConnectResult.Connected(proxyTls),
            ConnectResult.Connected(targetTls, null, peerCertificates: [certificate]));
        var dialer = new FakeTcpDialer { DialOutcome = _ => dialedConnection };
        var connector = new TcpConnector(new FakeDnsResolver(ProxyAddress), dialer, tlsProvider, new ManualTimeProvider());

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpsProxy },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(targetTls, result.Connection);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
        Assert.AreEqual(dialer.LocalEndPoint, result.LocalEndPoint);
        CollectionAssert.AreEqual(certificate, result.PeerCertificates.Single().ToArray());
        CollectionAssert.AreEqual(new[] { new IPEndPoint(ProxyAddress, 18405) }, dialer.DialedEndPoints);
        CollectionAssert.AreEqual(new[] { "localhost", "example.com" }, tlsProvider.ReceivedTargetHosts);
        CollectionAssert.AreEqual(new IConnection[] { dialedConnection, proxyTls }, tlsProvider.ReceivedPlaintexts);
        Assert.IsEmpty(dialedConnection.Written);
        Assert.AreEqual(
            "CONNECT example.com:443 HTTP/1.1\r\nHost: example.com:443\r\nUser-Agent: curl/8.21.0\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString([.. proxyTls.Written]));
        Assert.AreEqual(1, proxyTls.FlushCount);
        Assert.IsFalse(proxyTls.IsDisposed);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxyWithAProxyTlsProvider_RunsTheProxyHandshakeOnItAndTheTargetsOnTheOther()
    {
        // curl -s -S -k -x https://localhost:18462 https://example.com/ against a self-signed proxy -> exit 60,
        // and --proxy-insecure in place of -k reaches CONNECT (curl 8.21.0, 2026-09-27, BL-362):
        // the proxy is verified with its own settings, never the target's.
        var proxyTls = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var targetTls = new FakeConnection { IsSecure = true };
        var proxyTlsProvider = new SequencedTlsProvider(ConnectResult.Connected(proxyTls));
        var tlsProvider = new SequencedTlsProvider(ConnectResult.Connected(targetTls));
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) },
            tlsProvider,
            new ManualTimeProvider(),
            proxyTlsProvider: proxyTlsProvider);

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpsProxy },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(targetTls, result.Connection);
        CollectionAssert.AreEqual(new[] { "localhost" }, proxyTlsProvider.ReceivedTargetHosts);
        CollectionAssert.AreEqual(new[] { "example.com" }, tlsProvider.ReceivedTargetHosts);
        CollectionAssert.AreEqual(new IConnection[] { proxyTls }, tlsProvider.ReceivedPlaintexts);
    }

    [TestMethod]
    [DataRow(true, "proxy")]
    [DataRow(false, "target")]
    public async Task ConnectAsync_WithTls_HandshakesAForwardProxyThroughTheProxyTlsProviderAndATargetThroughTheOther(
        bool isForwardProxy,
        string expectedProvider)
    {
        // curl -sS -k -x https://127.0.0.1:18441 http://example.com/ against a self-signed proxy -> exit 60,
        // and with --proxy-insecure in place of -k -> exit 0 (BL-441): a forward proxy is verified
        // with the proxy's TLS options, never the target's.
        var proxyTls = new FakeConnection { IsSecure = true };
        var targetTls = new FakeConnection { IsSecure = true };
        var proxyTlsProvider = new SequencedTlsProvider(ConnectResult.Connected(proxyTls));
        var tlsProvider = new SequencedTlsProvider(ConnectResult.Connected(targetTls));
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) },
            tlsProvider,
            new ManualTimeProvider(),
            proxyTlsProvider: proxyTlsProvider);

        var result = await connector.ConnectAsync(
            new ConnectTarget("localhost", 18441, UseTls: true) { IsForwardProxy = isForwardProxy },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        var (usedProvider, unusedProvider, secured) = expectedProvider == "proxy"
            ? (proxyTlsProvider, tlsProvider, proxyTls)
            : (tlsProvider, proxyTlsProvider, targetTls);
        Assert.AreSame(secured, result.Connection);
        CollectionAssert.AreEqual(new[] { "localhost" }, usedProvider.ReceivedTargetHosts);
        Assert.IsEmpty(unusedProvider.ReceivedTargetHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpsProxyToAnHttpTarget_ReturnsTheTunnelWithoutASecondHandshake()
    {
        // curl -s -S -o /dev/null --proxy-insecure -p -x https://localhost:18411 http://example.com/
        //   -w "appconnect=%{time_appconnect} code=%{http_connect}" -> appconnect=0.000000 code=200
        var proxyTls = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var tlsProvider = new SequencedTlsProvider(ConnectResult.Connected(proxyTls));
        var connector = CreateHttpsProxyConnector(tlsProvider);

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 80, UseTls: false) { Proxy = HttpsProxy },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.AreSame(proxyTls, result.Connection);
        Assert.AreEqual(200, result.ProxyConnectResponseCode);
        Assert.IsNull(result.Timings!.TlsHandshakeCompleted);
        Assert.IsEmpty(result.PeerCertificates);
        CollectionAssert.AreEqual(new[] { "localhost" }, tlsProvider.ReceivedTargetHosts);
        StringAssert.StartsWith(Encoding.Latin1.GetString([.. proxyTls.Written]), "CONNECT example.com:80 HTTP/1.1\r\n");
    }

    [TestMethod]
    [DataRow(CurlExitCode.SslConnectError, "Recv failure: Connection was aborted")]
    [DataRow(
        CurlExitCode.PeerFailedVerification,
        "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.")]
    public async Task ConnectAsync_WhenTheHandshakeToTheHttpsProxyFails_ReturnsTheProvidersFailureAndSendsNoConnect(
        CurlExitCode exitCode,
        string message)
    {
        // A proxy that closed at once: curl -s -S -x https://127.0.0.1:18401 http://example.com/ ->
        //   curl: (35) Recv failure: Connection was aborted
        // A self-signed proxy, -k notwithstanding: curl -s -S -k -x https://localhost:18404 https://example.com/ ->
        //   curl: (60) schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.
        var failure = ConnectResult.Failed(exitCode, message);
        var dialedConnection = new ScriptedConnection([]);
        var tlsProvider = new SequencedTlsProvider(failure);
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => dialedConnection },
            tlsProvider,
            new ManualTimeProvider());

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpsProxy },
            CancellationToken.None);

        Assert.AreEqual(failure.ExitCode, result.ExitCode);
        Assert.AreEqual(failure.ErrorMessage, result.ErrorMessage);
        Assert.AreEqual(exitCode, result.ExitCode);
        Assert.AreEqual(message, result.ErrorMessage);
        CollectionAssert.AreEqual(new[] { "localhost" }, tlsProvider.ReceivedTargetHosts);
        Assert.IsEmpty(dialedConnection.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHttpsProxyRefusesConnect_FailsWithCouldntConnectAndDisposesTheProxyTls()
    {
        // curl -s -S --proxy-insecure -x https://localhost:18405 https://example.com/ answered 407 ->
        //   curl: (7) CONNECT tunnel failed, response 407
        var proxyTls = new ScriptedConnection(
            Encoding.Latin1.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\nContent-Length: 0\r\n\r\n"));
        var tlsProvider = new SequencedTlsProvider(ConnectResult.Connected(proxyTls));
        var connector = CreateHttpsProxyConnector(tlsProvider);

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpsProxy },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.CouldntConnect, result.ExitCode);
        Assert.AreEqual("CONNECT tunnel failed, response 407", result.ErrorMessage);
        Assert.IsNull(result.Connection);
        Assert.IsTrue(proxyTls.IsDisposed);
        CollectionAssert.AreEqual(new[] { "localhost" }, tlsProvider.ReceivedTargetHosts);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHandshakeToTheTargetThroughAnHttpsProxyFails_ReturnsTheProvidersFailure()
    {
        // The proxy answered 200, then sent plaintext where the target's TLS was due:
        // curl -s -S --proxy-insecure -x https://localhost:18408 https://example.com/ ->
        //   curl: (35) schannel: next InitializeSecurityContext failed: SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid
        var failure = ConnectResult.Failed(
            CurlExitCode.SslConnectError,
            "schannel: next InitializeSecurityContext failed: SEC_E_INVALID_TOKEN (0x80090308) - The token supplied to the function is invalid");
        var proxyTls = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var tlsProvider = new SequencedTlsProvider(ConnectResult.Connected(proxyTls), failure);
        var connector = CreateHttpsProxyConnector(tlsProvider);

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.com", 443, UseTls: true) { Proxy = HttpsProxy },
            CancellationToken.None);

        Assert.AreEqual(failure.ExitCode, result.ExitCode);
        Assert.AreEqual(failure.ErrorMessage, result.ErrorMessage);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        CollectionAssert.AreEqual(new[] { "localhost", "example.com" }, tlsProvider.ReceivedTargetHosts);
        Assert.AreSame(proxyTls, tlsProvider.ReceivedPlaintexts[1]);
    }

    [TestMethod]
    [DataRow("http/1.1", "CONNECT: 'http/1.1' negotiated", true, DisplayName = "http/1.1, Schannel build")]
    [DataRow(null, "CONNECT: no ALPN negotiated", true, DisplayName = "no ALPN, Schannel build")]
    [DataRow("http/1.1", "CONNECT: 'http/1.1' negotiated", false, DisplayName = "http/1.1, OpenSSL build")]
    public async Task ConnectAsync_ThroughAnHttpsProxy_ReportsTheProxysAlpnAfterItsHandshakeAndBeforeTheConnect(
        string? agreedProtocol,
        string expectedLine,
        bool matchesSchannelBuild)
    {
        // curl -v --proxy-insecure -p -x https://127.0.0.1:18872 http://example.test/ says, after the
        // proxy's handshake and before the CONNECT, CONNECT: 'http/1.1' negotiated when the proxy
        // agrees on http/1.1 and CONNECT: no ALPN negotiated when it agrees on nothing
        // (8.21.0 Schannel and 8.18.0 OpenSSL, measured, BL-872); the OpenSSL build then says
        // allocate connect buffer, and both end the CONNECT with the phase-completed and
        // tunnel-established lines (BL-964).
        var proxyTls = new ScriptedConnection(Encoding.Latin1.GetBytes(EstablishedReply));
        var writtenWhenReported = -1;
        var events = new RecordingTransferEvents
        {
            OnInfo = text => writtenWhenReported = text.StartsWith("CONNECT:", StringComparison.Ordinal) ? proxyTls.Written.Count : writtenWhenReported,
        };
        var tlsProvider = new SequencedTlsProvider(ConnectResult.Connected(proxyTls, null, applicationProtocol: agreedProtocol));
        var connector = CreateHttpsProxyConnector(tlsProvider, matchesSchannelBuild);

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.test", 80, UseTls: false) { Events = events, Proxy = HttpsProxy },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        string[] connectLines = matchesSchannelBuild ? [expectedLine] : [expectedLine, "allocate connect buffer"];
        CollectionAssert.AreEqual(
            ProxyResolvedAndTriedLines
                .Concat(connectLines)
                .Concat(["Establishing HTTP proxy tunnel to example.test:80", "CONNECT phase completed for HTTP proxy", "CONNECT tunnel established, response 200"])
                .ToArray(),
            events.Info);
        Assert.AreEqual(0, writtenWhenReported);
        Assert.IsNotEmpty(proxyTls.Written);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHandshakeToAnHttpsProxyFails_ReportsNoConnectAlpnLine()
    {
        var events = new RecordingTransferEvents();
        var tlsProvider = new SequencedTlsProvider(ConnectResult.Failed(CurlExitCode.SslConnectError, "x"));
        var connector = CreateHttpsProxyConnector(tlsProvider);

        var result = await connector.ConnectAsync(
            new ConnectTarget("example.test", 80, UseTls: false) { Events = events, Proxy = HttpsProxy },
            CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        CollectionAssert.AreEqual(ProxyResolvedAndTriedLines, events.Info);
    }

    private static TcpConnector CreateHttpsProxyConnector(SequencedTlsProvider tlsProvider, bool matchesSchannelBuild = true) =>
        new(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => new ScriptedConnection([]) },
            tlsProvider,
            new ManualTimeProvider(),
            HttpProxyTunnelOptions.Default with { MatchesSchannelBuild = matchesSchannelBuild });
}
