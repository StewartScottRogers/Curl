using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using CertificateRequest = System.Security.Cryptography.X509Certificates.CertificateRequest;

namespace Curl.Networking;

/// <summary>Pins which <c>--cacert</c> texts <see cref="OpenSslCaFile" /> reads and which it refuses.</summary>
[TestClass]
public sealed class OpenSslCaFileTests
{
    [TestMethod]
    public void Read_TrustedCertificateWithoutSettings_ListsItUnrejected()
    {
        using var root = CreateRoot();

        var file = OpenSslCaFile.Read(Pem("TRUSTED CERTIFICATE", root.RawData));

        Assert.AreEqual(root.Thumbprint, file.Certificates.Single().Thumbprint);
        Assert.IsEmpty(file.RejectedForServerAuthentication);
    }

    [TestMethod]
    public void Read_TrustedCertificateWithAnAliasOnly_ListsItUnrejected()
    {
        using var root = CreateRoot();

        // SEQUENCE { UTF8String "x" }
        var file = OpenSslCaFile.Read(Pem("TRUSTED CERTIFICATE", [.. root.RawData, 0x30, 0x03, 0x0C, 0x01, 0x78]));

        Assert.HasCount(1, file.Certificates);
        Assert.IsEmpty(file.RejectedForServerAuthentication);
    }

    [TestMethod]
    public void Read_BlockWithAnotherLabel_SkipsIt()
    {
        using var root = CreateRoot();

        var file = OpenSslCaFile.Read(Pem("X509 CRL", [0x30, 0x00]) + root.ExportCertificatePem());

        Assert.HasCount(1, file.Certificates);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-----BEGIN TRUSTED CERTIFICATE-----\n!!!!\n-----END TRUSTED CERTIFICATE-----\n")]
    [DataRow("-----BEGIN TRUSTED CERTIFICATE-----\nMAMC\n-----END TRUSTED CERTIFICATE-----\n")]
    [DataRow("-----BEGIN TRUSTED CERTIFICATE-----\nMAA=\n-----END TRUSTED CERTIFICATE-----\n")]
    [DataRow("-----BEGIN CERTIFICATE-----\nMAA=\n-----END CERTIFICATE-----\n")]
    public void Read_NoCertificateOrOneThatCannotBeDecoded_Throws(string pem)
    {
        Assert.Throws<CryptographicException>(() => OpenSslCaFile.Read(pem));
    }

    [TestMethod]
    public void Read_TrustSettingsThatCannotBeDecoded_Throws()
    {
        using var root = CreateRoot();

        // SEQUENCE { SEQUENCE { INTEGER 1 } }: a trusted use that is no object identifier.
        var pem = Pem("TRUSTED CERTIFICATE", [.. root.RawData, 0x30, 0x05, 0x30, 0x03, 0x02, 0x01, 0x01]);

        Assert.Throws<CryptographicException>(() => OpenSslCaFile.Read(pem));
    }

    private static string Pem(string label, byte[] der) => new string(PemEncoding.Write(label, der)) + "\n";

    private static X509Certificate2 CreateRoot()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var from = DateTimeOffset.UtcNow.AddDays(-1);
        return new CertificateRequest("CN=trusted-root", key, HashAlgorithmName.SHA256).CreateSelfSigned(from, from.AddDays(2));
    }
}
