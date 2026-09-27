using System.Security.Cryptography.X509Certificates;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// <see cref="SslStreamTlsProvider" /> with <c>--cert-type</c>, <c>--key-type</c> and
/// <c>--pass</c> (<see cref="TlsClientOptions.CertificateType" />,
/// <see cref="TlsClientOptions.PrivateKeyType" />, <see cref="TlsClientOptions.Passphrase" />):
/// the Schannel build accepts only PKCS#12, the OpenSSL build PEM, DER and PKCS#12, as
/// ADR-0009 decides, and every refusal is pinned to the text that build of curl printed,
/// measured 2026-09-26 (Schannel: curl 8.21.0; OpenSSL: curl 8.18.0 with OpenSSL 3.5.5).
/// </summary>
public sealed partial class SslStreamTlsProviderTests
{
    private const string OpenSslCertificateFormatHint = ", (no key found, wrong passphrase, or wrong file format?)";

    [TestMethod]
    [DataRow("P12")]
    [DataRow("p12")]
    public async Task AuthenticateAsClientAsync_WithCertTypeP12InTheSchannelBuild_PresentsTheCertificate(string certificateType)
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12, Passphrase));
        var options = ClientCertificate($"{file}:{Passphrase}") with { CertificateType = certificateType };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithPassOpeningAProtectedPkcs12InTheSchannelBuild_PresentsTheCertificate()
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12, Passphrase));
        var options = ClientCertificate(file) with { Passphrase = Passphrase };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithPassReplacingTheCertPassphraseInTheSchannelBuild_FailsWithSslCertProblem()
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12, Passphrase));
        var options = ClientCertificate($"{file}:{Passphrase}") with { Passphrase = "wrong" };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, $"schannel: Failed to import cert file {file}, password is bad");
    }

    [TestMethod]
    [DataRow("PEM")]
    [DataRow("pem")]
    [DataRow("DER")]
    [DataRow("ENG")]
    [DataRow("PROV")]
    [DataRow("FOO")]
    public async Task AuthenticateAsClientAsync_WithCertTypeOtherThanP12InTheSchannelBuild_FailsWithSslCertProblem(string certificateType)
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12, Passphrase));
        var options = ClientCertificate($"{file}:{Passphrase}") with { CertificateType = certificateType };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, $"schannel: certificate format compatibility error for {file}");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertTypePemAndAnEmptyFileInTheSchannelBuild_RefusesTheTypeBeforeReadingTheFile()
    {
        var file = WriteCertificateFile("empty.pem", []);
        var options = ClientCertificate(file) with { CertificateType = "PEM" };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, $"schannel: certificate format compatibility error for {file}");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertTypePemAndAMissingFileInTheSchannelBuild_ReportsTheMissingFile()
    {
        var file = Path.Combine(_caFileDirectory, "missing.pem");
        var options = ClientCertificate(file) with { CertificateType = "PEM" };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, $"schannel: Failed to get certificate location or file for {file}");
    }

    [TestMethod]
    [DataRow("DER")]
    [DataRow("FOO")]
    [DataRow("ENG")]
    public async Task AuthenticateAsClientAsync_WithAnyKeyTypeInTheSchannelBuild_IgnoresIt(string privateKeyType)
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var options = ClientCertificate(file) with { PrivateKeyType = privateKeyType };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, SchannelBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    [DataRow("P12", true)]
    [DataRow("p12", false)]
    public async Task AuthenticateAsClientAsync_WithCertTypeP12InTheOpenSslBuild_PresentsTheCertificate(string certificateType, bool passphraseFromPass)
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12, Passphrase));
        var options = passphraseFromPass
            ? ClientCertificate(Escaped(file)) with { Passphrase = Passphrase }
            : ClientCertificate($"{Escaped(file)}:{Passphrase}");

        var handshake = await HandshakeWithClientCertificateRequestAsync(options with { CertificateType = certificateType }, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertTypeP12AndKeyOptionsInTheOpenSslBuild_IgnoresTheKeyOptions()
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12));
        var options = ClientCertificate(Escaped(file)) with
        {
            CertificateType = "P12",
            PrivateKey = Path.Combine(_caFileDirectory, "missing.key"),
            PrivateKeyType = "DER",
        };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("wrong")]
    public async Task AuthenticateAsClientAsync_WithCertTypeP12AndAPassphraseThatDoesNotOpenItInTheOpenSslBuild_FailsWithSslCertProblem(string? passphrase)
    {
        var file = WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12, Passphrase));
        var options = ClientCertificate($"{Escaped(file)}:{Passphrase}") with { CertificateType = "P12", Passphrase = passphrase };
        if (passphrase is null)
        {
            options = options with { ClientCertificate = Escaped(file) };
        }

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(
            handshake,
            CurlExitCode.SslCertProblem,
            "could not parse PKCS12 file, check password, OpenSSL error error:11800071:PKCS12 routines::mac verify failure");
    }

    [TestMethod]
    [DataRow("missing", "could not open PKCS12 file '{0}'")]
    [DataRow("directory", "error reading PKCS12 file '{0}'")]
    [DataRow("empty", "error reading PKCS12 file '{0}'")]
    [DataRow("pem", "error reading PKCS12 file '{0}'")]
    public async Task AuthenticateAsClientAsync_WithCertTypeP12AndAFileThatIsNotPkcs12InTheOpenSslBuild_FailsWithSslCertProblem(
        string content,
        string expectedFormat)
    {
        var file = content switch
        {
            "missing" => Path.Combine(_caFileDirectory, "missing.p12"),
            "directory" => _caFileDirectory,
            "empty" => WriteCertificateFile("empty.p12", []),
            _ => WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem()),
        };
        var options = ClientCertificate(Escaped(file)) with { CertificateType = "P12" };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(handshake, CurlExitCode.SslCertProblem, string.Format(System.Globalization.CultureInfo.InvariantCulture, expectedFormat, file));
    }

    [TestMethod]
    [DataRow("DER")]
    [DataRow("der")]
    public async Task AuthenticateAsClientAsync_WithCertTypeDerAndAPemKeyInTheOpenSslBuild_PresentsTheCertificate(string certificateType)
    {
        var certificateFile = WriteCertificateFile("client.der", s_clientCertificate.Export(X509ContentType.Cert));
        var keyFile = WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem());
        var options = ClientCertificate(Escaped(certificateFile)) with { CertificateType = certificateType, PrivateKey = keyFile };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    [DataRow("pkcs8", "DER")]
    [DataRow("pkcs1", "der")]
    public async Task AuthenticateAsClientAsync_WithKeyTypeDerInTheOpenSslBuild_PresentsTheCertificate(string keyEncoding, string privateKeyType)
    {
        var certificateFile = WriteCertificateFile("client.der", s_clientCertificate.Export(X509ContentType.Cert));
        var key = keyEncoding == "pkcs8" ? s_clientKey.ExportPkcs8PrivateKey() : s_clientKey.ExportRSAPrivateKey();
        var keyFile = WriteCertificateFile("key.der", key);
        var options = ClientCertificate(Escaped(certificateFile)) with
        {
            CertificateType = "DER",
            PrivateKey = keyFile,
            PrivateKeyType = privateKeyType,
        };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithAPemCertificateAndKeyTypeDerInTheOpenSslBuild_PresentsTheCertificate()
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = WriteCertificateFile("key.der", s_clientKey.ExportPkcs8PrivateKey());
        var options = ClientCertificate(Escaped(certificateFile)) with { PrivateKey = keyFile, PrivateKeyType = "DER" };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    [DataRow("missing", TlsFailureMessages.OpenSslNoSuchFile)]
    [DataRow("directory", TlsFailureMessages.OpenSslIsADirectory)]
    [DataRow("empty", TlsFailureMessages.OpenSslAsn1Lib)]
    [DataRow("pem", TlsFailureMessages.OpenSslWrongTag)]
    [DataRow("p12", TlsFailureMessages.OpenSslWrongTag)]
    [DataRow("key", TlsFailureMessages.OpenSslWrongTag)]
    [DataRow("garbage", TlsFailureMessages.OpenSslNotEnoughData)]
    [DataRow("truncated", TlsFailureMessages.OpenSslNotEnoughData)]
    [DataRow("oneByte", TlsFailureMessages.OpenSslNotEnoughData)]
    [DataRow("lengthCutShort", TlsFailureMessages.OpenSslNotEnoughData)]
    [DataRow("indefiniteLength", TlsFailureMessages.OpenSslWrongTag)]
    [DataRow("overlongLength", TlsFailureMessages.OpenSslWrongTag)]
    [DataRow("longFormOctetString", TlsFailureMessages.OpenSslWrongTag)]
    public async Task AuthenticateAsClientAsync_WithCertTypeDerAndAFileThatIsNotADerCertificateInTheOpenSslBuild_FailsWithSslCertProblem(
        string content,
        string expectedOpenSslError)
    {
        var certificate = s_clientCertificate.Export(X509ContentType.Cert);
        var file = content switch
        {
            "missing" => Path.Combine(_caFileDirectory, "missing.der"),
            "directory" => _caFileDirectory,
            "empty" => WriteCertificateFile("empty.der", []),
            "pem" => WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem()),
            "p12" => WriteCertificateFile("client.p12", s_clientCertificate.Export(X509ContentType.Pkcs12)),
            "key" => WriteCertificateFile("key.der", s_clientKey.ExportPkcs8PrivateKey()),
            "garbage" => WriteCertificateFile("garbage.der", "garbage\n"),
            "truncated" => WriteCertificateFile("truncated.der", certificate[..100]),
            "oneByte" => WriteCertificateFile("one.der", [0x30]),
            "lengthCutShort" => WriteCertificateFile("cut.der", [0x30, 0x84, 0x00]),
            "indefiniteLength" => WriteCertificateFile("indefinite.der", [0x30, 0x80, 0x00, 0x00]),
            "overlongLength" => WriteCertificateFile("overlong.der", [0x30, 0x85, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00]),
            _ => WriteCertificateFile("octets.der", [0x04, 0x81, 0x01, 0x00]),
        };
        var keyFile = WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem());
        var options = ClientCertificate(Escaped(file)) with { CertificateType = "DER", PrivateKey = keyFile };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(
            handshake,
            CurlExitCode.SslCertProblem,
            $"could not load ASN1 client certificate from {file}, OpenSSL error {expectedOpenSslError}{OpenSslCertificateFormatHint}");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertTypeDerAndAFileThatCannotBeReadInTheOpenSslBuild_ReportsPermissionDenied()
    {
        var file = WriteCertificateFile("client.der", s_clientCertificate.Export(X509ContentType.Cert));
        await using var lockedFile = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.None);
        var options = ClientCertificate(Escaped(file)) with { CertificateType = "DER" };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(
            handshake,
            CurlExitCode.SslCertProblem,
            $"could not load ASN1 client certificate from {file}, OpenSSL error {TlsFailureMessages.OpenSslPermissionDenied}{OpenSslCertificateFormatHint}");
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithCertTypeDerAndNoKeyInTheOpenSslBuild_ReadsTheCertificateFileAsAPemKey()
    {
        var file = WriteCertificateFile("client.der", s_clientCertificate.Export(X509ContentType.Cert));
        var options = ClientCertificate(Escaped(file)) with { CertificateType = "DER" };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(handshake, CurlExitCode.BadFunctionArgument, $"unable to set private key file: '{file}' type PEM");
    }

    [TestMethod]
    [DataRow("pem", "DER")]
    [DataRow("garbage", "DER")]
    [DataRow("empty", "der")]
    [DataRow("missing", "DER")]
    [DataRow("otherKey", "DER")]
    public async Task AuthenticateAsClientAsync_WithKeyTypeDerAndAKeyThatDoesNotLoadInTheOpenSslBuild_FailsWithBadFunctionArgumentNamingTheType(
        string keyContent,
        string privateKeyType)
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = keyContent switch
        {
            "pem" => WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem()),
            "garbage" => WriteCertificateFile("key.der", "garbage\n"),
            "empty" => WriteCertificateFile("key.der", []),
            "missing" => Path.Combine(_caFileDirectory, "missing.der"),
            _ => WriteCertificateFile("other.der", OtherKeyDer()),
        };
        var options = ClientCertificate(Escaped(certificateFile)) with { PrivateKey = keyFile, PrivateKeyType = privateKeyType };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(handshake, CurlExitCode.BadFunctionArgument, $"unable to set private key file: '{keyFile}' type {privateKeyType}");
    }

    [TestMethod]
    [DataRow("FOO", CurlExitCode.BadFunctionArgument, "not supported file type 'FOO' for certificate")]
    [DataRow("ENG", CurlExitCode.SslCertProblem, "crypto engine not set, cannot load certificate")]
    [DataRow("prov", CurlExitCode.SslCertProblem, "crypto provider not set, cannot load certificate")]
    public async Task AuthenticateAsClientAsync_WithACertTypeTheOpenSslBuildCannotLoad_FailsWithTheMeasuredMessage(
        string certificateType,
        CurlExitCode expectedExitCode,
        string expectedMessage)
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem());
        var options = ClientCertificate(Escaped(certificateFile)) with { CertificateType = certificateType, PrivateKey = keyFile };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(handshake, expectedExitCode, expectedMessage);
    }

    [TestMethod]
    [DataRow("FOO", CurlExitCode.BadFunctionArgument, "not supported file type for private key")]
    [DataRow("ENG", CurlExitCode.SslCertProblem, "crypto engine not set, cannot load private key")]
    [DataRow("PROV", CurlExitCode.SslCertProblem, "crypto provider not set, cannot load private key")]
    [DataRow("P12", CurlExitCode.SslCertProblem, "file type P12 for private key not supported")]
    public async Task AuthenticateAsClientAsync_WithAKeyTypeTheOpenSslBuildCannotLoad_FailsWithTheMeasuredMessage(
        string privateKeyType,
        CurlExitCode expectedExitCode,
        string expectedMessage)
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = WriteCertificateFile("key.pem", s_clientKey.ExportPkcs8PrivateKeyPem());
        var options = ClientCertificate(Escaped(certificateFile)) with { PrivateKey = keyFile, PrivateKeyType = privateKeyType };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(handshake, expectedExitCode, expectedMessage);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithPassOpeningAnEncryptedPemKeyInTheOpenSslBuild_PresentsTheCertificate()
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = WriteCertificateFile("key.pem", EncryptedKeyPem(Passphrase));
        var options = ClientCertificate(Escaped(certificateFile)) with { PrivateKey = keyFile, Passphrase = Passphrase };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertPresented(handshake);
    }

    [TestMethod]
    public async Task AuthenticateAsClientAsync_WithPassReplacingTheCertPassphraseInTheOpenSslBuild_FailsWithBadFunctionArgument()
    {
        var certificateFile = WriteCertificateFile("client.pem", s_clientCertificate.ExportCertificatePem());
        var keyFile = WriteCertificateFile("key.pem", EncryptedKeyPem(Passphrase));
        var options = ClientCertificate($"{Escaped(certificateFile)}:{Passphrase}") with { PrivateKey = keyFile, Passphrase = "wrong" };

        var handshake = await HandshakeWithClientCertificateRequestAsync(options, OpenSslBuild);

        AssertFailed(handshake, CurlExitCode.BadFunctionArgument, $"unable to set private key file: '{keyFile}' type PEM");
    }

    private static byte[] OtherKeyDer()
    {
        using var otherKey = System.Security.Cryptography.RSA.Create(2048);
        return otherKey.ExportPkcs8PrivateKey();
    }
}
