using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Networking.UnitTests;

[TestClass]
public sealed class ServerCertificateVerificationPemRevocationListTests
{
    [TestMethod]
    public void DecodeOrNothing_PemArmouredList_DecodesLikeTheDerList()
    {
        var der = BuildList();
        var pem = Encoding.ASCII.GetBytes(PemEncoding.WriteString("X509 CRL", der));

        var fromPem = ServerCertificateVerification.DecodeOrNothing(pem);
        var fromDer = ServerCertificateVerification.DecodeOrNothing(der);

        Assert.IsNotNull(fromPem);
        Assert.IsNotNull(fromDer);
        Assert.AreEqual(fromDer.ThisUpdate, fromPem.ThisUpdate);
        Assert.IsNotNull(fromDer.NextUpdate);
        Assert.AreEqual(fromDer.NextUpdate, fromPem.NextUpdate);
    }

    [TestMethod]
    public void DecodeOrNothing_PemWithAnotherLabel_IsNothing()
    {
        var pem = Encoding.ASCII.GetBytes(PemEncoding.WriteString("CERTIFICATE", BuildList()));

        Assert.IsNull(ServerCertificateVerification.DecodeOrNothing(pem));
    }

    [TestMethod]
    public void DecodeOrNothing_PemWithBrokenBase64_IsNothing()
    {
        var pem = Encoding.ASCII.GetBytes("-----BEGIN X509 CRL-----\nAB=C\n-----END X509 CRL-----\n");

        Assert.IsNull(ServerCertificateVerification.DecodeOrNothing(pem));
    }

    private static byte[] BuildList()
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=Pem Crl Issuer", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign, true));
        using var issuer = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-2), DateTimeOffset.UtcNow.AddDays(30));
        var now = DateTimeOffset.UtcNow;
        return new CertificateRevocationListBuilder().Build(issuer, 1, now.AddDays(7), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1, now.AddHours(-1));
    }
}
