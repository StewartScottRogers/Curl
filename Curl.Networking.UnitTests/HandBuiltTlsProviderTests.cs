using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;
using Curl.Tls;
using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Runs <see cref="HandBuiltTlsProvider" /> against a server-side <see cref="SslStream" />
/// over an <see cref="InMemoryDuplexStream" /> pair, the same test server
/// <see cref="SslStreamTlsProviderTests" /> uses, so each exchange and each failure is
/// compared with what <see cref="SslStreamTlsProvider" /> does against the same server. TLS
/// 1.2 runs on every platform; TLS 1.3 is left out on macOS, whose <see cref="SslStream" />
/// has no TLS 1.3 server.
/// </summary>
[TestClass]
public sealed partial class HandBuiltTlsProviderTests
{
    private const string CertificateHost = "localhost";

    private const bool SchannelBuild = true;

    private const bool OpenSslBuild = false;

    private const string FailedToReceiveHandshakeLine = "schannel: failed to receive handshake, SSL/TLS connection failed";

    private const string UntrustedRootLine =
        "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 443);

    private static readonly string[] Http11 = ["http/1.1"];

    private static X509Certificate2 s_serverCertificate = null!;

    private static X509Certificate2 s_clientCertificate = null!;

    private string _directory = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [ClassInitialize]
    public static void CreateCertificates(TestContext context)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={CertificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(CertificateHost);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));
        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        s_serverCertificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);

        using var clientKey = RSA.Create(2048);
        var clientRequest = new CertificateRequest("CN=Curl Test Client", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var client = clientRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        s_clientCertificate = X509CertificateLoader.LoadPkcs12(client.Export(X509ContentType.Pkcs12), null, X509KeyStorageFlags.Exportable);
    }

    [ClassCleanup]
    public static void DisposeCertificates()
    {
        s_serverCertificate.Dispose();
        s_clientCertificate.Dispose();
    }

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Curl.Networking.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    public void Constructor_WithNullArguments_Throws()
    {
        var options = new TlsClientOptions();
        var store = new FakeClientCertificateStore();
        var random = SystemTlsRandomSource.Instance;
        Diagnostics.Arrange("null arguments", "options, timeProvider, certificateStore, random, one at a time");

        var optionsException = Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(null!, TimeProvider.System));
        var timeProviderException = Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(options, null!));
        var storeException = Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(options, SchannelBuild, TimeProvider.System, null!, random));
        var randomException = Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(options, SchannelBuild, TimeProvider.System, store, null!));
        Diagnostics.Act("options parameter", optionsException.ParamName);
        Diagnostics.Act("timeProvider parameter", timeProviderException.ParamName);
        Diagnostics.Act("certificateStore parameter", storeException.ParamName);
        Diagnostics.Act("random parameter", randomException.ParamName);

        Diagnostics.Assert("options parameter", "options", optionsException.ParamName);
        Assert.AreEqual("options", optionsException.ParamName);
        Diagnostics.Assert("timeProvider parameter", "timeProvider", timeProviderException.ParamName);
        Assert.AreEqual("timeProvider", timeProviderException.ParamName);
        Diagnostics.Assert("certificateStore parameter", "certificateStore", storeException.ParamName);
        Assert.AreEqual("certificateStore", storeException.ParamName);
        Diagnostics.Assert("random parameter", "random", randomException.ParamName);
        Assert.AreEqual("random", randomException.ParamName);
    }

    [TestMethod]
    public void Constructor_WithMinimumAboveMaximum_ThrowsArgumentException()
    {
        var options = new TlsClientOptions(MinimumVersion: TlsVersion.Tls12, MaximumVersion: TlsVersion.Tls11);
        Diagnostics.Arrange("minimum version", options.MinimumVersion);
        Diagnostics.Arrange("maximum version", options.MaximumVersion);

        var exception = Assert.ThrowsExactly<ArgumentException>(() => new HandBuiltTlsProvider(options, TimeProvider.System));
        Diagnostics.Act("exception type", exception.GetType().Name);

        Diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public void Warnings_WithCaCertificateDirectory_AreTheSslStreamProvidersWarnings(bool matchesSchannelBuild)
    {
        var options = new TlsClientOptions(CaCertificateDirectory: _directory);
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("CA certificate directory", "a temporary directory");

        var expected = new SslStreamTlsProvider(options, matchesSchannelBuild).Warnings.ToArray();
        var actual = Provider(options, matchesSchannelBuild).Warnings.ToArray();
        Diagnostics.Act("warnings", string.Join(" | ", actual));

        Diagnostics.Assert("warnings", string.Join(" | ", expected), string.Join(" | ", actual));
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithInvalidArguments_Throws()
    {
        var provider = new HandBuiltTlsProvider(new TlsClientOptions(), TimeProvider.System);
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("invalid arguments", "null connection, blank host, null events, null application protocols");

        var connectionException = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await provider.AuthenticateAsClientAsync(null!, CertificateHost, CancellationToken.None));
        var hostException = await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await provider.AuthenticateAsClientAsync(new FakeConnection(), " ", CancellationToken.None));
        var eventsException = await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, null!, CancellationToken.None));
        var protocolsException = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, events, false, null!, CancellationToken.None));
        Diagnostics.Act("null connection", connectionException.GetType().Name);
        Diagnostics.Act("blank host", hostException.GetType().Name);
        Diagnostics.Act("null events", eventsException.GetType().Name);
        Diagnostics.Act("null application protocols", protocolsException.GetType().Name);

        Diagnostics.Assert("null connection", nameof(ArgumentNullException), connectionException.GetType().Name);
        Diagnostics.Assert("blank host", nameof(ArgumentException), hostException.GetType().Name);
        Diagnostics.Assert("null events", nameof(ArgumentNullException), eventsException.GetType().Name);
        Diagnostics.Assert("null application protocols", nameof(ArgumentNullException), protocolsException.GetType().Name);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_Tls12UnderInsecure_ReturnsASecureConnectionThatRoundTripsBytes()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50000);
        var provider = new HandBuiltTlsProvider(Tls12Only(new TlsClientOptions(Insecure: true)), TimeProvider.System);
        Diagnostics.Arrange("insecure", true);
        Diagnostics.Arrange("maximum version", TlsVersion.Tls12);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await provider.AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint, localEndPoint), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        Diagnostics.Assert("is secure", true, connection.IsSecure);
        Assert.IsTrue(connection.IsSecure);
        Assert.AreSame(ServerEndPoint, connection.RemoteEndPoint);
        Assert.AreSame(localEndPoint, connection.LocalEndPoint);
        Diagnostics.Assert("peer certificate length", s_serverCertificate.RawData.Length, result.PeerCertificates.Single().Length);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, result.PeerCertificates.Single().ToArray());
        Assert.IsNotNull(result.Timings);
        var echoed = Encoding.ASCII.GetString(await EchoAsync(connection, "ping"));
        Diagnostics.Act("echoed text", echoed);
        Diagnostics.Assert("echoed text", "ping", echoed);
        Assert.AreEqual("ping", echoed);
        await connection.DisposeAsync();
        Diagnostics.Assert("plaintext disposed", true, client.IsDisposed);
        Assert.IsTrue(client.IsDisposed);
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsClientAsync_Tls13UnderInsecure_ReturnsASecureConnectionThatRoundTripsBytes()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls13);
        var events = new RecordingTransferEvents();
        var provider = new HandBuiltTlsProvider(new TlsClientOptions(Insecure: true), TimeProvider.System);
        Diagnostics.Arrange("insecure", true);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls13);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await provider.AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        var echoed = Encoding.ASCII.GetString(await EchoAsync(connection, "ping"));
        Diagnostics.Act("echoed text", echoed);
        Diagnostics.Assert("echoed text", "ping", echoed);
        Assert.AreEqual("ping", echoed);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Act("protocol version", handshake.ProtocolVersion);
        Diagnostics.Act("cipher suite", handshake.CipherSuite);
        Diagnostics.Assert("protocol version", SslProtocols.Tls13, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls13, handshake.ProtocolVersion);
        Diagnostics.Assert("cipher suite", TlsCipherSuite.TLS_AES_256_GCM_SHA384, handshake.CipherSuite);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_256_GCM_SHA384, handshake.CipherSuite);
        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    // One ClientHello offers TLS 1.3 and TLS 1.2, and the handshake continues as TLS 1.2 (BL-821).
    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_Tls12MinimumWithNoCeilingAgainstATls12OnlyServer_ConnectsOverTls12(bool matchesSchannelBuild)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls12);
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("minimum version", TlsVersion.Tls12);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        var echoed = Encoding.ASCII.GetString(await EchoAsync(connection, "ping"));
        Diagnostics.Act("echoed text", echoed);
        Diagnostics.Assert("echoed text", "ping", echoed);
        Assert.AreEqual("ping", echoed);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("protocol version", SslProtocols.Tls12, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls12, handshake.ProtocolVersion);
        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    // A TLS 1.3 minimum offers TLS 1.3 alone, which a TLS 1.2-only server refuses.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_Tls13MinimumAgainstATls12OnlyServer_FailsWithExit35()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var options = new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13);
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("minimum version", TlsVersion.Tls13);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("connection is null", true, result.Connection is null);
        Assert.IsNull(result.Connection);
        await server.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    // A CCM-only --tls13-ciphers still offers TLS 1.2 beside it, and a TLS 1.2 server takes that.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithOnlyACcmTls13CipherAndATls12Server_ConnectsOverTls12()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, Tls13Ciphers: "TLS_AES_128_CCM_SHA256");
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("TLS 1.3 ciphers", options.Tls13Ciphers);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("protocol version", SslProtocols.Tls12, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls12, handshake.ProtocolVersion);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    // With no TLS 1.2 suite the client can run, the range offers TLS 1.3 alone.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsClientAsync_WithOnlyTls12CiphersTheClientCannotRun_ConnectsOverTls13()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls13);
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, Ciphers: "TLS_ECDHE_ECDSA_WITH_AES_128_CCM");
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("ciphers", options.Ciphers);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls13);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("protocol version", SslProtocols.Tls13, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls13, handshake.ProtocolVersion);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    // A TLS 1.3 minimum leaves the --ciphers suites out and offers the default TLS 1.3 ones.
    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsClientAsync_WithTls12CiphersAndATls13Minimum_ConnectsOverTls13()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls13);
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, Ciphers: "ECDHE-RSA-AES128-GCM-SHA256", MinimumVersion: TlsVersion.Tls13);
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("ciphers", options.Ciphers);
        Diagnostics.Arrange("minimum version", TlsVersion.Tls13);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls13);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("protocol version", SslProtocols.Tls13, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls13, handshake.ProtocolVersion);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_Tls12_ReportsTheTrustAndTheHandshakeAsTheSslStreamProviderDoes()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12, Http11);
        var events = new RecordingTransferEvents();
        var options = Tls12Only(new TlsClientOptions(Insecure: true));
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("insecure", true);
        Diagnostics.Arrange("maximum version", TlsVersion.Tls12);
        Diagnostics.Arrange("offered application protocols", string.Join(",", Http11));
        Diagnostics.Arrange("is proxy", true);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: true, Http11, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents[0]);
        Diagnostics.Assert("trust verifies peer", false, ((TlsTrustEvent)events.TlsEvents[0]).VerifiesPeer);
        Assert.IsFalse(((TlsTrustEvent)events.TlsEvents[0]).VerifiesPeer);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("protocol version", SslProtocols.Tls12, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls12, handshake.ProtocolVersion);
        Diagnostics.Assert("cipher suite", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, handshake.CipherSuite);
        Assert.AreEqual(TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, handshake.CipherSuite);
        Diagnostics.Assert("negotiated application protocol", "http/1.1", handshake.NegotiatedApplicationProtocol);
        Assert.AreEqual("http/1.1", handshake.NegotiatedApplicationProtocol);
        Diagnostics.Assert("result application protocol", "http/1.1", result.ApplicationProtocol);
        Assert.AreEqual("http/1.1", result.ApplicationProtocol);
        CollectionAssert.AreEqual(Http11, handshake.OfferedApplicationProtocols.ToArray());
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.ServerCertificate!.RawData);
        Diagnostics.Assert("certificate verified", false, handshake.CertificateVerified);
        Assert.IsFalse(handshake.CertificateVerified);
        Diagnostics.Assert("certificate verify result", 18L, handshake.CertificateVerifyResult);
        Assert.AreEqual(18L, handshake.CertificateVerifyResult);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.PeerCertificateChain.Single().RawData);
        Diagnostics.Assert("is proxy", true, handshake.IsProxy);
        Assert.IsTrue(handshake.IsProxy);
        Diagnostics.Assert("verified host name is null", true, handshake.VerifiedHostName is null);
        Assert.IsNull(handshake.VerifiedHostName);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_UnderNoAlpn_OffersNoProtocols()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12, Http11);
        var events = new RecordingTransferEvents();
        var options = Tls12Only(new TlsClientOptions(Insecure: true, UseAlpn: false));
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("use ALPN", false);
        Diagnostics.Arrange("application protocols passed in", string.Join(",", Http11));
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: false, Http11, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("offered application protocols", 0, handshake.OfferedApplicationProtocols.Count());
        Assert.IsEmpty(handshake.OfferedApplicationProtocols);
        Diagnostics.Assert("negotiated application protocol", "(none)", handshake.NegotiatedApplicationProtocol ?? "(none)");
        Assert.IsNull(handshake.NegotiatedApplicationProtocol);
        Diagnostics.Assert("result application protocol", "(none)", result.ApplicationProtocol ?? "(none)");
        Assert.IsNull(result.ApplicationProtocol);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    // BL-708's criterion: a self-signed chain fails with the exit and message the SslStream
    // provider gives against the same server, for each build and each name.
    [TestMethod]
    [DataRow(SchannelBuild, CertificateHost)]
    [DataRow(OpenSslBuild, CertificateHost)]
    [DataRow(SchannelBuild, "wrong.example")]
    [DataRow(OpenSslBuild, "wrong.example")]
    public async Task AuthenticateAsClientAsync_WithAnUntrustedSelfSignedChain_FailsAsTheSslStreamProviderDoes(bool matchesSchannelBuild, string targetHost)
    {
        var options = Tls12Only(new TlsClientOptions());
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("target host", targetHost);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        (ConnectResult Result, bool PlaintextDisposed) handBuilt;
        (ConnectResult Result, bool PlaintextDisposed) sslStream;
        using (Diagnostics.Phase("TLS handshake"))
        {
            handBuilt = await HandshakeAsync(Provider(options, matchesSchannelBuild), targetHost);
            sslStream = await HandshakeAsync(new SslStreamTlsProvider(options with { MaximumVersion = TlsVersion.SystemDefault }, matchesSchannelBuild), targetHost);
        }

        ActConnectResult(handBuilt.Result);
        Diagnostics.Act("SslStream provider exit code", sslStream.Result.ExitCode);
        Diagnostics.Act("SslStream provider error message", sslStream.Result.ErrorMessage ?? "(none)");
        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, handBuilt.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, handBuilt.Result.ExitCode);
        Diagnostics.Assert("exit code matches SslStream provider", sslStream.Result.ExitCode, handBuilt.Result.ExitCode);
        Assert.AreEqual(sslStream.Result.ExitCode, handBuilt.Result.ExitCode);
        Diagnostics.Assert("error message matches SslStream provider", sslStream.Result.ErrorMessage, handBuilt.Result.ErrorMessage);
        Assert.AreEqual(sslStream.Result.ErrorMessage, handBuilt.Result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, handBuilt.PlaintextDisposed);
        Assert.IsTrue(handBuilt.PlaintextDisposed);
    }

    // curl 8.21.0 Schannel -v, untrusted self-signed root, exit 60: the failf text as an info
    // line after the ALPN offer, before "closing connection #0" (measured 2026-10-03, BL-1323).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnUntrustedSelfSignedChainInTheSchannelBuild_ReportsSecEUntrustedRootOnceAfterTheFailedHandshake()
    {
        var handshakesBeforeEcho = -1;
        RecordingTransferEvents? events = null;
        events = new RecordingTransferEvents { OnInfo = _ => handshakesBeforeEcho = events!.Handshakes.Count };
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("maximum version", TlsVersion.Tls12);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(Provider(Tls12Only(new TlsClientOptions()), SchannelBuild), CertificateHost, events, Http11);
        }

        ActConnectResult(result);
        Diagnostics.Act("info lines", events.Info.Count);
        Diagnostics.Act("handshakes before the echo", handshakesBeforeEcho);
        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Diagnostics.Assert("error message", UntrustedRootLine, result.ErrorMessage);
        Assert.AreEqual(UntrustedRootLine, result.ErrorMessage);
        Assert.AreEqual(UntrustedRootLine, Assert.ContainsSingle(events.Info));
        Diagnostics.Assert("handshakes before the echo", 1, handshakesBeforeEcho);
        Assert.AreEqual(1, handshakesBeforeEcho);
        Assert.IsTrue(Assert.ContainsSingle(events.Handshakes).Failed);
    }

    // Neither -k in the Schannel build nor the OpenSSL build refusing the same chain prints it.
    [TestMethod]
    [DataRow(SchannelBuild, true)]
    [DataRow(OpenSslBuild, false)]
    public async Task AuthenticateAsClientAsync_WithAnUntrustedSelfSignedChainInsecureOrInTheOpenSslBuild_ReportsNoSecEUntrustedRootLine(bool matchesSchannelBuild, bool insecure)
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("insecure", insecure);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(Provider(Tls12Only(new TlsClientOptions(Insecure: insecure)), matchesSchannelBuild), CertificateHost, events, Http11);
        }

        ActConnectResult(result);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Assert("info lines contain the untrusted root line", false, events.Info.Contains(UntrustedRootLine));
        CollectionAssert.DoesNotContain(events.Info, UntrustedRootLine);
        if (result.Connection is { } connection)
        {
            await connection.DisposeAsync();
        }
    }

    // curl 8.18.0 OpenSSL -v, untrusted self-signed root, exit 60: "SSL connection using", the
    // ALPN answer and the certificate before the error (ADR-0371, BL-1202).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnUntrustedSelfSignedChainInTheOpenSslBuild_ReportsAFailedHandshakeCarryingTheCertificate()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("maximum version", TlsVersion.Tls12);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(Provider(Tls12Only(new TlsClientOptions()), OpenSslBuild), CertificateHost, events, Http11);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("handshake failed", true, handshake.Failed);
        Assert.IsTrue(handshake.Failed);
        Diagnostics.Assert("protocol version", SslProtocols.Tls12, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.Tls12, handshake.ProtocolVersion);
        Diagnostics.Assert("cipher suite", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, handshake.CipherSuite);
        Assert.AreEqual(TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, handshake.CipherSuite);
        CollectionAssert.AreEqual(Http11, handshake.OfferedApplicationProtocols.ToArray());
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.ServerCertificate!.RawData);
        Diagnostics.Assert("certificate verified", false, handshake.CertificateVerified);
        Assert.IsFalse(handshake.CertificateVerified);
        Diagnostics.Assert("verified host name", CertificateHost, handshake.VerifiedHostName);
        Assert.AreEqual(CertificateHost, handshake.VerifiedHostName);
    }

    // curl 8.18.0 OpenSSL -v -k --tlsv1.3 against a TLS 1.2 server, exit 35: the ALPN offer and
    // nothing negotiated (ADR-0371, BL-1202).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithATls13FloorAgainstATls12ServerInTheOpenSslBuild_ReportsAFailedHandshakeNegotiatingNothing()
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("insecure", true);
        Diagnostics.Arrange("minimum version", TlsVersion.Tls13);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeAsync(
                Provider(new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13), OpenSslBuild), CertificateHost, events, Http11);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("handshake failed", true, handshake.Failed);
        Assert.IsTrue(handshake.Failed);
        Diagnostics.Assert("protocol version", SslProtocols.None, handshake.ProtocolVersion);
        Assert.AreEqual(SslProtocols.None, handshake.ProtocolVersion);
        CollectionAssert.AreEqual(Http11, handshake.OfferedApplicationProtocols.ToArray());
        Diagnostics.Assert("server certificate is null", true, handshake.ServerCertificate is null);
        Assert.IsNull(handshake.ServerCertificate);
        Diagnostics.Assert("negotiated application protocol", "(none)", handshake.NegotiatedApplicationProtocol ?? "(none)");
        Assert.IsNull(handshake.NegotiatedApplicationProtocol);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingTheServerCertificate_Succeeds(bool matchesSchannelBuild)
    {
        var caFile = WriteFile("ca.pem", s_serverCertificate.ExportCertificatePem());
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("CA certificate file", Path.GetFileName(caFile));
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeAsync(Provider(Tls12Only(new TlsClientOptions(CaCertificateFile: caFile)), matchesSchannelBuild), CertificateHost, events);
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("certificate verified", true, handshake.CertificateVerified);
        Assert.IsTrue(handshake.CertificateVerified);
        Diagnostics.Assert("certificate verify result", 0L, handshake.CertificateVerifyResult);
        Assert.AreEqual(0L, handshake.CertificateVerifyResult);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryHoldingTheServerCertificateInTheOpenSslBuild_Succeeds()
    {
        WriteFile("server.pem", s_serverCertificate.ExportCertificatePem());
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("CA certificate directory", "a temporary directory holding server.pem");
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeAsync(Provider(Tls12Only(new TlsClientOptions(CaCertificateDirectory: _directory)), OpenSslBuild), CertificateHost);
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileNamingADirectory_FailsWithExit77AsTheSslStreamProviderDoes(bool matchesSchannelBuild)
    {
        var options = Tls12Only(new TlsClientOptions(CaCertificateFile: _directory));
        var (plaintext, plaintextStream) = Unanswered();
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("CA certificate file", "a directory, not a file");

        ConnectResult result;
        ConnectResult expected;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
            expected = await new SslStreamTlsProvider(options, matchesSchannelBuild).AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslCacertBadfile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.ExitCode);
        Diagnostics.Assert("error message matches SslStream provider", expected.ErrorMessage, result.ErrorMessage);
        Assert.AreEqual(expected.ErrorMessage, result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, plaintextStream.IsDisposed);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCiphersInTheSchannelBuild_FailsWithExit59()
    {
        var (plaintext, plaintextStream) = Unanswered();
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("ciphers", "ECDHE-RSA-AES128-GCM-SHA256");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(new TlsClientOptions(Ciphers: "ECDHE-RSA-AES128-GCM-SHA256"), SchannelBuild)
                .AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslCipher, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCipher, result.ExitCode);
        Diagnostics.Assert("error message", TlsFailureMessages.SchannelCipherListRefused, result.ErrorMessage);
        Assert.AreEqual(TlsFailureMessages.SchannelCipherListRefused, result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, plaintextStream.IsDisposed);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    [DataRow("nonsense", null, TlsVersion.SystemDefault, TlsVersion.Tls12)]
    [DataRow("ECDHE-ECDSA-AES128-CCM", null, TlsVersion.SystemDefault, TlsVersion.Tls12)]
    [DataRow("TLS_PSK_WITH_AES_128_GCM_SHA256", null, TlsVersion.SystemDefault, TlsVersion.Tls12)]
    public async Task AuthenticateAsClientAsync_WithCiphersTheOpenSslBuildCannotOffer_FailsWithExit59(string? ciphers, string? tls13Ciphers, TlsVersion minimumVersion, TlsVersion maximumVersion)
    {
        var (plaintext, plaintextStream) = Unanswered();
        var options = new TlsClientOptions(Ciphers: ciphers, Tls13Ciphers: tls13Ciphers, MinimumVersion: minimumVersion, MaximumVersion: maximumVersion);
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("ciphers", ciphers ?? "(none)");
        Diagnostics.Arrange("TLS 1.3 ciphers", tls13Ciphers ?? "(none)");
        Diagnostics.Arrange("minimum version", minimumVersion);
        Diagnostics.Arrange("maximum version", maximumVersion);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslCipher, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCipher, result.ExitCode);
        Diagnostics.Assert("error message", OpenSslCipherSuites.Unapplied(ciphers, tls13Ciphers), result.ErrorMessage);
        Assert.AreEqual(OpenSslCipherSuites.Unapplied(ciphers, tls13Ciphers), result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, plaintextStream.IsDisposed);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuild_OffersOnlyThoseSuites()
    {
        var events = new RecordingTransferEvents();
        var options = Tls12Only(new TlsClientOptions(Insecure: true, Ciphers: "ECDHE-RSA-AES128-GCM-SHA256"));
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("ciphers", options.Ciphers);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        ArrangeCertificate("server certificate", s_serverCertificate);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeAsync(Provider(options, OpenSslBuild), CertificateHost, events);
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("cipher suite", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256, handshake.CipherSuite);
        Assert.AreEqual(TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256, handshake.CipherSuite);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)]
    public async Task AuthenticateAsClientAsync_WithTls13CiphersInTheOpenSslBuild_OffersOnlyThoseSuites()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls13);
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, Tls13Ciphers: "TLS_AES_128_GCM_SHA256");
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("TLS 1.3 ciphers", options.Tls13Ciphers);
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls13);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Diagnostics.Assert("cipher suite", TlsCipherSuite.TLS_AES_128_GCM_SHA256, handshake.CipherSuite);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_128_GCM_SHA256, handshake.CipherSuite);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAClientCertificateThatDoesNotLoad_FailsWithExit58()
    {
        var (plaintext, plaintextStream) = Unanswered();
        var options = new TlsClientOptions(ClientCertificate: Path.Combine(_directory, "missing.p12"));
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("client certificate", "missing.p12 (does not exist)");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, SchannelBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Diagnostics.Assert("plaintext disposed", true, plaintextStream.IsDisposed);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    // BL-1088: the Schannel build's client-certificate line comes before the failure; no SNI line.
    [TestMethod]
    [DataRow(SchannelBuild, 1)]
    [DataRow(OpenSslBuild, 0)]
    public async Task AuthenticateAsClientAsync_WithAClientCertificateThatDoesNotLoadToAnIpAddress_ReportsTheTrustWithoutTheIpAddressOnlyInTheSchannelBuild(bool matchesSchannelBuild, int expectedTrustEvents)
    {
        var (plaintext, _) = Unanswered();
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(ClientCertificate: Path.Combine(_directory, "nosuch.pem"));
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("client certificate", "nosuch.pem (does not exist)");
        Diagnostics.Arrange("target host", "127.0.0.1");
        Diagnostics.Arrange("expected trust events", expectedTrustEvents);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(plaintext, "127.0.0.1", events, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Act("trust events", events.TlsEvents.Count);
        Diagnostics.Assert("exit code", CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Diagnostics.Assert("trust events", expectedTrustEvents, events.TlsEvents.Count);
        Assert.HasCount(expectedTrustEvents, events.TlsEvents);
        var noIpAddressTrust = events.TlsEvents.Cast<TlsTrustEvent>().All(trust => !trust.TargetsIpAddress && !trust.UsesAutomaticClientCertificate);
        Diagnostics.Assert("no trust event targets an IP address or uses an automatic certificate", true, noIpAddressTrust);
        Assert.IsTrue(noIpAddressTrust);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerAsksForAClientCertificate_PresentsTheCertCertificate()
    {
        var file = WriteFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var options = Tls12Only(new TlsClientOptions(Insecure: true, ClientCertificate: file));
        var (client, server) = InMemoryDuplexStream.CreatePair();
        X509Certificate? received = null;
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("client certificate file", Path.GetFileName(file));
        Diagnostics.Arrange("server TLS protocol", SslProtocols.Tls12);
        Diagnostics.Arrange("server requires a client certificate", true);
        ArrangeCertificate("server certificate", s_serverCertificate);
        ArrangeCertificate("client certificate", s_clientCertificate);
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = s_serverCertificate,
                EnabledSslProtocols = SslProtocols.Tls12,
                ClientCertificateRequired = true,
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                {
                    received = certificate is null ? null : X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                    return true;
                },
            });
        });

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(options, SchannelBuild).AuthenticateAsClientAsync(
                new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.Ok, result.ExitCode);
        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await serverTask;
        Diagnostics.Act("received certificate hash", received?.GetCertHashString() ?? "(none)");
        Diagnostics.Assert("received certificate hash", s_clientCertificate.GetCertHashString(), received?.GetCertHashString());
        Assert.AreEqual(s_clientCertificate.GetCertHashString(), received?.GetCertHashString());
        await result.Connection!.DisposeAsync();
    }

    // The hand-built client's failures in the text each build's curl prints (ADR-0140).
    [TestMethod]
    [DataRow(SchannelBuild, "schannel: failed to receive handshake, SSL/TLS connection failed")]
    [DataRow(OpenSslBuild, "TLS connect error: error:0A000126:SSL routines::unexpected eof while reading")]
    public async Task AuthenticateAsClientAsync_WhenTheServerClosesMidHandshake_ReportsTheMeasuredLine(bool matchesSchannelBuild, string expected)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("server answer", "reads the ClientHello, then closes");
        Diagnostics.Arrange("expected error message", expected);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeWithServerAnsweringAsync(matchesSchannelBuild, answer: null);
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Diagnostics.Assert("error message", expected, result.Result.ErrorMessage);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
        Diagnostics.Assert("plaintext disposed", true, result.PlaintextDisposed);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    // curl 8.21.0 Schannel -v, --tlsv1.3 against a TLS 1.2-only server, exit 35: the failf text
    // as an info line after the ALPN offer, before "closing connection #0" (measured 2026-10-03, BL-1324).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerClosesMidHandshakeInTheSchannelBuild_ReportsFailedToReceiveHandshakeOnceAsTheLastLine()
    {
        var handshakesAtEcho = -1;
        RecordingTransferEvents? events = null;
        events = new RecordingTransferEvents { OnInfo = _ => handshakesAtEcho = events!.Handshakes.Count };
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("server answer", "reads the ClientHello, then closes");

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithServerAnsweringAsync(SchannelBuild, answer: null, events: events);
        }

        ActConnectResult(result);
        Diagnostics.Act("info lines", string.Join(" | ", events.Info));
        Diagnostics.Act("handshakes at the echo", handshakesAtEcho);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("error message", FailedToReceiveHandshakeLine, result.ErrorMessage);
        Assert.AreEqual(FailedToReceiveHandshakeLine, result.ErrorMessage);
        Diagnostics.Assert("last info line", FailedToReceiveHandshakeLine, events.Info[^1]);
        Assert.AreEqual(FailedToReceiveHandshakeLine, events.Info[^1]);
        Diagnostics.Assert("echo count", 1, events.Info.Count(line => line == FailedToReceiveHandshakeLine));
        Assert.AreEqual(1, events.Info.Count(line => line == FailedToReceiveHandshakeLine));
        Diagnostics.Assert("handshakes at the echo", events.Handshakes.Count, handshakesAtEcho);
        Assert.AreEqual(events.Handshakes.Count, handshakesAtEcho);
    }

    // Every Schannel exit 35 is a failf, so a named security status is echoed too (BL-1324).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerSendsAHandshakeFailureAlertInTheSchannelBuild_ReportsTheSecurityStatusOnceAsTheLastLine()
    {
        var events = new RecordingTransferEvents();
        byte[] alert = [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x28];
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("server answer", "a fatal handshake_failure alert, then closes");
        Diagnostics.Bytes("server alert", alert);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithServerAnsweringAsync(SchannelBuild, answer: alert, events: events);
        }

        ActConnectResult(result);
        Diagnostics.Act("info lines", events.Info.Count);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("error message starts with", "schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326)", result.ErrorMessage);
        StringAssert.StartsWith(result.ErrorMessage, "schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326)");
        Diagnostics.Assert("last info line", result.ErrorMessage, events.Info[^1]);
        Assert.AreEqual(result.ErrorMessage, events.Info[^1]);
        Diagnostics.Assert("echo count", 1, events.Info.Count(line => line == result.ErrorMessage));
        Assert.AreEqual(1, events.Info.Count(line => line == result.ErrorMessage));
    }

    // The OpenSSL build's exit 35 lines are BL-1178's; this echo adds none of them.
    [TestMethod]
    [DataRow(null)]
    [DataRow(new byte[] { 0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x28 })]
    public async Task AuthenticateAsClientAsync_WhenTheHandshakeFailsInTheOpenSslBuild_ReportsNoFailureLine(byte[]? answer)
    {
        var events = new RecordingTransferEvents();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("server answer", answer is null ? "reads the ClientHello, then closes" : "a fatal handshake_failure alert, then closes");
        if (answer is not null)
        {
            Diagnostics.Bytes("server alert", answer);
        }

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            (result, _) = await HandshakeWithServerAnsweringAsync(OpenSslBuild, answer, events: events);
        }

        ActConnectResult(result);
        Diagnostics.Act("info lines", events.Info.Count);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("info lines contain the error message", false, events.Info.Contains(result.ErrorMessage!));
        CollectionAssert.DoesNotContain(events.Info, result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(SchannelBuild, "schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.")]
    [DataRow(OpenSslBuild, "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    public async Task AuthenticateAsClientAsync_WhenTheServerSendsAHandshakeFailureAlert_ReportsTheMeasuredLine(bool matchesSchannelBuild, string expected)
    {
        byte[] alert = [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x28];
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("server answer", "a fatal handshake_failure alert, then closes");
        Diagnostics.Bytes("server alert", alert);
        Diagnostics.Arrange("expected error message", expected);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeWithServerAnsweringAsync(matchesSchannelBuild, answer: alert);
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Diagnostics.Assert("error message", expected, result.Result.ErrorMessage);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(SchannelBuild, "schannel: failed to receive handshake, SSL/TLS connection failed")]
    [DataRow(OpenSslBuild, "TLS connect error: error:0A000126:SSL routines::unexpected eof while reading")]
    public async Task AuthenticateAsClientAsync_WhenTheServerClosesMidTls13Handshake_ReportsTheMeasuredLine(bool matchesSchannelBuild, string expected)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("insecure", true);
        Diagnostics.Arrange("server answer", "reads the ClientHello, then closes");
        Diagnostics.Arrange("expected error message", expected);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeWithServerAnsweringAsync(matchesSchannelBuild, answer: null, new TlsClientOptions(Insecure: true));
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Diagnostics.Assert("error message", expected, result.Result.ErrorMessage);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheHandshakeFailsWithACertCertificateLoaded_FailsAndDisposesThePlaintext()
    {
        var file = WriteFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("client certificate file", Path.GetFileName(file));
        Diagnostics.Arrange("server answer", "reads the ClientHello, then closes");
        ArrangeCertificate("client certificate", s_clientCertificate);

        (ConnectResult Result, bool PlaintextDisposed) result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await HandshakeWithServerAnsweringAsync(SchannelBuild, answer: null, Tls12Only(new TlsClientOptions(Insecure: true, ClientCertificate: file)));
        }

        ActConnectResult(result.Result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Diagnostics.Assert("plaintext disposed", true, result.PlaintextDisposed);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnUnusableCaCertificateFileAndACertCertificate_FailsWithExit77()
    {
        var file = WriteFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var (plaintext, plaintextStream) = Unanswered();
        Diagnostics.Arrange("build", "Schannel");
        Diagnostics.Arrange("CA certificate file", "a directory, not a file");
        Diagnostics.Arrange("client certificate file", Path.GetFileName(file));
        ArrangeCertificate("client certificate", s_clientCertificate);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(Tls12Only(new TlsClientOptions(CaCertificateFile: _directory, ClientCertificate: file)), SchannelBuild)
                .AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslCacertBadfile, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.ExitCode);
        Diagnostics.Assert("plaintext disposed", true, plaintextStream.IsDisposed);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.SystemDefault, TlsVersion.Tls10, "Recv failure: Connection was reset")]
    [DataRow(OpenSslBuild, TlsVersion.SystemDefault, TlsVersion.Tls12, "Recv failure: Connection reset by peer")]
    [DataRow(OpenSslBuild, TlsVersion.Tls12, TlsVersion.Tls12, "Recv failure: Connection reset by peer")]
    [DataRow(OpenSslBuild, TlsVersion.SystemDefault, TlsVersion.SystemDefault, "Recv failure: Connection reset by peer")]
    public async Task AuthenticateAsClientAsync_WhenTheServerResetsMidHandshake_ReportsTheMeasuredLine(
        bool matchesSchannelBuild,
        TlsVersion minimumVersion,
        TlsVersion maximumVersion,
        string expected)
    {
        Diagnostics.Arrange("build", matchesSchannelBuild ? "Schannel" : "OpenSSL");
        Diagnostics.Arrange("minimum version", minimumVersion);
        Diagnostics.Arrange("maximum version", maximumVersion);
        Diagnostics.Arrange("server behaviour", "resets the connection");
        Diagnostics.Arrange("expected error message", expected);

        ConnectResult result;
        using (Diagnostics.Phase("TLS handshake"))
        {
            result = await Provider(new TlsClientOptions(Insecure: true, MinimumVersion: minimumVersion, MaximumVersion: maximumVersion), matchesSchannelBuild)
                .AuthenticateAsClientAsync(new ResettingConnection(SocketError.ConnectionReset), CertificateHost, CancellationToken.None);
        }

        ActConnectResult(result);
        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Diagnostics.Assert("error message", expected, result.ErrorMessage);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenCancelled_ThrowsOperationCanceledExceptionAndDisposesThePlaintext()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        Diagnostics.Arrange("build", "OpenSSL");
        Diagnostics.Arrange("cancellation token", "already cancelled");

        OperationCanceledException exception;
        using (Diagnostics.Phase("TLS handshake"))
        {
            exception = await Assert.ThrowsAsync<OperationCanceledException>(async () => await Provider(Tls12Only(new TlsClientOptions()), OpenSslBuild)
                .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, cancellation.Token));
        }

        Diagnostics.Act("exception type", exception.GetType().Name);
        Diagnostics.Assert("plaintext disposed", true, client.IsDisposed);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public void ToTlsClientCertificate_TakesAnRsaOrEcdsaKeyAndNothingElse()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var ecdsaCertificate = new CertificateRequest("CN=ec", ecdsa, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var withoutKey = X509CertificateLoader.LoadCertificate(s_clientCertificate.RawData);
        ArrangeCertificate("RSA client certificate", s_clientCertificate);
        ArrangeCertificate("ECDSA certificate", ecdsaCertificate);
        ArrangeCertificate("certificate without a private key", withoutKey);

        var fromNull = HandBuiltTlsProvider.ToTlsClientCertificate(null);
        var fromWithoutKey = HandBuiltTlsProvider.ToTlsClientCertificate(withoutKey);
        var rsaClientCertificate = HandBuiltTlsProvider.ToTlsClientCertificate(s_clientCertificate)!;
        var ecdsaClientCertificate = HandBuiltTlsProvider.ToTlsClientCertificate(ecdsaCertificate)!;
        Diagnostics.Act("from null", fromNull is null ? "(none)" : "converted");
        Diagnostics.Act("from a certificate without a key", fromWithoutKey is null ? "(none)" : "converted");
        Diagnostics.Act("RSA signing key", rsaClientCertificate.SigningKey.GetType().Name);
        Diagnostics.Act("ECDSA signing key", ecdsaClientCertificate.SigningKey.GetType().Name);

        Diagnostics.Assert("from null", "(none)", fromNull is null ? "(none)" : "converted");
        Assert.IsNull(fromNull);
        Diagnostics.Assert("from a certificate without a key", "(none)", fromWithoutKey is null ? "(none)" : "converted");
        Assert.IsNull(fromWithoutKey);
        Diagnostics.Assert("RSA signing key", nameof(RsaTlsSigningKey), rsaClientCertificate.SigningKey.GetType().Name);
        Assert.IsInstanceOfType<RsaTlsSigningKey>(rsaClientCertificate.SigningKey);
        Diagnostics.Assert("ECDSA signing key", nameof(EcdsaTlsSigningKey), ecdsaClientCertificate.SigningKey.GetType().Name);
        Assert.IsInstanceOfType<EcdsaTlsSigningKey>(ecdsaClientCertificate.SigningKey);
        CollectionAssert.AreEqual(ecdsaCertificate.RawData, ecdsaClientCertificate.CertificateChain.Single());
    }

    [TestMethod]
    public void ToTlsClientCertificate_OfALoadedPkcs12RsaKey_CanSignTheLegacyRsaRule()
    {
        var path = ClientCertificateLoaderTests.WriteRsaPkcs12File(_directory);
        Diagnostics.Arrange("PKCS#12 file", Path.GetFileName(path));
        Diagnostics.Arrange("password", "secret");
        var (loaded, failure) = ClientCertificateLoader.LoadAsSchannelBuild(path, "secret", null, new FakeClientCertificateStore());
        Diagnostics.Act("load failure", failure is null ? "(none)" : "present");
        Diagnostics.Assert("load failure is null", true, failure is null);
        Assert.IsNull(failure);
        using (loaded)
        {
            var signingKey = HandBuiltTlsProvider.ToTlsClientCertificate(loaded)!.SigningKey;

            var canSign = CanSignLegacyRsaRule(signingKey);
            Diagnostics.Act("signing key", signingKey.GetType().Name);
            Diagnostics.Assert("can sign the legacy RSA rule", true, canSign);
            Assert.IsTrue(canSign);
        }
    }

    [TestMethod]
    [DataRow("localhost", "localhost")]
    [DataRow("127.0.0.1", null)]
    [DataRow("[::1]", null)]
    public void ServerNameFor_LeavesOutAnIpAddress(string targetHost, string? expected)
    {
        Diagnostics.Arrange("target host", targetHost);
        Diagnostics.Arrange("expected server name", expected ?? "(none)");

        var actual = HandBuiltTlsProvider.ServerNameFor(targetHost);
        Diagnostics.Act("server name", actual ?? "(none)");

        Diagnostics.Assert("server name", expected ?? "(none)", actual ?? "(none)");
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ServerCertificateOf_WithNoCertificate_IsNull()
    {
        Diagnostics.Arrange("peer certificates", 0);

        var actual = HandBuiltTlsProvider.ServerCertificateOf([]);
        Diagnostics.Act("server certificate", actual is null ? "(none)" : "present");

        Diagnostics.Assert("server certificate", "(none)", actual is null ? "(none)" : "present");
        Assert.IsNull(actual);
    }

    // TLS 1.0 and 1.1's RSA rule and TlsSigningKey.CanSign(rule) are internal to Curl.Tls, so
    // the test reaches them by reflection.
    private static bool CanSignLegacyRsaRule(TlsSigningKey signingKey)
    {
        var legacyRules = (System.Collections.IList)typeof(TlsSignatureScheme)
            .GetProperty("LegacyRules", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .GetValue(null)!;
        var canSign = typeof(TlsSigningKey).GetMethods(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Single(method => method.Name == nameof(TlsSigningKey.CanSign));
        return (bool)canSign.Invoke(signingKey, [legacyRules[0]])!;
    }

    // Writes a certificate by subject and thumbprint, never by file path, which differs by operating system.
    private void ArrangeCertificate(string label, X509Certificate2 certificate) =>
        Diagnostics.Arrange(label, $"{certificate.Subject} (thumbprint {certificate.Thumbprint})");

    // Writes what a reader needs from a handshake's ConnectResult to see why it passed or failed.
    private void ActConnectResult(ConnectResult result)
    {
        Diagnostics.Act("exit code", result.ExitCode);
        Diagnostics.Act("error message", result.ErrorMessage ?? "(none)");
        Diagnostics.Act("peer certificates", result.PeerCertificates.Count);
        Diagnostics.Act("application protocol", result.ApplicationProtocol ?? "(none)");
    }

    private static TlsClientOptions Tls12Only(TlsClientOptions options) => options with { MaximumVersion = TlsVersion.Tls12 };

    private static HandBuiltTlsProvider Provider(TlsClientOptions options, bool matchesSchannelBuild, IEchConfigListLookup? echConfigs = null) =>
        new(options, matchesSchannelBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance, echConfigs: echConfigs);

    // A plaintext connection the test watches for disposal; nothing answers on it.
    private static (StreamConnection Plaintext, InMemoryDuplexStream Stream) Unanswered()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        return (new StreamConnection(client, ServerEndPoint), client);
    }

    private string WriteFile(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteFile(string name, byte[] content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static async Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeAsync(
        IHandshakeReportingTlsProvider provider,
        string targetHost,
        RecordingTransferEvents? events = null,
        IReadOnlyList<string>? applicationProtocols = null)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), targetHost, events ?? new RecordingTransferEvents(), false, applicationProtocols ?? [], CancellationToken.None);

        var plaintextDisposed = client.IsDisposed;
        if (plaintextDisposed)
        {
            await IgnoreFailureAsync(serverTask);
        }

        return (result, plaintextDisposed);
    }

    // A server that reads the ClientHello and then sends the answer and closes, or just closes.
    private static async Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeWithServerAnsweringAsync(bool matchesSchannelBuild, byte[]? answer, TlsClientOptions? options = null, RecordingTransferEvents? events = null)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = Task.Run(async () =>
        {
            var header = new byte[5];
            await server.ReadExactlyAsync(header);
            await server.ReadExactlyAsync(new byte[(header[3] << 8) | header[4]]);
            if (answer is not null)
            {
                await server.WriteAsync(answer);
            }

            await server.DisposeAsync();
        });

        var result = await Provider(options ?? Tls12Only(new TlsClientOptions(Insecure: true)), matchesSchannelBuild)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, events ?? new RecordingTransferEvents(), false, Http11, CancellationToken.None);

        await serverTask;
        return (result, client.IsDisposed);
    }

    private static Task RunEchoServerAsync(InMemoryDuplexStream server, SslProtocols protocols, string[]? applicationProtocols = null) => Task.Run(async () =>
    {
        await using var sslStream = new SslStream(server);
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = s_serverCertificate,
            EnabledSslProtocols = protocols,
            ApplicationProtocols = applicationProtocols?.Select(protocol => new SslApplicationProtocol(protocol)).ToList(),
        });

        var buffer = new byte[256];
        int read;
        while ((read = await sslStream.ReadAsync(buffer)) > 0)
        {
            await sslStream.WriteAsync(buffer.AsMemory(0, read));
            await sslStream.FlushAsync();
        }
    });

    private static async Task<byte[]> EchoAsync(IConnection connection, string text)
    {
        await connection.WriteAsync(Encoding.ASCII.GetBytes(text), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        var buffer = new byte[text.Length];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await connection.ReadAsync(buffer.AsMemory(total), CancellationToken.None);
            Assert.AreNotEqual(0, read, "The server closed before echoing every byte.");
            total += read;
        }

        return buffer;
    }

    private static async Task IgnoreFailureAsync(Task serverTask)
    {
        try
        {
            await serverTask;
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException or ObjectDisposedException)
        {
            // The server side fails whenever the client refuses or abandons the handshake.
        }
    }
}
