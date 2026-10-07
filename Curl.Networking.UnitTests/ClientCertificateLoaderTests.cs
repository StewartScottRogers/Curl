using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using Curl.Networking.Fakes;
using Curl.Testing;

using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>
/// Pins that <see cref="ClientCertificateLoader" /> loads an RSA key from PKCS #12 - the
/// Schannel build's file, the OpenSSL build's <c>--cert-type P12</c>, and the PEM path's round
/// trip through PKCS #12 - with private parameters that export on every platform, which TLS
/// 1.0 and 1.1 need to sign their MD5 and SHA-1 block (BL-946).
/// </summary>
[TestClass]
public sealed class ClientCertificateLoaderTests
{
    private const string Passphrase = "secret";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "curl-client-cert-" + Guid.NewGuid().ToString("N"));

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestCleanup]
    public void DeleteFiles()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [TestMethod]
    public void LoadAsSchannelBuild_Pkcs12RsaKey_ExportsItsPrivateParameters()
    {
        Diagnostics.Arrange("file name", "client.p12");
        Diagnostics.Arrange("key", "RSA 2048");
        var path = WriteRsaPkcs12File(_directory);
        Diagnostics.Arrange("file size", new FileInfo(path).Length);

        var (loaded, failure) = ClientCertificateLoader.LoadAsSchannelBuild(path, Passphrase, null, new FakeClientCertificateStore());
        Diagnostics.Act("failure is null", failure is null);
        Diagnostics.Act("loaded subject", loaded?.Subject);

        AssertPrivateParametersExport(loaded, failure);
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_Pkcs12RsaKey_ExportsItsPrivateParameters()
    {
        Diagnostics.Arrange("file name", "client.p12");
        Diagnostics.Arrange("certificate type", "P12");
        var path = WriteRsaPkcs12File(_directory);
        Diagnostics.Arrange("file size", new FileInfo(path).Length);

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(path, Passphrase, null, "P12", null);
        Diagnostics.Act("failure is null", failure is null);
        Diagnostics.Act("loaded subject", loaded?.Subject);

        AssertPrivateParametersExport(loaded, failure);
    }

    [TestMethod]
    public void LoadAsOpenSslBuild_PemRsaKey_ExportsItsPrivateParameters()
    {
        using var rsa = RSA.Create(2048);
        using var certificate = SelfSigned(rsa);
        Directory.CreateDirectory(_directory);
        var certificateFile = Path.Combine(_directory, "cert.pem");
        var keyFile = Path.Combine(_directory, "key.pem");
        File.WriteAllText(certificateFile, certificate.ExportCertificatePem());
        File.WriteAllText(keyFile, rsa.ExportPkcs8PrivateKeyPem());
        Diagnostics.Arrange("certificate file", "cert.pem");
        Diagnostics.Arrange("key file", "key.pem");
        Diagnostics.Arrange("certificate subject", certificate.Subject);
        Diagnostics.Arrange("certificate file length", new FileInfo(certificateFile).Length);

        var (loaded, failure) = ClientCertificateLoader.LoadAsOpenSslBuild(certificateFile, null, keyFile, null, null);
        Diagnostics.Act("failure is null", failure is null);
        Diagnostics.Act("loaded subject", loaded?.Subject);

        AssertPrivateParametersExport(loaded, failure);
    }

    /// <summary>Writes a passphrase-protected PKCS #12 file holding a self-signed RSA certificate and its key.</summary>
    /// <param name="directory">The folder to write <c>client.p12</c> in; created when missing.</param>
    /// <returns>The file's path; its passphrase is <c>secret</c>.</returns>
    internal static string WriteRsaPkcs12File(string directory)
    {
        using var rsa = RSA.Create(2048);
        using var certificate = SelfSigned(rsa);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "client.p12");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, Passphrase));
        return path;
    }

    private static X509Certificate2 SelfSigned(RSA rsa) =>
        new CertificateRequest("CN=client", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1)
            .CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));

    private void AssertPrivateParametersExport(X509Certificate2? loaded, Curl.Protocol.Abstractions.ConnectResult? failure)
    {
        Diagnostics.Assert("failure is null", true, failure is null);
        Assert.IsNull(failure);
        using (loaded)
        {
            using var key = loaded!.GetRSAPrivateKey()!;
            var parameters = key.ExportParameters(includePrivateParameters: true);
            Diagnostics.Act("private exponent length", parameters.D?.Length);
            Diagnostics.Assert("private exponent exported", true, parameters.D is not null);
            Assert.IsNotNull(parameters.D);
        }
    }
}
