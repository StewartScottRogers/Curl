using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-096: a PKCS#12 <c>--cert</c> file that opens with its passphrase fails in the Schannel
/// build with exit 35 and AcquireCredentialsHandle's <c>SEC_E_UNKNOWN_CREDENTIALS</c>, as
/// curl 8.22.0 and 8.21.0 Schannel do on Windows, before any ClientHello (ADR-1187). The
/// OpenSSL build still loads it.
/// </summary>
[TestClass]
public sealed class Cgr096Tests
{
    private string _directory = null!;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestInitialize]
    public void CreateDirectory() =>
        _directory = Directory.CreateTempSubdirectory("cgr096-").FullName;

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    [TestMethod]
    [DataRow(TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.Tls12)]
    public void Load_Pkcs12WithItsPassphraseInTheSchannelBuild_FailsWithExit35UnknownCredentials(TlsVersion maximumVersion)
    {
        var path = WritePkcs12("secret");
        var options = new TlsClientOptions(Insecure: true, ClientCertificate: path, Passphrase: "secret", CertificateType: "P12", MaximumVersion: maximumVersion);
        Diagnostics.Arrange("--cert", "cli.p12:secret --cert-type P12");
        Diagnostics.Arrange("--tls-max", maximumVersion);

        var (certificate, failure) = ClientCertificateLoader.Load(options, matchesSchannelBuild: true, new FakeClientCertificateStore());
        Diagnostics.Act("exit code", failure?.ExitCode);
        Diagnostics.Act("message", failure?.ErrorMessage);

        Diagnostics.Assert("exit code", CurlExitCode.SslConnectError, failure?.ExitCode);
        Assert.IsNull(certificate);
        Assert.AreEqual(CurlExitCode.SslConnectError, failure?.ExitCode);
        Assert.AreEqual(
            "schannel: AcquireCredentialsHandle failed: SEC_E_UNKNOWN_CREDENTIALS (0x8009030d) - The credentials supplied to the package were not recognized",
            failure?.ErrorMessage);
    }

    [TestMethod]
    public void Load_Pkcs12WithItsPassphraseInTheOpenSslBuild_LoadsTheCertificate()
    {
        var path = WritePkcs12("secret");
        var options = new TlsClientOptions(Insecure: true, ClientCertificate: path, Passphrase: "secret", CertificateType: "P12");
        Diagnostics.Arrange("--cert", "cli.p12:secret --cert-type P12, OpenSSL build");

        var (certificate, failure) = ClientCertificateLoader.Load(options, matchesSchannelBuild: false, new FakeClientCertificateStore());
        using (certificate)
        {
            Diagnostics.Act("loaded", certificate?.Subject);

            Diagnostics.Assert("loaded with its key", true, certificate?.HasPrivateKey);
            Assert.IsNull(failure);
            Assert.IsTrue(certificate?.HasPrivateKey);
        }
    }

    private string WritePkcs12(string passphrase)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=cli", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        var path = Path.Combine(_directory, "cli.p12");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, passphrase));
        return path;
    }
}
