using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// The connect steps <see cref="TcpConnector" /> writes to the target's diagnostic log
/// (ADR-0222, BL-920): <c>dns</c>, <c>connect</c>, <c>proxy</c> and <c>tls</c> at the levels the
/// ADR gives them, and never a credential.
/// </summary>
public sealed partial class TcpConnectorTests
{
    [TestMethod]
    public async Task ConnectAsync_WithADiagnosticLog_LogsTheResolutionAtInfo()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = CreateConnector(new FakeDnsResolver(Loopback, IPAddress.IPv6Loopback), ConnectingDialer(), new FakeTlsProvider());

        await connector.ConnectAsync(new ConnectTarget("example.com", 80, UseTls: false) { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "example.com:80 resolved by lookup to 127.0.0.1, ::1 in 0 ms" },
            log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheNameIsCached_LogsTheResolutionAsFromTheDnsCache()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = CreateConnector(new FakeDnsResolver(Loopback), ConnectingDialer(), new FakeTlsProvider());
        var target = new ConnectTarget("example.com", 80, UseTls: false) { DiagnosticLog = log };

        await connector.ConnectAsync(target, CancellationToken.None);
        await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual("example.com:80 resolved from the DNS cache to 127.0.0.1 in 0 ms", log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Dns)[1]);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheNameDoesNotResolve_LogsAWarningAndTheFailureAtError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Warning);
        var connector = CreateConnector(new FakeDnsResolver(), ConnectingDialer(), new FakeTlsProvider());

        await connector.ConnectAsync(new ConnectTarget("nonexistent.invalid", 80, UseTls: false) { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "nonexistent.invalid:80 did not resolve to an address that can be dialled" },
            log.At(DiagnosticLogLevel.Warning, DiagnosticLogComponents.Dns));
        CollectionAssert.AreEqual(
            new[] { "failed with CouldntResolveHost (6): Could not resolve host: nonexistent.invalid" },
            log.At(DiagnosticLogLevel.Error, DiagnosticLogComponents.Connect));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheFirstAddressRefusesAndTheSecondConnects_LogsAWarningThenTheConnection()
    {
        var refused = IPAddress.Parse("192.0.2.1");
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var dialer = new FakeTcpDialer
        {
            DialOutcome = endPoint => endPoint.Address.Equals(refused)
                ? throw new SocketException((int)SocketError.ConnectionRefused)
                : new FakeConnection(),
        };
        var connector = CreateConnector(new FakeDnsResolver(refused, Loopback), dialer, new FakeTlsProvider());

        await connector.ConnectAsync(new ConnectTarget("example.com", 80, UseTls: false) { DiagnosticLog = log }, CancellationToken.None);

        var connectLines = log.Lines.Where(line => line.Component == DiagnosticLogComponents.Connect).ToArray();
        Assert.HasCount(4, connectLines);
        Assert.AreEqual((DiagnosticLogLevel.Verbose, "dialling 192.0.2.1:80"), (connectLines[0].Level, connectLines[0].Message));
        Assert.AreEqual(DiagnosticLogLevel.Warning, connectLines[1].Level);
        Assert.StartsWith("dial to 192.0.2.1:80 failed with ConnectionRefused (SocketException: ", connectLines[1].Message);
        Assert.AreEqual((DiagnosticLogLevel.Verbose, "dialling 127.0.0.1:80"), (connectLines[2].Level, connectLines[2].Message));
        Assert.AreEqual((DiagnosticLogLevel.Info, "connected to 127.0.0.1:80 from 127.0.0.1:50000"), (connectLines[3].Level, connectLines[3].Message));
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpProxy_LogsTheConnectAtVerboseAndTheTunnelAtInfo()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        await connector.ConnectAsync(PlainTarget with { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "sending CONNECT example.com:80 to Http proxy proxy.example:3128" },
            log.At(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Proxy));
        CollectionAssert.AreEqual(
            new[] { "tunnel to example.com:80 established through Http proxy proxy.example:3128 (CONNECT 200)" },
            log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Proxy));
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughAnHttpProxyWithACredential_NeverLogsThePasswordOrItsEncoding()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());
        var proxy = HttpProxy with { Credential = new NetworkCredential("proxyuser", "pr0xy-s3cret") };

        var result = await connector.ConnectAsync(PlainTarget with { Proxy = proxy, DiagnosticLog = log }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.Contains("Proxy-Authorization: Basic", Encoding.Latin1.GetString([.. proxyConnection.Written]));
        log.AssertNeverContains("pr0xy-s3cret");
        log.AssertNeverContains(Convert.ToBase64String(Encoding.UTF8.GetBytes("proxyuser:pr0xy-s3cret")));
    }

    [TestMethod]
    public async Task ConnectAsync_ThroughASocks5ProxyWithACredential_LogsTheTunnelButNeverThePassword()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var proxyConnection = new ScriptedConnection([0x05, 0x02, 0x01, 0x00, .. Socks5Succeeded]);
        var connector = new TcpConnector(
            new FakeDnsResolver(ProxyAddress),
            new FakeTcpDialer { DialOutcome = _ => proxyConnection },
            new FakeTlsProvider(),
            new ManualTimeProvider());
        var target = new ConnectTarget("127.0.0.1", 8080, UseTls: false)
        {
            Proxy = new ProxyEndpoint(ProxyKind.Socks5, "socks.example", 1080, new NetworkCredential("bob", "s0cks-s3cret")),
            DiagnosticLog = log,
        };

        var result = await connector.ConnectAsync(target, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "Socks5 proxy socks.example:1080 handshake for 127.0.0.1:8080" },
            log.At(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Proxy));
        CollectionAssert.AreEqual(
            new[] { "tunnel to 127.0.0.1:8080 established through Socks5 proxy socks.example:1080" },
            log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Proxy));
        log.AssertNeverContains("s0cks-s3cret");
    }

    [TestMethod]
    public async Task ConnectAsync_WithAClientKeyPassPhrase_NeverLogsThePassPhrase()
    {
        // --cert with --pass whose file is missing: the OpenSSL build fails with exit 58, and the
        // failure is logged at error without the pass phrase.
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var missing = Path.Combine(Path.GetTempPath(), "bl-920-" + Guid.NewGuid().ToString("N"), "client.pem");
        var tlsProvider = new SslStreamTlsProvider(new TlsClientOptions(ClientCertificate: missing, Passphrase: "k3y-s3cret"), matchesSchannelBuild: false);
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), new FakeTcpDialer { DialOutcome = _ => new FakeConnection() }, tlsProvider, new ManualTimeProvider());

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true) { DiagnosticLog = log }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.HasCount(1, log.At(DiagnosticLogLevel.Error, DiagnosticLogComponents.Tls));
        log.AssertNeverContains("k3y-s3cret");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHandshakeCompletes_LogsTheVersionCipherAlpnAndRouteAtInfo()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var events = new RecordingTransferEvents();
        var tlsProvider = new FakeTlsProvider { Route = TlsClientRoute.HandBuilt, HandshakeToReport = Handshake(verified: true) };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), ConnectingDialer(), tlsProvider);

        var result = await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true) { Events = events, DiagnosticLog = log }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "handshake with example.com complete: Tls12, TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256, ALPN none, route HandBuilt" },
            log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Tls));
        Assert.HasCount(1, events.Handshakes, "The handshake still reaches the transfer's events.");
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHandshakeCompletesAtVerbose_LogsTheCertificateChainAndItsVerdict()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        using var certificate = SelfSignedCertificate();
        var tlsProvider = new FakeTlsProvider { HandshakeToReport = Handshake(verified: false) with { PeerCertificateChain = [certificate] } };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), ConnectingDialer(), tlsProvider);

        await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true) { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "certificate subject 'CN=bl-920.test', issuer 'CN=bl-920.test'", "certificate chain not verified" },
            log.At(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Tls));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheProviderReportsNoHandshake_LogsTheVersionAsUnreported()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Verbose);
        var connector = CreateConnector(new FakeDnsResolver(Loopback), ConnectingDialer(), new FakeTlsProvider());

        await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true) { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "handshake with example.com complete: an unreported version, an unreported cipher suite, ALPN none, route SslStream" },
            log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Tls));
        Assert.IsEmpty(log.At(DiagnosticLogLevel.Verbose, DiagnosticLogComponents.Tls));
    }

    [TestMethod]
    public async Task ConnectAsync_WithAProviderThatCannotReport_LogsTheRouteAsUnreported()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Info);
        var connector = new TcpConnector(
            new FakeDnsResolver(Loopback),
            ConnectingDialer(),
            new SequencedTlsProvider(ConnectResult.Connected(new FakeConnection { IsSecure = true }, null)),
            new ManualTimeProvider());

        await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true) { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { "handshake with example.com complete: an unreported version, an unreported cipher suite, ALPN none, route unreported" },
            log.At(DiagnosticLogLevel.Info, DiagnosticLogComponents.Tls));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheHandshakeFails_LogsItAtErrorForTlsAndConnect()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        var tlsProvider = new FakeTlsProvider { FailureToReturn = ConnectResult.Failed(CurlExitCode.SslConnectError, "handshake refused") };
        var connector = CreateConnector(new FakeDnsResolver(Loopback), ConnectingDialer(), tlsProvider);

        await connector.ConnectAsync(new ConnectTarget("example.com", 443, UseTls: true) { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "failed with SslConnectError (35): handshake refused" }, log.At(DiagnosticLogLevel.Error, DiagnosticLogComponents.Tls));
        CollectionAssert.AreEqual(new[] { "failed with SslConnectError (35): handshake refused" }, log.At(DiagnosticLogLevel.Error, DiagnosticLogComponents.Connect));
    }

    [TestMethod]
    public async Task ConnectAsync_WhenTheConnectTimesOut_LogsOperationTimedOutAtError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        var time = new ManualTimeProvider();
        var dialer = new StallingTcpDialer { OnStalled = () => time.Advance(1001) };
        var connector = new TcpConnector(new FakeDnsResolver(Loopback), dialer, new FakeTlsProvider(), time, connectTimeout: TimeSpan.FromSeconds(1));

        await connector.ConnectAsync(new ConnectTarget("127.0.0.1", 80, UseTls: false) { DiagnosticLog = log }, CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { (DiagnosticLogLevel.Error, DiagnosticLogComponents.Connect, "failed with OperationTimedOut (28): Connection timed out after 1001 milliseconds") },
            log.Lines);
    }

    [TestMethod]
    public async Task ConnectAsync_WhenAnExceptionEscapes_LogsItsTypeAndMessageAtError()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        var proxyConnection = new ScriptedConnection([]) { ReadException = new IOException("reset") };
        var connector = CreateProxyConnector(proxyConnection, new FakeTlsProvider());

        await Assert.ThrowsExactlyAsync<IOException>(async () => await connector.ConnectAsync(PlainTarget with { DiagnosticLog = log }, CancellationToken.None));

        CollectionAssert.AreEqual(new[] { "failed with IOException: reset" }, log.At(DiagnosticLogLevel.Error, DiagnosticLogComponents.Connect));
    }

    [TestMethod]
    public async Task ConnectAsync_AtErrorLevel_RecordsNoInfoOrVerboseLine()
    {
        var log = new RecordingDiagnosticLog(DiagnosticLogLevel.Error);
        var proxyConnection = new ScriptedConnection(Encoding.Latin1.GetBytes("HTTP/1.1 200 Connection established\r\n\r\n"));
        var tlsProvider = new FakeTlsProvider { HandshakeToReport = Handshake(verified: true) };
        var connector = CreateProxyConnector(proxyConnection, tlsProvider);
        var events = new RecordingTransferEvents();

        var result = await connector.ConnectAsync(PlainTarget with { UseTls = true, Events = events, DiagnosticLog = log }, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        Assert.IsEmpty(log.Lines);
        Assert.AreSame(events, tlsProvider.ReceivedEvents, "With info off the provider gets the target's own events.");
    }

    private static FakeTcpDialer ConnectingDialer() => new() { DialOutcome = _ => new FakeConnection() };

    private static TlsHandshakeEvent Handshake(bool verified) => new()
    {
        ProtocolVersion = SslProtocols.Tls12,
        CipherSuite = TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
        NegotiatedApplicationProtocol = null,
        OfferedApplicationProtocols = [],
        ServerCertificate = null,
        CertificateVerified = verified,
    };

    private static System.Security.Cryptography.X509Certificates.X509Certificate2 SelfSignedCertificate()
    {
        using var key = System.Security.Cryptography.ECDsa.Create();
        var request = new System.Security.Cryptography.X509Certificates.CertificateRequest("CN=bl-920.test", key, System.Security.Cryptography.HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }
}
