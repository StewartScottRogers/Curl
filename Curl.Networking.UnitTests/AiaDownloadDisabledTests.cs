using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// Pins ADR-1296 (BL-5396): the OpenSSL-flavoured build follows no AIA link, so every chain
/// policy it builds disables certificate downloads; the Schannel-flavoured build still may.
/// </summary>
[TestClass]
public sealed class AiaDownloadDisabledTests
{
    [TestMethod]
    public void ReadTrustAnchors_SystemStoreInTheOpenSslBuild_DisablesCertificateDownloads()
    {
        var policy = new ServerCertificateVerification(new TlsClientOptions(), false, TimeProvider.System).ReadTrustAnchors().ChainPolicy;

        Assert.IsTrue(policy?.DisableCertificateDownloads);
    }

    [TestMethod]
    public void ReadTrustAnchors_CaCertificatePathInTheOpenSslBuild_DisablesCertificateDownloads()
    {
        var directory = Directory.CreateTempSubdirectory("capath");
        try
        {
            var options = new TlsClientOptions(CaCertificateDirectory: directory.FullName);
            var policy = new ServerCertificateVerification(options, false, TimeProvider.System).ReadTrustAnchors().ChainPolicy;

            Assert.IsTrue(policy?.DisableCertificateDownloads);
        }
        finally
        {
            directory.Delete(true);
        }
    }

    [TestMethod]
    public void ReadTrustAnchors_CaCertificateInTheOpenSslBuild_DisablesCertificateDownloads()
    {
        using var root = CreateRoot();
        var policy = ReadPolicy(false, root);

        Assert.IsTrue(policy?.DisableCertificateDownloads);
    }

    [TestMethod]
    public void ReadTrustAnchors_InsecureInTheOpenSslBuild_DisablesCertificateDownloads()
    {
        var policy = new ServerCertificateVerification(new TlsClientOptions(Insecure: true), false, TimeProvider.System).ReadTrustAnchors().ChainPolicy;

        Assert.IsTrue(policy?.DisableCertificateDownloads);
    }

    [TestMethod]
    public void ReadTrustAnchors_InsecureInTheSchannelBuild_HasNoChainPolicy()
    {
        var policy = new ServerCertificateVerification(new TlsClientOptions(Insecure: true), true, TimeProvider.System).ReadTrustAnchors().ChainPolicy;

        Assert.IsNull(policy);
    }

    [TestMethod]
    public void ReadTrustAnchors_SystemStoreInTheSchannelBuild_KeepsCertificateDownloads()
    {
        var policy = new ServerCertificateVerification(new TlsClientOptions(), true, TimeProvider.System).ReadTrustAnchors().ChainPolicy;

        Assert.IsFalse(policy?.DisableCertificateDownloads);
    }

    [TestMethod]
    public void ReadTrustAnchors_CaCertificateInTheSchannelBuild_KeepsCertificateDownloads()
    {
        using var root = CreateRoot();
        var policy = ReadPolicy(true, root);

        Assert.IsFalse(policy?.DisableCertificateDownloads);
    }

    [TestMethod]
    public void ReadTrustAnchors_LeafWhoseIntermediateTheServerSentInTheOpenSslBuild_StillVerifies()
    {
        using var root = CreateRoot();
        using var intermediate = CreateIssued("CN=Aia Intermediate", root, isCa: true);
        using var leaf = CreateIssued("CN=aia.test", intermediate, isCa: false);
        var policy = ReadPolicy(false, root)!;

        using var chain = new X509Chain { ChainPolicy = policy.Clone() };
        chain.ChainPolicy.ExtraStore.Add(intermediate);

        Assert.IsTrue(chain.Build(leaf));
    }

    private static X509ChainPolicy? ReadPolicy(bool matchesSchannelBuild, X509Certificate2 root)
    {
        var pem = Encoding.ASCII.GetBytes(root.ExportCertificatePem());
        var options = new TlsClientOptions(CaCertificateBlob: pem, SkipRevocationCheck: true);
        return new ServerCertificateVerification(options, matchesSchannelBuild, TimeProvider.System).ReadTrustAnchors().ChainPolicy;
    }

    private static X509Certificate2 CreateRoot()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Aia Root", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign, true));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
    }

    private static X509Certificate2 CreateIssued(string subject, X509Certificate2 issuer, bool isCa)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(isCa, false, 0, isCa));
        request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
        request.CertificateExtensions.Add(X509AuthorityKeyIdentifierExtension.CreateFromCertificate(issuer, true, false));
        if (!isCa)
        {
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension([new Oid("1.3.6.1.5.5.7.3.1")], false));
        }

        using var signed = request.Create(issuer, new DateTimeOffset(issuer.NotBefore), new DateTimeOffset(issuer.NotAfter), RandomNumberGenerator.GetBytes(8));
        return signed.CopyWithPrivateKey(key);
    }
}
