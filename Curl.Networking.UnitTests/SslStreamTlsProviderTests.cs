using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// Runs <see cref="SslStreamTlsProvider" /> against a server-side <see cref="SslStream" />
/// over an <see cref="InMemoryDuplexStream" /> pair, with a self-signed certificate made
/// here: verification (exit 60), <c>-k</c>, <c>--cacert</c> (exit 77 when its file is
/// unusable), <c>--capath</c>, TLS minimum versions and handshake failures (exit 35), all
/// without a socket. Each failure's message is pinned for both builds ADR-0009 reproduces,
/// the Schannel build and the OpenSSL build, through the internal constructor that names
/// the build. The --cert and --key tests are in SslStreamTlsProviderTests.ClientCertificate.cs.
/// </summary>
[TestClass]
public sealed partial class SslStreamTlsProviderTests
{
    private const string CertificateHost = "localhost";

    private const bool SchannelBuild = true;

    private const bool OpenSslBuild = false;

    private const string UntrustedRootLine =
        "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.";

    private const string CorruptCertificatePem = "-----BEGIN CERTIFICATE-----\nnot base64 !!!\n-----END CERTIFICATE-----\n";

    // Base64, so ImportFromPem decodes it, but not a certificate, so it throws.
    private const string UndecodableCertificatePem = "-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----\n";

    private static readonly IPEndPoint ServerEndPoint = new(IPAddress.Loopback, 443);

    private static X509Certificate2 s_serverCertificate = null!;

    [ClassInitialize]
    public static void CreateServerCertificate(TestContext context)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest($"CN={CertificateHost}", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName(CertificateHost);
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], critical: false));

        using var ephemeral = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

        // A server certificate with an ephemeral key can fail the handshake on Windows;
        // reloading it from PKCS#12 gives it a key the platform can use.
        s_serverCertificate = X509CertificateLoader.LoadPkcs12(ephemeral.Export(X509ContentType.Pkcs12), null);
    }

    [ClassCleanup]
    public static void DisposeCertificates()
    {
        s_serverCertificate.Dispose();
        s_clientCertificate.Dispose();
        s_clientKey.Dispose();
    }

    private string _caFileDirectory = null!;

    [TestInitialize]
    public void CreateCaFileDirectory()
    {
        _caFileDirectory = Path.Combine(Path.GetTempPath(), "Curl.Networking.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_caFileDirectory);
    }

    [TestCleanup]
    public void DeleteCaFileDirectory() => Directory.Delete(_caFileDirectory, recursive: true);

    [TestMethod]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        var exception = Assert.ThrowsExactly<ArgumentNullException>(() => new SslStreamTlsProvider(null!));

        Assert.AreEqual("options", exception.ParamName);
    }

    [TestMethod]
    public void Warnings_WithCaCertificateDirectoryOnThisPlatform_AreThoseOfThePlatformsUsualBuild()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateDirectory: _caFileDirectory));

        Assert.HasCount(OperatingSystem.IsWindows() ? 1 : 0, provider.Warnings);
    }

    [TestMethod]
    public void Warnings_WithCaCertificateDirectoryInTheSchannelBuild_AreTheOneUnwrappedLineSchannelCurlWarns()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateDirectory: _caFileDirectory), SchannelBuild);

        CollectionAssert.AreEqual(
            new[]
            {
                "Warning: ignoring setting the CA path for the proxy, not supported by libcurl with Schannel",
            },
            provider.Warnings.ToArray());
    }

    [TestMethod]
    public void Warnings_WithCaCertificateDirectoryInTheOpenSslBuild_AreEmpty()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(CaCertificateDirectory: _caFileDirectory), OpenSslBuild);

        Assert.IsEmpty(provider.Warnings);
    }

    [TestMethod]
    public void Warnings_WithoutCaCertificateDirectoryInTheSchannelBuild_AreEmpty()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), SchannelBuild);

        Assert.IsEmpty(provider.Warnings);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithNullPlaintext_ThrowsArgumentNullException()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentNullException>(
            async () => await provider.AuthenticateAsClientAsync(null!, CertificateHost, CancellationToken.None));

        Assert.AreEqual("plaintext", exception.ParamName);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithBlankTargetHost_ThrowsArgumentException()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions());

        var exception = await Assert.ThrowsExactlyAsync<ArgumentException>(
            async () => await provider.AuthenticateAsClientAsync(new FakeConnection(), " ", CancellationToken.None));

        Assert.AreEqual("targetHost", exception.ParamName);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenInsecure_ReturnsASecureConnectionThatRoundTripsBytes()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.None);
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true));
        var localEndPoint = new IPEndPoint(IPAddress.Loopback, 50000);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint, localEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        Assert.IsTrue(connection.IsSecure);
        Assert.AreSame(ServerEndPoint, connection.RemoteEndPoint);
        Assert.AreSame(localEndPoint, connection.LocalEndPoint);

        await connection.WriteAsync(Encoding.ASCII.GetBytes("ping"), CancellationToken.None);
        await connection.FlushAsync(CancellationToken.None);
        var echoed = await ReadExactlyAsync(connection, 4);

        Assert.AreEqual("ping", Encoding.ASCII.GetString(echoed));
        await connection.DisposeAsync();
        Assert.IsTrue(client.IsDisposed);
        await IgnoreFailureAsync(serverTask);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUntrustedSelfSignedCertificate_FailsWithPeerFailedVerification()
    {
        var result = await HandshakeAsync(new TlsClientOptions(), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.IsNotNull(result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUntrustedSelfSignedCertificateInTheSchannelBuild_ReportsSecEUntrustedRoot()
    {
        var result = await HandshakeAsync(new TlsClientOptions(), CertificateHost, SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(
            "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.",
            result.Result.ErrorMessage);
    }

    // curl 8.21.0 Schannel -v, untrusted self-signed certificate: failf writes the exit 60 text
    // as an info line after "ALPN: curl offers http/1.1", before "closing connection #0"
    // (measured 2026-10-03, BL-1323).
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUntrustedSelfSignedCertificateInTheSchannelBuild_ReportsSecEUntrustedRootOnceAfterTheFailedHandshake()
    {
        var handshakesBeforeEcho = -1;
        RecordingTransferEvents? events = null;
        events = new RecordingTransferEvents { OnInfo = _ => handshakesBeforeEcho = events!.Handshakes.Count };

        var result = await PinReportingHandshakeAsync(new TlsClientOptions(), events, SchannelBuild, ["http/1.1"]);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.ExitCode);
        Assert.AreEqual(UntrustedRootLine, result.ErrorMessage);
        Assert.AreEqual(UntrustedRootLine, Assert.ContainsSingle(events.Info));
        Assert.AreEqual(1, handshakesBeforeEcho);
        Assert.IsTrue(Assert.ContainsSingle(events.Handshakes).Failed);
    }

    // Neither -k in the Schannel build nor the OpenSSL build refusing the same certificate prints it.
    [TestMethod]
    [DataRow(SchannelBuild, true)]
    [DataRow(OpenSslBuild, false)]
    public async Task AuthenticateAsClientAsync_WithUntrustedSelfSignedCertificateInsecureOrInTheOpenSslBuild_ReportsNoSecEUntrustedRootLine(bool matchesSchannelBuild, bool insecure)
    {
        var events = new RecordingTransferEvents();

        var result = await PinReportingHandshakeAsync(new TlsClientOptions(Insecure: insecure), events, matchesSchannelBuild, ["http/1.1"]);

        CollectionAssert.DoesNotContain(events.Info, UntrustedRootLine);
        if (result.Connection is { } connection)
        {
            await connection.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUntrustedSelfSignedCertificateInTheOpenSslBuild_ReportsVerifyResult18()
    {
        var result = await HandshakeAsync(new TlsClientOptions(), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: self-signed certificate (18)", result.Result.ErrorMessage);
    }

    // BL-150, measured: the OpenSSL build checks the name before the chain, the Schannel
    // build the chain before the name.
    [TestMethod]
    [DataRow(SchannelBuild, "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.")]
    [DataRow(OpenSslBuild, "SSL: no alternative certificate subject name matches target hostname 'wrong.example'")]
    public async Task AuthenticateAsClientAsync_WithUntrustedCertificateNotNamingTheTargetHost_ReportsWhatTheBuildChecksFirst(
        bool matchesSchannelBuild,
        string expected)
    {
        var result = await HandshakeAsync(new TlsClientOptions(), "wrong.example", SslProtocols.None, matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(expected, result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertificateNotNamingTheTargetHost_FailsWithPeerFailedVerification()
    {
        var result = await HandshakeAsync(new TlsClientOptions(), "wrong.example", SslProtocols.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithUntrustedCertificateWhenInsecure_Succeeds()
    {
        var result = await HandshakeAsync(new TlsClientOptions(Insecure: true), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        Assert.IsFalse(result.PlaintextDisposed);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertificateNotNamingTheTargetHostWhenInsecure_Succeeds()
    {
        var result = await HandshakeAsync(new TlsClientOptions(Insecure: true), "wrong.example", SslProtocols.None);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTls13MinimumAgainstATls12OnlyServer_FailsWithSslConnectError()
    {
        await AssertTls13IsAvailableAsync();

        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13),
            CertificateHost,
            SslProtocols.Tls12);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.IsNotNull(result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTls13MinimumAgainstATls12OnlyServerInTheSchannelBuildOnWindows_ReportsTheSecurityStatus()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("Only Windows' Schannel returns a security status; other platforms have no Win32Exception to name.");
        }

        await AssertTls13IsAvailableAsync();

        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13),
            CertificateHost,
            SslProtocols.Tls12,
            SchannelBuild);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual(
            "schannel: next InitializeSecurityContext failed: SEC_E_ILLEGAL_MESSAGE (0x80090326) - The message received was unexpected or badly formatted.",
            result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTls13MinimumAgainstATls12OnlyServerInTheOpenSslBuildOnLinux_ReportsTheOpenSslErrorString()
    {
        if (!OperatingSystem.IsLinux())
        {
            Assert.Inconclusive("Only Linux's SslStream runs on OpenSSL and carries an OpenSSL error string.");
        }

        await AssertTls13IsAvailableAsync();

        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls13),
            CertificateHost,
            SslProtocols.Tls12,
            OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.AreEqual("TLS connect error: error:0A00042E:SSL routines::tlsv1 alert protocol version", result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTls12MinimumAgainstATls12OnlyServer_Succeeds()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, MinimumVersion: TlsVersion.Tls12),
            CertificateHost,
            SslProtocols.Tls12);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    // BL-150 measured both builds against a server that reads the ClientHello and closes.
    [TestMethod]
    [DataRow(SchannelBuild, "schannel: failed to receive handshake, SSL/TLS connection failed")]
    [DataRow(OpenSslBuild, "TLS connect error: error:0A000126:SSL routines::unexpected eof while reading")]
    public async Task AuthenticateAsClientAsync_WhenTheServerClosesMidHandshake_ReportsTheMeasuredLine(
        bool matchesSchannelBuild,
        string expected)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var closeAfterClientHello = Task.Run(async () =>
        {
            await server.ReadExactlyAsync(new byte[1], CancellationToken.None);
            await server.DisposeAsync();
        });
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        await closeAfterClientHello;
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(expected, result.ErrorMessage);
        Assert.IsTrue(client.IsDisposed);
    }

    // curl 8.21.0 Schannel -v, --tlsv1.3 against a TLS 1.2-only server, exit 35: failf writes
    // the text as an info line after the ALPN offer, before "closing connection #0" (measured
    // 2026-10-03, BL-1324); the OpenSSL build's lines are BL-1178's and gain nothing here.
    [TestMethod]
    [DataRow(SchannelBuild, 1)]
    [DataRow(OpenSslBuild, 0)]
    public async Task AuthenticateAsClientAsync_WhenTheServerClosesMidHandshake_EchoesTheFailureOnlyInTheSchannelBuild(
        bool matchesSchannelBuild,
        int expectedEchoes)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var closeAfterClientHello = Task.Run(async () =>
        {
            await server.ReadExactlyAsync(new byte[1], CancellationToken.None);
            await server.DisposeAsync();
        });
        var handshakesAtLastInfo = -1;
        RecordingTransferEvents? events = null;
        events = new RecordingTransferEvents { OnInfo = _ => handshakesAtLastInfo = events!.Handshakes.Count };
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, events, false, ["http/1.1"], CancellationToken.None);

        await closeAfterClientHello;
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(expectedEchoes, events.Info.Count(line => line == result.ErrorMessage));
        if (matchesSchannelBuild)
        {
            Assert.AreEqual("schannel: failed to receive handshake, SSL/TLS connection failed", result.ErrorMessage);
            Assert.AreEqual(result.ErrorMessage, events.Info[^1]);
            Assert.AreEqual(1, handshakesAtLastInfo);
            Assert.IsTrue(Assert.ContainsSingle(events.Handshakes).Failed);
        }
    }

    // BL-369 measured both builds against a server that resets the connection mid-handshake.
    [TestMethod]
    [DataRow(SchannelBuild, SocketError.ConnectionReset, "Recv failure: Connection was reset")]
    [DataRow(SchannelBuild, SocketError.ConnectionAborted, "Recv failure: Connection was aborted")]
    [DataRow(OpenSslBuild, SocketError.ConnectionReset, "Recv failure: Connection reset by peer")]
    public async Task AuthenticateAsClientAsync_WhenTheServerResetsMidHandshake_ReportsTheMeasuredLine(
        bool matchesSchannelBuild,
        SocketError socketError,
        string expected)
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true), matchesSchannelBuild);

        var result = await provider.AuthenticateAsClientAsync(
            new ResettingConnection(socketError), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual(expected, result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenThePlaintextConnectionThrows_FailsWithSslConnectError()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions());

        var result = await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenThePlaintextConnectionThrowsInTheSchannelBuild_ReportsTheHandshakeWasNotReceived()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), SchannelBuild);

        var result = await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual("schannel: failed to receive handshake, SSL/TLS connection failed", result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenThePlaintextConnectionThrowsInTheOpenSslBuild_ReportsTheInnermostMessage()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), OpenSslBuild);

        var result = await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.AreEqual($"TLS connect error: {new NotSupportedException().Message}", result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenCancelledMidHandshake_ThrowsOperationCanceledExceptionAndDisposesThePlaintext()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        using var cancellation = new CancellationTokenSource();
        var provider = new SslStreamTlsProvider(new TlsClientOptions());

        var handshake = provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, cancellation.Token).AsTask();
        await server.ReadExactlyAsync(new byte[1], CancellationToken.None);
        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(async () => await handshake);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingTheServerCertificate_Succeeds(bool matchesSchannelBuild)
    {
        var caFile = WriteCaFile("server.pem", s_serverCertificate.ExportCertificatePem());

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None, matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        Assert.IsFalse(result.PlaintextDisposed);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileButAnotherHostNameInTheSchannelBuild_ReportsTheHostNameCertGetNameStringDidNotMatch()
    {
        var caFile = WriteCaFile("server.pem", s_serverCertificate.ExportCertificatePem());

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile), "wrong.example", SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(
            "schannel: CertGetNameString() failed to match connection hostname (wrong.example) against server certificate names",
            result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileButAnotherHostNameInTheOpenSslBuild_ReportsNoAlternativeNameMatches()
    {
        var caFile = WriteCaFile("server.pem", s_serverCertificate.ExportCertificatePem());

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile), "wrong.example", SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(
            "SSL: no alternative certificate subject name matches target hostname 'wrong.example'",
            result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingAnUnrelatedAuthorityInTheSchannelBuild_ReportsAnUntrustedRoot()
    {
        var caFile = WriteCaFile("unrelated.pem", CreateUnrelatedAuthorityPem());

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("schannel: the certificate or certificate chain is based on an untrusted root", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingAnUnrelatedAuthorityInTheOpenSslBuild_ReportsVerifyResult18()
    {
        var caFile = WriteCaFile("unrelated.pem", CreateUnrelatedAuthorityPem());

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: self-signed certificate (18)", result.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("unrelated")]
    [DataRow("empty")]
    [DataRow("corrupt")]
    [DataRow("directory")]
    public async Task AuthenticateAsClientAsync_WithAnyCaCertificateFileWhenInsecure_Succeeds(string caFileContent)
    {
        var caFile = caFileContent switch
        {
            "unrelated" => WriteCaFile("unrelated.pem", CreateUnrelatedAuthorityPem()),
            "empty" => WriteCaFile("empty.pem", string.Empty),
            "corrupt" => WriteCaFile("corrupt.pem", CorruptCertificatePem),
            _ => _caFileDirectory,
        };

        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, CaCertificateFile: caFile), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileNamingADirectoryInTheSchannelBuild_ReportsTheFileCouldNotBeOpened()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: _caFileDirectory), CertificateHost, SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.Result.ExitCode);
        Assert.AreEqual($"schannel: failed to open CA file '{_caFileDirectory}'", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileNamingADirectoryInTheOpenSslBuild_ReportsNoTrustAnchorsAdded()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: _caFileDirectory), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.Result.ExitCode);
        Assert.AreEqual($"error adding trust anchors from file: {_caFileDirectory}", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("text")]
    [DataRow("corrupt")]
    [DataRow("undecodable")]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingNoParsableCertificateInTheSchannelBuild_ReportsAnUntrustedRoot(
        string caFileContent)
    {
        var caFile = WriteUnusableCaFile(caFileContent);

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("schannel: the certificate or certificate chain is based on an untrusted root", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("text")]
    [DataRow("corrupt")]
    [DataRow("undecodable")]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingNoParsableCertificateInTheOpenSslBuild_FailsWithSslCacertBadfile(
        string caFileContent)
    {
        var caFile = WriteUnusableCaFile(caFileContent);

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.Result.ExitCode);
        Assert.AreEqual($"error adding trust anchors from file: {caFile}", result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingTheServerCertificateAndACorruptBlockInTheSchannelBuild_Succeeds()
    {
        var caFile = WriteCaFile("mixed.pem", s_serverCertificate.ExportCertificatePem() + "\n" + CorruptCertificatePem);

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryHoldingTheServerCertificateInTheOpenSslBuild_Succeeds()
    {
        var caDirectory = WriteCaDirectory(("server.pem", s_serverCertificate.ExportCertificatePem()));

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateDirectory: caDirectory), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        Assert.IsFalse(result.PlaintextDisposed);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryHoldingTheServerCertificateAmongUnusableFilesInTheOpenSslBuild_Succeeds()
    {
        var caDirectory = WriteCaDirectory(
            ("corrupt.pem", CorruptCertificatePem),
            ("undecodable.pem", UndecodableCertificatePem),
            ("server.pem", s_serverCertificate.ExportCertificatePem()));
        Directory.CreateDirectory(Path.Combine(caDirectory, "subdirectory"));

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateDirectory: caDirectory), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryWhoseOnlyFileCannotBeReadInTheOpenSslBuild_AddsNothing()
    {
        var caDirectory = WriteCaDirectory(("server.pem", s_serverCertificate.ExportCertificatePem()));
        await using var lockedFile = new FileStream(
            Path.Combine(caDirectory, "server.pem"), FileMode.Open, FileAccess.Read, FileShare.None);

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateDirectory: caDirectory), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: self-signed certificate (18)", result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryAndAnUnrelatedCaCertificateFileInTheOpenSslBuild_TrustsBoth()
    {
        var caFile = WriteCaFile("unrelated.pem", CreateUnrelatedAuthorityPem());
        var caDirectory = WriteCaDirectory(("server.pem", s_serverCertificate.ExportCertificatePem()));

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile, CaCertificateDirectory: caDirectory),
            CertificateHost,
            SslProtocols.None,
            OpenSslBuild);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryHoldingAnUnrelatedAuthorityInTheOpenSslBuild_ReportsVerifyResult18()
    {
        var caDirectory = WriteCaDirectory(("unrelated.pem", CreateUnrelatedAuthorityPem()));

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateDirectory: caDirectory), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: self-signed certificate (18)", result.Result.ErrorMessage);
    }

    [TestMethod]
    [DataRow("empty")]
    [DataRow("missing")]
    [DataRow("file")]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryThatAddsNothingInTheOpenSslBuild_VerifiesAgainstTheSystemStoreAlone(
        string directoryState)
    {
        var caDirectory = directoryState switch
        {
            "empty" => WriteCaDirectory(),
            "missing" => Path.Combine(_caFileDirectory, "missing"),
            _ => WriteCaFile("server.pem", s_serverCertificate.ExportCertificatePem()),
        };

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateDirectory: caDirectory), CertificateHost, SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("SSL certificate OpenSSL verify result: self-signed certificate (18)", result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryHoldingTheServerCertificateInTheSchannelBuild_IgnoresTheDirectory()
    {
        var caDirectory = WriteCaDirectory(("server.pem", s_serverCertificate.ExportCertificatePem()));

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateDirectory: caDirectory), CertificateHost, SslProtocols.None, SchannelBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(
            "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.",
            result.Result.ErrorMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryHoldingTheServerCertificateButAnotherHostNameInTheOpenSslBuild_ReportsNoAlternativeNameMatches()
    {
        var caDirectory = WriteCaDirectory(("server.pem", s_serverCertificate.ExportCertificatePem()));

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateDirectory: caDirectory), "wrong.example", SslProtocols.None, OpenSslBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual(
            "SSL: no alternative certificate subject name matches target hostname 'wrong.example'",
            result.Result.ErrorMessage);
    }

    // BL-150, measured: the Schannel build ignores --capath beside --cacert as it does alone.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateDirectoryAndAnUnrelatedCaCertificateFileInTheSchannelBuild_TrustsTheFileAlone()
    {
        var caFile = WriteCaFile("unrelated.pem", CreateUnrelatedAuthorityPem());
        var caDirectory = WriteCaDirectory(("server.pem", s_serverCertificate.ExportCertificatePem()));

        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: caFile, CaCertificateDirectory: caDirectory),
            CertificateHost,
            SslProtocols.None,
            SchannelBuild);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.AreEqual("schannel: the certificate or certificate chain is based on an untrusted root", result.Result.ErrorMessage);
    }

    [TestMethod]
    public void VerifyPeer_WithNoChainAndCaCertificateDirectoryAnchors_ReportsTheErrors()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), OpenSslBuild);
        var anchors = new X509Certificate2Collection { s_serverCertificate };

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateNotAvailable, null, CertificateHost, anchors);

        Assert.AreEqual(
            (CurlExitCode.PeerFailedVerification, "SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)"),
            failure);
    }

    [TestMethod]
    public void VerifyPeer_WithAnEmptyChainAndCaCertificateDirectoryAnchors_ReportsTheErrors()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions(), OpenSslBuild);
        var anchors = new X509Certificate2Collection { s_serverCertificate };
        using var chain = new X509Chain();

        var failure = provider.VerifyPeer(SslPolicyErrors.RemoteCertificateChainErrors, chain, CertificateHost, anchors);

        Assert.AreEqual(
            (CurlExitCode.PeerFailedVerification, "SSL certificate OpenSSL verify result: unable to get local issuer certificate (20)"),
            failure);
    }

    private string WriteUnusableCaFile(string content) => content switch
    {
        "empty" => WriteCaFile("empty.pem", string.Empty),
        "text" => WriteCaFile("text.pem", "this is not a certificate\n"),
        "corrupt" => WriteCaFile("corrupt.pem", CorruptCertificatePem),
        _ => WriteCaFile("undecodable.pem", UndecodableCertificatePem),
    };

    private string WriteCaFile(string fileName, string content)
    {
        var path = Path.Combine(_caFileDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private string WriteCaDirectory(params (string FileName, string Content)[] files)
    {
        var directory = Path.Combine(_caFileDirectory, "capath");
        Directory.CreateDirectory(directory);
        foreach (var (fileName, content) in files)
        {
            File.WriteAllText(Path.Combine(directory, fileName), content);
        }

        return directory;
    }

    private static string CreateUnrelatedAuthorityPem()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Unrelated Test Authority", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var authority = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return authority.ExportCertificatePem();
    }

    private static Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeAsync(
        TlsClientOptions options,
        string targetHost,
        SslProtocols serverProtocols) =>
        HandshakeAsync(new SslStreamTlsProvider(options), targetHost, serverProtocols);

    private static Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeAsync(
        TlsClientOptions options,
        string targetHost,
        SslProtocols serverProtocols,
        bool matchesSchannelBuild) =>
        HandshakeAsync(new SslStreamTlsProvider(options, matchesSchannelBuild), targetHost, serverProtocols);

    private static async Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeAsync(
        SslStreamTlsProvider provider,
        string targetHost,
        SslProtocols serverProtocols)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, serverProtocols);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), targetHost, CancellationToken.None);

        var plaintextDisposed = client.IsDisposed;
        if (plaintextDisposed)
        {
            await IgnoreFailureAsync(serverTask);
        }

        return (result, plaintextDisposed);
    }

    private static async Task AssertTls13IsAvailableAsync()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, SslProtocols.Tls13);
        await using var clientStream = new SslStream(client);
        try
        {
            await clientStream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions
            {
                TargetHost = CertificateHost,
                EnabledSslProtocols = SslProtocols.Tls13,
                RemoteCertificateValidationCallback = (_, _, _, _) => true,
            });
        }
        catch (Exception exception) when (exception is AuthenticationException or IOException)
        {
            Assert.Inconclusive($"This operating system cannot run a TLS 1.3 handshake: {exception.Message}");
        }
        finally
        {
            await clientStream.DisposeAsync();
            await IgnoreFailureAsync(serverTask);
        }
    }

    private static Task RunEchoServerAsync(InMemoryDuplexStream server, SslProtocols protocols) => Task.Run(async () =>
    {
        await using var sslStream = new SslStream(server);
        await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
        {
            ServerCertificate = s_serverCertificate,
            EnabledSslProtocols = protocols,
        });

        var buffer = new byte[256];
        int read;
        while ((read = await sslStream.ReadAsync(buffer)) > 0)
        {
            await sslStream.WriteAsync(buffer.AsMemory(0, read));
            await sslStream.FlushAsync();
        }
    });

    private static async Task<byte[]> ReadExactlyAsync(IConnection connection, int count)
    {
        var buffer = new byte[count];
        var total = 0;
        while (total < count)
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
