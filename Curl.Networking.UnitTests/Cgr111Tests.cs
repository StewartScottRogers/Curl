using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// CGR-111 (BL-3640, ADR-0836): the OpenSSL build reads a <c>--capath</c> or <c>SSL_CERT_DIR</c>
/// directory as OpenSSL's hashed directory lookup does, trusting only the certificates in files
/// named <c>&lt;subject hash&gt;.&lt;n&gt;</c> (as <c>openssl rehash</c> names them), so a
/// certificate copied in under any other name is not trusted (exit 60 with
/// <c>self-signed certificate (18)</c>, as curl 8.18.0's OpenSSL build).
/// </summary>
[TestClass]
public sealed class Cgr111Tests
{
    private const bool OpenSslBuild = false;

    private static readonly X509Certificate2 s_serverCertificate = CreateSelfSigned("CN=localhost");

    private string _root = null!;

    [TestInitialize]
    public void CreateRoot()
    {
        _root = Path.Combine(Path.GetTempPath(), "Curl.Networking.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void DeleteRoot() => Directory.Delete(_root, recursive: true);

    [TestMethod]
    public void ReadTrustAnchors_CapathWhoseCertificateFileIsNotHashNamed_TrustsNothing()
    {
        var plain = WriteDirectory("plain", ("server-ca.crt", s_serverCertificate));

        var anchors = AnchorsFor(plain);

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void ReadTrustAnchors_CapathWhoseCertificateFileIsHashNamed_TrustsIt()
    {
        var hashdir = WriteDirectory("hashdir", (HashName(s_serverCertificate, 0), s_serverCertificate));

        var anchors = AnchorsFor(hashdir);

        Assert.HasCount(1, anchors);
        Assert.AreEqual(s_serverCertificate.Thumbprint, anchors[0].Thumbprint);
    }

    [TestMethod]
    public void ReadTrustAnchors_SslCertDirListWithAMissingDirectoryThenAPlainOne_TrustsNothing()
    {
        var plain = WriteDirectory("plain", ("server-ca.crt", s_serverCertificate));

        var anchors = AnchorsFor($"{Path.Combine(_root, "nope")}{Path.PathSeparator}{plain}");

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void ReadTrustAnchors_SslCertDirListWithAMissingDirectoryThenAHashedOne_TrustsTheHashedCertificate()
    {
        var hashdir = WriteDirectory("hashdir", (HashName(s_serverCertificate, 0), s_serverCertificate));

        var anchors = AnchorsFor($"{Path.Combine(_root, "nope")}{Path.PathSeparator}{hashdir}{Path.PathSeparator}");

        Assert.HasCount(1, anchors);
    }

    [TestMethod]
    public void ReadTrustAnchors_HashNamedFilesCountingOnFromZero_TrustsEach()
    {
        using var second = CreateSelfSigned("CN=localhost");
        var hashdir = WriteDirectory(
            "hashdir", (HashName(s_serverCertificate, 0), s_serverCertificate), (HashName(second, 1), second));

        var anchors = AnchorsFor(hashdir);

        Assert.HasCount(2, anchors);
    }

    [TestMethod]
    public void ReadTrustAnchors_HashNamedFileNumberedOneWithoutZero_TrustsNothing()
    {
        var hashdir = WriteDirectory("hashdir", (HashName(s_serverCertificate, 1), s_serverCertificate));

        var anchors = AnchorsFor(hashdir);

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    [DataRow("0000000g.0")]
    [DataRow("0000000A.0")]
    [DataRow("00000000.x")]
    [DataRow("00000000.")]
    [DataRow("000000000.0")]
    public void ReadTrustAnchors_FileNameNotAHashName_TrustsNothing(string fileName)
    {
        var hashdir = WriteDirectory("hashdir", (fileName, s_serverCertificate));

        var anchors = AnchorsFor(hashdir);

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void ReadTrustAnchors_HashNamedFileHoldingACertificateOfAnotherSubject_TrustsNothing()
    {
        using var other = CreateSelfSigned("CN=another.example");
        var hashdir = WriteDirectory("hashdir", (HashName(s_serverCertificate, 0), other));

        var anchors = AnchorsFor(hashdir);

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    public void ReadTrustAnchors_InTheSchannelBuild_ReadsNoCapath()
    {
        var hashdir = WriteDirectory("hashdir", (HashName(s_serverCertificate, 0), s_serverCertificate));

        var anchors = new ServerCertificateVerification(new TlsClientOptions(CaCertificateDirectory: hashdir), true, TimeProvider.System)
            .ReadTrustAnchors().AnchorsBesideSystemStore;

        Assert.IsEmpty(anchors);
    }

    [TestMethod]
    [DataRow("4042bcee", "US", "Internet Security Research Group", "ISRG Root X1")]
    [DataRow("4042bcee", "us", "  internet   SECURITY research group ", "isrg\troot x1")]
    public void Of_IsrgRootX1_IsOpenSslsHash(string expected, string country, string organization, string commonName)
    {
        // The builder encodes the last name added first.
        var builder = new X500DistinguishedNameBuilder();
        builder.AddCommonName(commonName);
        builder.AddOrganizationName(organization);
        builder.AddCountryOrRegion(country);

        Assert.AreEqual(expected, OpenSslSubjectNameHash.Of(builder.Build()));
    }

    [TestMethod]
    public void Of_LocalhostCertificateSubject_IsOpenSslsHash()
    {
        // openssl x509 -noout -subject_hash for a certificate with -subj "/CN=localhost".
        Assert.AreEqual("ce275665", OpenSslSubjectNameHash.Of(s_serverCertificate.SubjectName));
    }

    [TestMethod]
    public void Of_BmpStringValue_HashesAsTheSameUtf8Text()
    {
        var printable = NameWithCommonName(writer => writer.WriteCharacterString(UniversalTagNumber.PrintableString, "Example"));
        var bmp = NameWithCommonName(writer => writer.WriteCharacterString(UniversalTagNumber.BMPString, " EXAMPLE "));

        Assert.AreEqual(OpenSslSubjectNameHash.Of(printable), OpenSslSubjectNameHash.Of(bmp));
    }

    [TestMethod]
    public void Of_ValueThatIsNotAString_IsHashedAsEncoded()
    {
        var integer = NameWithCommonName(writer => writer.WriteInteger(7));
        var octets = NameWithCommonName(writer => writer.WriteOctetString([7]));

        Assert.AreNotEqual(OpenSslSubjectNameHash.Of(integer), OpenSslSubjectNameHash.Of(octets));
    }

    [TestMethod]
    public void Of_ContextTaggedValueWithAStringTagNumber_IsHashedAsEncoded()
    {
        var tagged = NameWithCommonName(
            writer => writer.WriteCharacterString(UniversalTagNumber.UTF8String, "example", new Asn1Tag(TagClass.ContextSpecific, (int)UniversalTagNumber.UTF8String)));
        var utf8 = NameWithCommonName(writer => writer.WriteCharacterString(UniversalTagNumber.UTF8String, "example"));

        Assert.AreNotEqual(OpenSslSubjectNameHash.Of(utf8), OpenSslSubjectNameHash.Of(tagged));
    }

    private static X509Certificate2Collection AnchorsFor(string directory) =>
        new ServerCertificateVerification(new TlsClientOptions(CaCertificateDirectory: directory), OpenSslBuild, TimeProvider.System)
            .ReadTrustAnchors().AnchorsBesideSystemStore;

    private static string HashName(X509Certificate2 certificate, int number) =>
        $"{OpenSslSubjectNameHash.Of(certificate.SubjectName)}.{number}";

    private string WriteDirectory(string name, params (string FileName, X509Certificate2 Certificate)[] files)
    {
        var directory = Path.Combine(_root, name);
        Directory.CreateDirectory(directory);
        foreach (var (fileName, certificate) in files)
        {
            File.WriteAllText(Path.Combine(directory, fileName), certificate.ExportCertificatePem());
        }

        return directory;
    }

    private static X500DistinguishedName NameWithCommonName(Action<AsnWriter> writeValue)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        using (writer.PushSetOf())
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier("2.5.4.3");
            writeValue(writer);
        }

        return new X500DistinguishedName(writer.Encode());
    }

    private static X509Certificate2 CreateSelfSigned(string subject)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest(subject, key, HashAlgorithmName.SHA256);
        return request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }
}
