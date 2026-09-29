using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
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

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 443);

    private static readonly string[] Http11 = ["http/1.1"];

    private static X509Certificate2 s_serverCertificate = null!;

    private static X509Certificate2 s_clientCertificate = null!;

    private string _directory = null!;

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

        Assert.AreEqual("options", Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(null!, TimeProvider.System)).ParamName);
        Assert.AreEqual("timeProvider", Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(options, null!)).ParamName);
        Assert.AreEqual("certificateStore", Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(options, SchannelBuild, TimeProvider.System, null!, random)).ParamName);
        Assert.AreEqual("random", Assert.ThrowsExactly<ArgumentNullException>(() => new HandBuiltTlsProvider(options, SchannelBuild, TimeProvider.System, store, null!)).ParamName);
    }

    [TestMethod]
    public void Constructor_WithMinimumAboveMaximum_ThrowsArgumentException()
    {
        var options = new TlsClientOptions(MinimumVersion: TlsVersion.Tls12, MaximumVersion: TlsVersion.Tls11);

        Assert.ThrowsExactly<ArgumentException>(() => new HandBuiltTlsProvider(options, TimeProvider.System));
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public void Warnings_WithCaCertificateDirectory_AreTheSslStreamProvidersWarnings(bool matchesSchannelBuild)
    {
        var options = new TlsClientOptions(CaCertificateDirectory: _directory);

        CollectionAssert.AreEqual(
            new SslStreamTlsProvider(options, matchesSchannelBuild).Warnings.ToArray(),
            Provider(options, matchesSchannelBuild).Warnings.ToArray());
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithInvalidArguments_Throws()
    {
        var provider = new HandBuiltTlsProvider(new TlsClientOptions(), TimeProvider.System);
        var events = new RecordingTransferEvents();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await provider.AuthenticateAsClientAsync(null!, CertificateHost, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentException>(async () => await provider.AuthenticateAsClientAsync(new FakeConnection(), " ", CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(async () => await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, null!, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, events, false, null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_Tls12UnderInsecure_ReturnsASecureConnectionThatRoundTripsBytes()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50000);
        var provider = new HandBuiltTlsProvider(Tls12Only(new TlsClientOptions(Insecure: true)), TimeProvider.System);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint, localEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        Assert.IsTrue(connection.IsSecure);
        Assert.AreSame(ServerEndPoint, connection.RemoteEndPoint);
        Assert.AreSame(localEndPoint, connection.LocalEndPoint);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, result.PeerCertificates.Single().ToArray());
        Assert.IsNotNull(result.Timings);
        Assert.AreEqual("ping", Encoding.ASCII.GetString(await EchoAsync(connection, "ping")));
        await connection.DisposeAsync();
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

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await using var connection = result.Connection!;
        Assert.AreEqual("ping", Encoding.ASCII.GetString(await EchoAsync(connection, "ping")));
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.AreEqual(SslProtocols.Tls13, handshake.ProtocolVersion);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_256_GCM_SHA384, handshake.CipherSuite);
        await connection.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_Tls12_ReportsTheTrustAndTheHandshakeAsTheSslStreamProviderDoes()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12, Http11);
        var events = new RecordingTransferEvents();
        var options = Tls12Only(new TlsClientOptions(Insecure: true));

        var result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: true, Http11, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.IsInstanceOfType<TlsTrustEvent>(events.TlsEvents[0]);
        Assert.IsFalse(((TlsTrustEvent)events.TlsEvents[0]).VerifiesPeer);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.AreEqual(SslProtocols.Tls12, handshake.ProtocolVersion);
        Assert.AreEqual(TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384, handshake.CipherSuite);
        Assert.AreEqual("http/1.1", handshake.NegotiatedApplicationProtocol);
        CollectionAssert.AreEqual(Http11, handshake.OfferedApplicationProtocols.ToArray());
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.ServerCertificate!.RawData);
        Assert.IsFalse(handshake.CertificateVerified);
        Assert.AreEqual(18L, handshake.CertificateVerifyResult);
        CollectionAssert.AreEqual(s_serverCertificate.RawData, handshake.PeerCertificateChain.Single().RawData);
        Assert.IsTrue(handshake.IsProxy);
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

        var result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, isProxy: false, Http11, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.IsEmpty(handshake.OfferedApplicationProtocols);
        Assert.IsNull(handshake.NegotiatedApplicationProtocol);
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

        var handBuilt = await HandshakeAsync(Provider(options, matchesSchannelBuild), targetHost);
        var sslStream = await HandshakeAsync(new SslStreamTlsProvider(options with { MaximumVersion = TlsVersion.SystemDefault }, matchesSchannelBuild), targetHost);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, handBuilt.Result.ExitCode);
        Assert.AreEqual(sslStream.Result.ExitCode, handBuilt.Result.ExitCode);
        Assert.AreEqual(sslStream.Result.ErrorMessage, handBuilt.Result.ErrorMessage);
        Assert.IsTrue(handBuilt.PlaintextDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingTheServerCertificate_Succeeds(bool matchesSchannelBuild)
    {
        var caFile = WriteFile("ca.pem", s_serverCertificate.ExportCertificatePem());
        var events = new RecordingTransferEvents();

        var result = await HandshakeAsync(Provider(Tls12Only(new TlsClientOptions(CaCertificateFile: caFile)), matchesSchannelBuild), CertificateHost, events);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        var handshake = Assert.ContainsSingle(events.Handshakes);
        Assert.IsTrue(handshake.CertificateVerified);
        Assert.AreEqual(0L, handshake.CertificateVerifyResult);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryHoldingTheServerCertificateInTheOpenSslBuild_Succeeds()
    {
        WriteFile("server.pem", s_serverCertificate.ExportCertificatePem());

        var result = await HandshakeAsync(Provider(Tls12Only(new TlsClientOptions(CaCertificateDirectory: _directory)), OpenSslBuild), CertificateHost);

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

        var result = await Provider(options, matchesSchannelBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);
        var expected = await new SslStreamTlsProvider(options, matchesSchannelBuild).AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.ExitCode);
        Assert.AreEqual(expected.ErrorMessage, result.ErrorMessage);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCiphersInTheSchannelBuild_FailsWithExit59()
    {
        var (plaintext, plaintextStream) = Unanswered();

        var result = await Provider(new TlsClientOptions(Ciphers: "ECDHE-RSA-AES128-GCM-SHA256"), SchannelBuild)
            .AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCipher, result.ExitCode);
        Assert.AreEqual(TlsFailureMessages.SchannelCipherListRefused, result.ErrorMessage);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    [DataRow("nonsense", null, TlsVersion.Tls12)]
    [DataRow(null, "TLS_AES_128_CCM_SHA256", TlsVersion.SystemDefault)]
    [DataRow("ECDHE-ECDSA-AES128-CCM", null, TlsVersion.Tls12)]
    public async Task AuthenticateAsClientAsync_WithCiphersTheOpenSslBuildCannotOffer_FailsWithExit59(string? ciphers, string? tls13Ciphers, TlsVersion maximumVersion)
    {
        var (plaintext, plaintextStream) = Unanswered();
        var options = new TlsClientOptions(Ciphers: ciphers, Tls13Ciphers: tls13Ciphers, MaximumVersion: maximumVersion);

        var result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCipher, result.ExitCode);
        Assert.AreEqual(OpenSslCipherSuites.Unapplied(ciphers, tls13Ciphers), result.ErrorMessage);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCiphersInTheOpenSslBuild_OffersOnlyThoseSuites()
    {
        var events = new RecordingTransferEvents();
        var options = Tls12Only(new TlsClientOptions(Insecure: true, Ciphers: "ECDHE-RSA-AES128-GCM-SHA256"));

        var result = await HandshakeAsync(Provider(options, OpenSslBuild), CertificateHost, events);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode, result.Result.ErrorMessage);
        Assert.AreEqual(TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256, Assert.ContainsSingle(events.Handshakes).CipherSuite);
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

        var result = await Provider(options, OpenSslBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_128_GCM_SHA256, Assert.ContainsSingle(events.Handshakes).CipherSuite);
        await result.Connection!.DisposeAsync();
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAClientCertificateThatDoesNotLoad_FailsWithExit58()
    {
        var (plaintext, plaintextStream) = Unanswered();
        var options = new TlsClientOptions(ClientCertificate: Path.Combine(_directory, "missing.p12"));

        var result = await Provider(options, SchannelBuild).AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerAsksForAClientCertificate_PresentsTheCertCertificate()
    {
        var file = WriteFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var options = Tls12Only(new TlsClientOptions(Insecure: true, ClientCertificate: file));
        var (client, server) = InMemoryDuplexStream.CreatePair();
        X509Certificate? received = null;
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

        var result = await Provider(options, SchannelBuild).AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode, result.ErrorMessage);
        await serverTask;
        Assert.AreEqual(s_clientCertificate.GetCertHashString(), received?.GetCertHashString());
        await result.Connection!.DisposeAsync();
    }

    // The hand-built client's failures in the text each build's curl prints (ADR-0140).
    [TestMethod]
    [DataRow(SchannelBuild, "schannel: failed to receive handshake, SSL/TLS connection failed")]
    [DataRow(OpenSslBuild, "TLS connect error: error:0A000126:SSL routines::unexpected eof while reading")]
    public async Task AuthenticateAsClientAsync_WhenTheServerClosesMidHandshake_ReportsTheMeasuredLine(bool matchesSchannelBuild, string expected)
    {
        var result = await HandshakeWithServerAnsweringAsync(matchesSchannelBuild, answer: null);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild, "schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - This error usually occurs when a fatal SSL/TLS alert is received (e.g. handshake failed). More detail may be available in the Windows System event log.")]
    [DataRow(OpenSslBuild, "TLS connect error: error:0A000410:SSL routines::ssl/tls alert handshake failure")]
    public async Task AuthenticateAsClientAsync_WhenTheServerSendsAHandshakeFailureAlert_ReportsTheMeasuredLine(bool matchesSchannelBuild, string expected)
    {
        var result = await HandshakeWithServerAnsweringAsync(matchesSchannelBuild, answer: [0x15, 0x03, 0x03, 0x00, 0x02, 0x02, 0x28]);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow(SchannelBuild, "schannel: failed to receive handshake, SSL/TLS connection failed")]
    [DataRow(OpenSslBuild, "TLS connect error: error:0A000126:SSL routines::unexpected eof while reading")]
    public async Task AuthenticateAsClientAsync_WhenTheServerClosesMidTls13Handshake_ReportsTheMeasuredLine(bool matchesSchannelBuild, string expected)
    {
        var result = await HandshakeWithServerAnsweringAsync(matchesSchannelBuild, answer: null, new TlsClientOptions(Insecure: true));

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheHandshakeFailsWithACertCertificateLoaded_FailsAndDisposesThePlaintext()
    {
        var file = WriteFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));

        var result = await HandshakeWithServerAnsweringAsync(SchannelBuild, answer: null, Tls12Only(new TlsClientOptions(Insecure: true, ClientCertificate: file)));

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnUnusableCaCertificateFileAndACertCertificate_FailsWithExit77()
    {
        var file = WriteFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var (plaintext, plaintextStream) = Unanswered();

        var result = await Provider(Tls12Only(new TlsClientOptions(CaCertificateFile: _directory, ClientCertificate: file)), SchannelBuild)
            .AuthenticateAsClientAsync(plaintext, CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.ExitCode);
        Assert.IsTrue(plaintextStream.IsDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild, TlsVersion.SystemDefault, TlsVersion.Tls10, "Recv failure: Connection was reset")]
    [DataRow(OpenSslBuild, TlsVersion.SystemDefault, TlsVersion.Tls10, "Recv failure: Connection reset by peer")]
    [DataRow(OpenSslBuild, TlsVersion.Tls11, TlsVersion.Tls11, "Recv failure: Connection reset by peer")]
    [DataRow(OpenSslBuild, TlsVersion.SystemDefault, TlsVersion.SystemDefault, "Recv failure: Connection reset by peer")]
    public async Task AuthenticateAsClientAsync_WhenTheServerResetsMidHandshake_ReportsTheMeasuredLine(
        bool matchesSchannelBuild,
        TlsVersion minimumVersion,
        TlsVersion maximumVersion,
        string expected)
    {
        var result = await Provider(new TlsClientOptions(Insecure: true, MinimumVersion: minimumVersion, MaximumVersion: maximumVersion), matchesSchannelBuild)
            .AuthenticateAsClientAsync(new ResettingConnection(SocketError.ConnectionReset), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenCancelled_ThrowsOperationCanceledExceptionAndDisposesThePlaintext()
    {
        var (client, _) = InMemoryDuplexStream.CreatePair();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await Provider(Tls12Only(new TlsClientOptions()), OpenSslBuild)
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, cancellation.Token));

        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public void ToTlsClientCertificate_TakesAnRsaOrEcdsaKeyAndNothingElse()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var ecdsaCertificate = new CertificateRequest("CN=ec", ecdsa, HashAlgorithmName.SHA256)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        using var withoutKey = X509CertificateLoader.LoadCertificate(s_clientCertificate.RawData);

        Assert.IsNull(HandBuiltTlsProvider.ToTlsClientCertificate(null));
        Assert.IsNull(HandBuiltTlsProvider.ToTlsClientCertificate(withoutKey));
        Assert.IsInstanceOfType<RsaTlsSigningKey>(HandBuiltTlsProvider.ToTlsClientCertificate(s_clientCertificate)!.SigningKey);
        var ecdsaClientCertificate = HandBuiltTlsProvider.ToTlsClientCertificate(ecdsaCertificate)!;
        Assert.IsInstanceOfType<EcdsaTlsSigningKey>(ecdsaClientCertificate.SigningKey);
        CollectionAssert.AreEqual(ecdsaCertificate.RawData, ecdsaClientCertificate.CertificateChain.Single());
    }

    [TestMethod]
    [DataRow("localhost", "localhost")]
    [DataRow("127.0.0.1", null)]
    [DataRow("[::1]", null)]
    public void ServerNameFor_LeavesOutAnIpAddress(string targetHost, string? expected) =>
        Assert.AreEqual(expected, HandBuiltTlsProvider.ServerNameFor(targetHost));

    [TestMethod]
    public void ServerCertificateOf_WithNoCertificate_IsNull() =>
        Assert.IsNull(HandBuiltTlsProvider.ServerCertificateOf([]));

    private static TlsClientOptions Tls12Only(TlsClientOptions options) => options with { MaximumVersion = TlsVersion.Tls12 };

    private static HandBuiltTlsProvider Provider(TlsClientOptions options, bool matchesSchannelBuild) =>
        new(options, matchesSchannelBuild, TimeProvider.System, new FakeClientCertificateStore(), SystemTlsRandomSource.Instance);

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
        RecordingTransferEvents? events = null)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls12);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), targetHost, events ?? new RecordingTransferEvents(), false, [], CancellationToken.None);

        var plaintextDisposed = client.IsDisposed;
        if (plaintextDisposed)
        {
            await IgnoreFailureAsync(serverTask);
        }

        return (result, plaintextDisposed);
    }

    // A server that reads the ClientHello and then sends the answer and closes, or just closes.
    private static async Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeWithServerAnsweringAsync(bool matchesSchannelBuild, byte[]? answer, TlsClientOptions? options = null)
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
            .AuthenticateAsClientAsync(new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

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
