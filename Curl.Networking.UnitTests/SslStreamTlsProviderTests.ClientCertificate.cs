using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="SslStreamTlsProvider" /> with <c>--cert</c> and <c>--key</c>
/// (<see cref="TlsClientOptions.ClientCertificate" />, <see cref="TlsClientOptions.PrivateKey" />):
/// a server-side <see cref="SslStream" /> that asks for a client certificate, over an
/// <see cref="InMemoryDuplexStream" /> pair, records the one it receives. Each build
/// ADR-0009 reproduces loads its own format, PKCS#12 for Schannel and PEM for OpenSSL, and
/// every failure is pinned to the text that build of curl printed, measured 2026-09-26.
/// Certificate files are written to the per-test temporary directory.
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    private const string Passphrase = "secret";

    private static readonly RSA s_clientKey = RSA.Create(2048);

    private static readonly X509Certificate2 s_clientCertificate = CreateClientCertificate(s_clientKey);

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithoutClientCertificate_PresentsNone(bool matchesSchannelBuild)
    {
        var handshake = await HandshakeWithClientCertificateRequestAsync(new TlsClientOptions(Insecure: true), matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, handshake.Result.ExitCode);
        Assert.IsNull(handshake.Received);
    }

    // BL-254: on Windows SslStream caches the credential handle of a handshake that
    // presented a certificate under the key of one that presents none, so every later
    // handshake without --cert in the process presented it too. Real curl presents none.
    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithoutClientCertificateAfterAHandshakeThatPresentedOne_PresentsNone(bool matchesSchannelBuild)
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        AssertPresented(await HandshakeWithClientCertificateRequestAsync(ClientCertificate(file), SchannelBuild));

        var handshake = await HandshakeWithClientCertificateRequestAsync(new TlsClientOptions(Insecure: true), matchesSchannelBuild);

        Assert.AreEqual(CurlExitCode.Ok, handshake.Result.ExitCode);
        Assert.IsNull(handshake.Received);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithPkcs12AndItsPassphraseInTheSchannelBuild_PresentsTheCertificate()
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12, Passphrase));

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate($"{file}:{Passphrase}"), SchannelBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithPkcs12WithoutAPassphraseInTheSchannelBuild_PresentsTheCertificate()
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(file), SchannelBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithPkcs12AndAMissingKeyFileInTheSchannelBuild_IgnoresTheKeyFile()
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var options = ClientCertificate(file) with { PrivateKey = Path.Combine(_caFileDirectory, "missing.pem") };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithPemCertificateAndKeyFilesInTheOpenSslBuild_PresentsTheCertificate()
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem());

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(Escaped(certificateFile)) with { PrivateKey = keyFile }, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAPemFileHoldingCertificateAndKeyInTheOpenSslBuild_PresentsTheCertificate()
    {
        var file = WriteCertificateFile("both.pem", s_clientCertificate.ExportCertificatePem() + "\n" + s_clientKey.ExportPkcs8PrivateKeyPem());

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(Escaped(file)), OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnEncryptedPemKeyAndItsPassphraseInTheOpenSslBuild_PresentsTheCertificate()
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = WriteCertificateFile("key.pem", EncryptedKeyPem(Passphrase));

        var handshake = await HandshakeWithClientCertificateRequestAsync(
            ClientCertificate($"{Escaped(certificateFile)}:{Passphrase}") with { PrivateKey = keyFile }, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAPassphraseAndAnUnencryptedPemKeyInTheOpenSslBuild_PresentsTheCertificate()
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem());

        var handshake = await HandshakeWithClientCertificateRequestAsync(
            ClientCertificate($"{Escaped(certificateFile)}:{Passphrase}") with { PrivateKey = keyFile }, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    [DataRow(SchannelBuild)]
    [DataRow(OpenSslBuild)]
    public async Task AuthenticateAsClientAsync_WithAMissingClientCertificateFile_FailsWithSslCertProblem(bool matchesSchannelBuild)
    {
        var file = Path.Combine(_caFileDirectory, "nonexist.p12");

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate($"{Escaped(file)}:{Passphrase}"), matchesSchannelBuild);

        AssertFailed(
            handshake,
            CurlExitCode.SslCertProblem,
            matchesSchannelBuild
                ? $"schannel: Failed to get certificate location or file for {file}"
                : $"could not load PEM client certificate from {file}, OpenSSL error error:80000002:system library::No such file or directory, (no key found, wrong passphrase, or wrong file format?)");
    }

    // BL-1088: curl 8.21.0's Schannel build writes "schannel: disabled automatic use of client
    // certificate" before it fails on the certificate, and never gets as far as the SNI line.
    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAMissingClientCertificateFileToAnIpAddressInTheSchannelBuild_ReportsTheTrustWithoutTheIpAddressBeforeFailingWithExit58()
    {
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, ClientCertificate: Path.Combine(_caFileDirectory, "nosuch.pem"));

        var result = await new SslStreamTlsProvider(options, SchannelBuild).AuthenticateAsClientAsync(new FakeConnection(), "127.0.0.1", events, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        var trust = (TlsTrustEvent)Assert.ContainsSingle(events.TlsEvents);
        Assert.IsFalse(trust.UsesAutomaticClientCertificate);
        Assert.IsFalse(trust.TargetsIpAddress);
        Assert.IsFalse(trust.VerifiesPeer);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAMissingClientCertificateFileInTheOpenSslBuild_ReportsNoTrust()
    {
        var events = new RecordingTransferEvents();
        var options = new TlsClientOptions(Insecure: true, ClientCertificate: Path.Combine(_caFileDirectory, "nosuch.pem"));

        var result = await new SslStreamTlsProvider(options, OpenSslBuild).AuthenticateAsClientAsync(new FakeConnection(), "127.0.0.1", events, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.SslCertProblem, result.ExitCode);
        Assert.IsEmpty(events.TlsEvents);
    }

    [TestMethod]
    [DataRow(SchannelBuild, "schannel: Failed to get certificate location or file for ")]
    [DataRow(OpenSslBuild, "could not load PEM client certificate from , OpenSSL error error:80000002:system library::No such file or directory, (no key found, wrong passphrase, or wrong file format?)")]
    public async Task AuthenticateAsClientAsync_WithAnEmptyFileNameBeforeThePassphrase_FailsWithSslCertProblem(
        bool matchesSchannelBuild,
        string expectedMessage)
    {
        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate($":{Passphrase}"), matchesSchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, expectedMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithADirectoryAsClientCertificateInTheSchannelBuild_FailsWithSslCertProblem()
    {
        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(_caFileDirectory), SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, $"schannel: Failed to get certificate location or file for {_caFileDirectory}");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAnEmptyClientCertificateFileInTheSchannelBuild_FailsWithSslCertProblem()
    {
        var file = WriteCertificateFile("empty.p12", []);

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(file), SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, $"schannel: Failed to read cert file {file}");
    }

    [TestMethod]
    [DataRow("pem")]
    [DataRow("der")]
    [DataRow("garbage")]
    public async Task AuthenticateAsClientAsync_WithAClientCertificateThatIsNotPkcs12InTheSchannelBuild_FailsWithSslCertProblem(string format)
    {
        var file = format switch
        {
            "pem" => WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem()),
            "der" => WriteCertificateFile("client.der", s_clientCertificate.Export(X509ContentType.Cert)),
            _ => WriteCertificateFile("garbage.p12", "garbage\n"),
        };
        var keyFile = WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem());

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(file) with { PrivateKey = keyFile }, SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, $"schannel: Failed to import cert file {file}, last error is 0x80092002");
    }

    [TestMethod]
    [DataRow(":wrong")]
    [DataRow("")]
    public async Task AuthenticateAsClientAsync_WithAPkcs12PassphraseThatDoesNotOpenItInTheSchannelBuild_FailsWithSslCertProblem(string passphraseSuffix)
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12, Passphrase));

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(file + passphraseSuffix), SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, $"schannel: Failed to import cert file {file}, password is bad");
    }

    [TestMethod]
    [DataRow("p12")]
    [DataRow("der")]
    [DataRow("empty")]
    [DataRow("key")]
    [DataRow("undecodable")]
    [DataRow("directory")]
    public async Task AuthenticateAsClientAsync_WithAClientCertificateThatIsNotPemInTheOpenSslBuild_FailsWithSslCertProblem(string content)
    {
        var file = content switch
        {
            "p12" => WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12)),
            "der" => WriteCertificateFile("client.der", s_clientCertificate.Export(X509ContentType.Cert)),
            "empty" => WriteCertificateFile("empty.pem", []),
            "key" => WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem()),
            "undecodable" => WriteCertificateFile("undecodable.pem", UndecodableCertificatePem),
            _ => _caFileDirectory,
        };

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(Escaped(file)), OpenSslBuild);

        AssertFailed(
            handshake,
            CurlExitCode.SslCertProblem,
            $"could not load PEM client certificate from {file}, OpenSSL error error:0480006C:PEM routines::no start line, (no key found, wrong passphrase, or wrong file format?)");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAClientCertificateFileThatCannotBeReadInTheOpenSslBuild_FailsWithSslCertProblem()
    {
        var file = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        await using var lockedFile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(Escaped(file)), OpenSslBuild);

        AssertFailed(
            handshake,
            CurlExitCode.SslCertProblem,
            $"could not load PEM client certificate from {file}, OpenSSL error error:0480006C:PEM routines::no start line, (no key found, wrong passphrase, or wrong file format?)");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAPemCertificateAndNoKeyInTheOpenSslBuild_FailsWithBadFunctionArgumentNamingTheCertificateFile()
    {
        var file = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(Escaped(file)), OpenSslBuild);

        AssertFailed(handshake, CurlExitCode.BadFunctionArgument, $"unable to set private key file: '{file}' type PEM");
    }

    [TestMethod]
    [DataRow("missing", null)]
    [DataRow("directory", null)]
    [DataRow("emptyName", null)]
    [DataRow("otherKey", null)]
    [DataRow("encrypted", null)]
    [DataRow("encrypted", "wrong")]
    public async Task AuthenticateAsClientAsync_WithAPrivateKeyThatDoesNotLoadInTheOpenSslBuild_FailsWithBadFunctionArgument(
        string keyFileState,
        string? passphrase)
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = keyFileState switch
        {
            "missing" => Path.Combine(_caFileDirectory, "nonexist.pem"),
            "directory" => _caFileDirectory,
            "emptyName" => string.Empty,
            "otherKey" => WriteCertificateFile("other.pem", OtherKeyPem()),
            _ => WriteCertificateFile("key.pem", EncryptedKeyPem(Passphrase)),
        };
        var value = passphrase is null ? Escaped(certificateFile) : $"{Escaped(certificateFile)}:{passphrase}";

        var handshake = await HandshakeWithClientCertificateRequestAsync(ClientCertificate(value) with { PrivateKey = keyFile }, OpenSslBuild);

        AssertFailed(handshake, CurlExitCode.BadFunctionArgument, $"unable to set private key file: '{keyFile}' type PEM");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAClientCertificateAndAnUnusableCaCertificateFile_FailsWithSslCacertBadfile()
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var options = new TlsClientOptions(CaCertificateFile: _caFileDirectory, ClientCertificate: file);

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCacertBadfile, $"schannel: failed to open CA file '{_caFileDirectory}'");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAClientCertificateAndAnUntrustedServer_FailsWithPeerFailedVerification()
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));

        var handshake = await HandshakeWithClientCertificateRequestAsync(new TlsClientOptions(ClientCertificate: file), SchannelBuild);

        AssertFailed(
            handshake,
            CurlExitCode.PeerFailedVerification,
            "schannel: SEC_E_UNTRUSTED_ROOT (0x80090325) - The certificate chain was issued by an authority that is not trusted.");
    }

    private static X509Certificate2 CreateClientCertificate(RSA key)
    {
        var request = new CertificateRequest("CN=Curl Test Client", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.2")], critical: false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
    }

    private static TlsClientOptions ClientCertificate(string value) => new(Insecure: true, ClientCertificate: value);

    // The OpenSSL build splits at a drive letter's colon, so on Windows a temporary path is
    // given to it with the colon escaped, as a user of that build would write it.
    private static string Escaped(string path) => path.Replace(":", @"\:", StringComparison.Ordinal);

    private static string EncryptedKeyPem(string passphrase) =>
        s_clientKey.ExportEncryptedPkcs8PrivateKeyPem(
            passphrase,
            new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000));

    private static string OtherKeyPem()
    {
        using var otherKey = RSA.Create(2048);
        return otherKey.ExportPkcs8PrivateKeyPem();
    }

    private static void AssertPresented((ConnectResult Result, X509Certificate? Received, bool PlaintextDisposed) handshake)
    {
        Assert.AreEqual(CurlExitCode.Ok, handshake.Result.ExitCode, handshake.Result.ErrorMessage);
        Assert.IsNotNull(handshake.Received);
        Assert.AreEqual(s_clientCertificate.GetCertHashString(), handshake.Received.GetCertHashString());
    }

    private static void AssertFailed(
        (ConnectResult Result, X509Certificate? Received, bool PlaintextDisposed) handshake,
        CurlExitCode expectedExitCode,
        string expectedMessage)
    {
        Assert.AreEqual(expectedExitCode, handshake.Result.ExitCode);
        Assert.AreEqual(expectedMessage, handshake.Result.ErrorMessage);
        Assert.IsNull(handshake.Result.Connection);
        Assert.IsTrue(handshake.PlaintextDisposed);
    }

    private string WriteCertificateFile(string fileName, string content) => WriteCertificateFile(fileName, System.Text.Encoding.ASCII.GetBytes(content));

    private string WriteCertificateFile(string fileName, byte[] content)
    {
        var path = Path.Combine(_caFileDirectory, fileName);
        File.WriteAllBytes(path, content);
        return path;
    }

    // The server asks for a client certificate and accepts whatever it gets, or none, so
    // the test sees exactly what the provider presented.
    private static async Task<(ConnectResult Result, X509Certificate? Received, bool PlaintextDisposed)> HandshakeWithClientCertificateRequestAsync(
        TlsClientOptions options,
        bool matchesSchannelBuild,
        IClientCertificateStore? certificateStore = null)
    {
        var (client, server) = InMemoryDuplexStream.CreatePair();
        X509Certificate? received = null;
        var serverTask = Task.Run(async () =>
        {
            await using var sslStream = new SslStream(server);
            await sslStream.AuthenticateAsServerAsync(new SslServerAuthenticationOptions
            {
                ServerCertificate = s_serverCertificate,
                ClientCertificateRequired = true,
                RemoteCertificateValidationCallback = (_, certificate, _, _) =>
                {
                    received = certificate is null ? null : X509CertificateLoader.LoadCertificate(certificate.GetRawCertData());
                    return true;
                },
            });
        });
        var provider = certificateStore is null
            ? new SslStreamTlsProvider(options, matchesSchannelBuild)
            : new SslStreamTlsProvider(options, matchesSchannelBuild, TimeProvider.System, certificateStore);

        var result = await provider.AuthenticateAsClientAsync(
            new StreamConnection(client, ServerEndPoint), CertificateHost, CancellationToken.None);

        var plaintextDisposed = client.IsDisposed;
        if (result.Connection is not null)
        {
            // In TLS 1.3 the server reads the client's certificate after the client finishes.
            await serverTask;
            await result.Connection.DisposeAsync();
        }
        else
        {
            await IgnoreFailureAsync(serverTask);
        }

        return (result, received, plaintextDisposed);
    }
}
