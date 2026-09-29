using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// <see cref="PinnedPublicKey" /> matches keys as curl 8.21.0's <c>Curl_pin_peer_pubkey</c> does
/// (ADR-0193). The measured cases, curl 8.21.0 Schannel and curl 8.18.0 OpenSSL, 2026-09-29
/// (BL-608): the right hash, the right hash second in a list, a PEM file and a DER file of the
/// key succeed; a wrong hash, <c>sha256//</c> with nothing after it, a hash that is not base64,
/// another key's PEM file, a file holding no key and a missing file are exit 90.
/// </summary>
[TestClass]
public sealed class PinnedPublicKeyTests
{
    private static X509Certificate2 s_certificate = null!;

    private static byte[] s_subjectPublicKeyInfo = null!;

    private string _directory = null!;

    [ClassInitialize]
    public static void CreateCertificate(TestContext context)
    {
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        s_certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        s_subjectPublicKeyInfo = s_certificate.PublicKey.ExportSubjectPublicKeyInfo();
    }

    [ClassCleanup]
    public static void DisposeCertificate() => s_certificate.Dispose();

    [TestInitialize]
    public void CreateDirectory()
    {
        _directory = Path.Combine(Path.GetTempPath(), "Curl.Networking.UnitTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
    }

    [TestCleanup]
    public void DeleteDirectory() => Directory.Delete(_directory, recursive: true);

    private static string Hash => PinnedPublicKey.HashOf(s_subjectPublicKeyInfo);

    [TestMethod]
    public void HashOf_IsTheBase64Sha256OfTheSubjectPublicKeyInfo()
    {
        Assert.AreEqual(Convert.ToBase64String(SHA256.HashData(s_subjectPublicKeyInfo)), Hash);
    }

    [TestMethod]
    public void Accepts_WithoutAPin_AcceptsAnything()
    {
        Assert.IsTrue(PinnedPublicKey.Accepts(null, []));
    }

    [TestMethod]
    public void Accepts_WithAPinAndNoServerCertificate_Refuses()
    {
        Assert.IsFalse(PinnedPublicKey.Accepts("sha256//" + Hash, []));
    }

    [TestMethod]
    public void Accepts_WithAServerCertificateThatDoesNotParse_Refuses()
    {
        Assert.IsFalse(PinnedPublicKey.Accepts("sha256//" + Hash, [new byte[] { 0x30, 0x03, 0x02, 0x01, 0x00 }]));
    }

    [TestMethod]
    public void Accepts_WithTheServerCertificatesKeyPinned_Accepts()
    {
        Assert.IsTrue(PinnedPublicKey.Accepts("sha256//" + Hash, [s_certificate.RawData]));
    }

    [TestMethod]
    public void Refusal_WithAMatchingPin_IsNullAndWithAnotherIsExit90()
    {
        Assert.IsNull(PinnedPublicKey.Refusal("sha256//" + Hash, [s_certificate.RawData]));
        Assert.AreEqual(
            (Curl.Protocol.Abstractions.CurlExitCode.SslPinnedPubKeyNotMatch, "SSL: public key does not match pinned public key"),
            PinnedPublicKey.Refusal("sha256//AAAA", [s_certificate.RawData]));
    }

    [TestMethod]
    public void Matches_WithTheRightHash_Matches()
    {
        Assert.IsTrue(PinnedPublicKey.Matches("sha256//" + Hash, s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithTheRightHashSecondInAList_Matches()
    {
        Assert.IsTrue(PinnedPublicKey.Matches("sha256//AAAA;sha256//" + Hash, s_subjectPublicKeyInfo));
    }

    [TestMethod]
    [DataRow("sha256//AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [DataRow("sha256//")]
    [DataRow("sha256//!!!")]
    public void Matches_WithAWrongHash_DoesNotMatch(string pin)
    {
        Assert.IsFalse(PinnedPublicKey.Matches(pin, s_subjectPublicKeyInfo));
    }

    // curl compares each hash whole and exactly: case matters, and only ";sha256//" separates.
    [TestMethod]
    public void Matches_WithTheRightHashInAnotherCaseOrAfterABareSemicolon_DoesNotMatch()
    {
        Assert.IsFalse(PinnedPublicKey.Matches("sha256//" + Hash.ToUpperInvariant() + "x", s_subjectPublicKeyInfo));
        Assert.IsFalse(PinnedPublicKey.Matches("sha256//AAAA;" + Hash, s_subjectPublicKeyInfo));
        Assert.IsFalse(PinnedPublicKey.Matches("SHA256//" + Hash, s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithTheKeysPemFile_Matches()
    {
        Assert.IsTrue(PinnedPublicKey.Matches(Write("key.pem", PemEncoding.WriteString("PUBLIC KEY", s_subjectPublicKeyInfo)), s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithTheKeysPemFileWithCrlfLinesAfterOtherText_Matches()
    {
        var pem = "junk\r\n" + PemEncoding.WriteString("PUBLIC KEY", s_subjectPublicKeyInfo).Replace("\n", "\r\n", StringComparison.Ordinal);

        Assert.IsTrue(PinnedPublicKey.Matches(Write("key.pem", pem), s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithTheKeysDerFile_Matches()
    {
        Assert.IsTrue(PinnedPublicKey.Matches(Write("key.der", s_subjectPublicKeyInfo), s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithAnotherKeysDerFileOfTheSameSize_DoesNotMatch()
    {
        var other = (byte[])s_subjectPublicKeyInfo.Clone();
        other[^1] ^= 0xFF;

        Assert.IsFalse(PinnedPublicKey.Matches(Write("key.der", other), s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithAnotherKeysPemFile_DoesNotMatch()
    {
        using var otherKey = RSA.Create(2048);

        Assert.IsFalse(PinnedPublicKey.Matches(Write("other.pem", otherKey.ExportSubjectPublicKeyInfoPem()), s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithAFileHoldingNoKey_DoesNotMatch()
    {
        Assert.IsFalse(PinnedPublicKey.Matches(Write("small", "not a key"), s_subjectPublicKeyInfo));
        Assert.IsFalse(PinnedPublicKey.Matches(Write("large", new string('x', 1000)), s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithAMissingFileOrADirectory_DoesNotMatch()
    {
        Assert.IsFalse(PinnedPublicKey.Matches(Path.Combine(_directory, "nope.pem"), s_subjectPublicKeyInfo));
        Assert.IsFalse(PinnedPublicKey.Matches(_directory, s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void Matches_WithAFileOver1MiB_DoesNotMatch()
    {
        var pem = PemEncoding.WriteString("PUBLIC KEY", s_subjectPublicKeyInfo) + new string('x', 1_048_576);

        Assert.IsFalse(PinnedPublicKey.Matches(Write("big.pem", pem), s_subjectPublicKeyInfo));
    }

    [TestMethod]
    public void PemToDer_DecodesTheFirstPublicKeyBlock()
    {
        CollectionAssert.AreEqual(s_subjectPublicKeyInfo, PinnedPublicKey.PemToDer(Encoding.ASCII.GetBytes(PemEncoding.WriteString("PUBLIC KEY", s_subjectPublicKeyInfo))));
    }

    [TestMethod]
    [DataRow("no block at all")]
    [DataRow("x-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----\n")]
    [DataRow("-----BEGIN PUBLIC KEY-----\nAAAA\n")]
    [DataRow("-----BEGIN PUBLIC KEY-----\n-----END PUBLIC KEY-----\n")]
    [DataRow("-----BEGIN PUBLIC KEY-----\nAA AA\n-----END PUBLIC KEY-----\n")]
    [DataRow("-----BEGIN PUBLIC KEY-----\nAAA\n-----END PUBLIC KEY-----\n")]
    [DataRow("\0-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----\n")]
    public void PemToDer_WithoutAWellFormedBlock_ReturnsNull(string text)
    {
        Assert.IsNull(PinnedPublicKey.PemToDer(Encoding.Latin1.GetBytes(text)));
    }

    private string Write(string name, string content) => Write(name, Encoding.ASCII.GetBytes(content));

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
