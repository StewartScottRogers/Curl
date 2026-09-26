using System.Net;
using System.Net.Security;
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
/// unusable), TLS minimum versions and handshake failures (exit 35), all without a socket.
/// </summary>
[TestClass]
public sealed class SslStreamTlsProviderTests
{
    private const string CertificateHost = "localhost";

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
    public static void DisposeServerCertificate() => s_serverCertificate.Dispose();

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

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        await using var connection = result.Connection!;
        Assert.IsTrue(connection.IsSecure);
        Assert.AreSame(ServerEndPoint, connection.RemoteEndPoint);

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
            new TlsClientOptions(Insecure: true, MinimumVersion: TlsMinimumVersion.Tls13),
            CertificateHost,
            SslProtocols.Tls12);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.Result.ExitCode);
        Assert.IsNotNull(result.Result.ErrorMessage);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithTls12MinimumAgainstATls12OnlyServer_Succeeds()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(Insecure: true, MinimumVersion: TlsMinimumVersion.Tls12),
            CertificateHost,
            SslProtocols.Tls12);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenTheServerClosesMidHandshake_FailsWithSslConnectError()
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var closeAfterClientHello = Task.Run(async () =>
        {
            await server.ReadExactlyAsync(new byte[1], CancellationToken.None);
            await server.DisposeAsync();
        });
        var provider = new SslStreamTlsProvider(new TlsClientOptions(Insecure: true));

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        await closeAfterClientHello;
        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
        Assert.IsTrue(client.IsDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WhenThePlaintextConnectionThrows_FailsWithSslConnectError()
    {
        var provider = new SslStreamTlsProvider(new TlsClientOptions());

        var result = await provider.AuthenticateAsClientAsync(new FakeConnection(), CertificateHost, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslConnectError, result.ExitCode);
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
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingTheServerCertificate_Succeeds()
    {
        var caFile = WriteCaFile("server.pem", s_serverCertificate.ExportCertificatePem());

        var result = await HandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.Ok, result.Result.ExitCode);
        Assert.IsFalse(result.PlaintextDisposed);
        await result.Result.Connection!.DisposeAsync();
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingTheServerCertificateButAnotherHostName_FailsWithPeerFailedVerification()
    {
        var caFile = WriteCaFile("server.pem", s_serverCertificate.ExportCertificatePem());

        var result = await HandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), "wrong.example", SslProtocols.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingAnUnrelatedAuthority_FailsWithPeerFailedVerification()
    {
        var caFile = WriteCaFile("unrelated.pem", CreateUnrelatedAuthorityPem());

        var result = await HandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.IsTrue(result.PlaintextDisposed);
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
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileNamingADirectory_FailsWithSslCacertBadfile()
    {
        var result = await HandshakeAsync(
            new TlsClientOptions(CaCertificateFile: _caFileDirectory), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.Result.ExitCode);
        Assert.IsNotNull(result.Result.ErrorMessage);
        StringAssert.Contains(result.Result.ErrorMessage, _caFileDirectory);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileHoldingACorruptCertificateBlock_FailsWithSslCacertBadfile()
    {
        var caFile = WriteCaFile("corrupt.pem", CorruptCertificatePem);

        var result = await HandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.SslCacertBadfile, result.Result.ExitCode);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithEmptyCaCertificateFile_FailsWithPeerFailedVerification()
    {
        var caFile = WriteCaFile("empty.pem", string.Empty);

        var result = await HandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCaCertificateFileOfNonPemText_FailsWithPeerFailedVerification()
    {
        var caFile = WriteCaFile("text.pem", "this is not a certificate\n");

        var result = await HandshakeAsync(new TlsClientOptions(CaCertificateFile: caFile), CertificateHost, SslProtocols.None);

        Assert.AreEqual(CurlExitCode.PeerFailedVerification, result.Result.ExitCode);
        Assert.IsTrue(result.PlaintextDisposed);
    }

    private const string CorruptCertificatePem = "-----BEGIN CERTIFICATE-----\nnot base64 !!!\n-----END CERTIFICATE-----\n";

    private string WriteCaFile(string fileName, string content)
    {
        var path = Path.Combine(_caFileDirectory, fileName);
        File.WriteAllText(path, content);
        return path;
    }

    private static string CreateUnrelatedAuthorityPem()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Unrelated Test Authority", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        using var authority = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return authority.ExportCertificatePem();
    }

    private static async Task<(ConnectResult Result, bool PlaintextDisposed)> HandshakeAsync(
        TlsClientOptions options,
        string targetHost,
        SslProtocols serverProtocols)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        var serverTask = RunEchoServerAsync(server, serverProtocols);
        var provider = new SslStreamTlsProvider(options);

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
