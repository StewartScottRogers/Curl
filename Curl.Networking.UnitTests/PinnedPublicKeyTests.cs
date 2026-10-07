using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        var diagnostics = Diagnostics;
        diagnostics.Arrange("input", "subject public key info of the test certificate");
        var expected = Convert.ToBase64String(SHA256.HashData(s_subjectPublicKeyInfo));

        string actual;
        using (diagnostics.Phase("hash"))
        {
            actual = Hash;
        }

        diagnostics.Act("hash equals base64 sha256 of the key info", expected == actual);
        diagnostics.Assert("hash equals base64 sha256 of the key info", true, expected == actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Accepts_WithoutAPin_AcceptsAnything()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "none (null)");
        diagnostics.Arrange("server certificates", "none");

        bool accepted;
        using (diagnostics.Phase("accepts"))
        {
            accepted = PinnedPublicKey.Accepts(null, []);
        }

        diagnostics.Act("accepted", accepted);
        diagnostics.Assert("accepted", true, accepted);
        Assert.IsTrue(accepted);
    }

    [TestMethod]
    public void Accepts_WithAPinAndNoServerCertificate_Refuses()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "sha256// hash of the test certificate's key");
        diagnostics.Arrange("server certificates", "none");

        bool accepted;
        using (diagnostics.Phase("accepts"))
        {
            accepted = PinnedPublicKey.Accepts("sha256//" + Hash, []);
        }

        diagnostics.Act("accepted", accepted);
        diagnostics.Assert("accepted", false, accepted);
        Assert.IsFalse(accepted);
    }

    [TestMethod]
    public void Accepts_WithAServerCertificateThatDoesNotParse_Refuses()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "sha256// hash of the test certificate's key");
        diagnostics.Arrange("server certificates", "one 5-byte blob that is not a certificate");

        bool accepted;
        using (diagnostics.Phase("accepts"))
        {
            accepted = PinnedPublicKey.Accepts("sha256//" + Hash, [new byte[] { 0x30, 0x03, 0x02, 0x01, 0x00 }]);
        }

        diagnostics.Act("accepted", accepted);
        diagnostics.Assert("accepted", false, accepted);
        Assert.IsFalse(accepted);
    }

    [TestMethod]
    public void Accepts_WithTheServerCertificatesKeyPinned_Accepts()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "sha256// hash of the test certificate's key");
        diagnostics.Arrange("server certificates", "the test certificate");

        bool accepted;
        using (diagnostics.Phase("accepts"))
        {
            accepted = PinnedPublicKey.Accepts("sha256//" + Hash, [s_certificate.RawData]);
        }

        diagnostics.Act("accepted", accepted);
        diagnostics.Assert("accepted", true, accepted);
        Assert.IsTrue(accepted);
    }

    [TestMethod]
    public void Refusal_WithAMatchingPin_IsNullAndWithAnotherIsExit90()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("matching pin", "sha256// hash of the test certificate's key");
        diagnostics.Arrange("other pin", "sha256//AAAA");

        var matching = PinnedPublicKey.Refusal("sha256//" + Hash, [s_certificate.RawData]);
        diagnostics.Act("refusal with matching pin", matching is null ? "null" : "not null");
        diagnostics.Assert("refusal with matching pin is null", true, matching is null);
        Assert.IsNull(matching);

        var expected = (Curl.Protocol.Abstractions.CurlExitCode.SslPinnedPubKeyNotMatch, "SSL: public key does not match pinned public key");
        (Curl.Protocol.Abstractions.CurlExitCode, string)? other;
        using (diagnostics.Phase("refusal"))
        {
            other = PinnedPublicKey.Refusal("sha256//AAAA", [s_certificate.RawData]);
        }

        diagnostics.Act("refusal with other pin", other);
        diagnostics.Assert("refusal with other pin", expected, other);
        Assert.AreEqual(expected, other);
    }

    [TestMethod]
    public void ReportedHash_WithAHashPinMatchingOrNot_IsTheServerKeysHash()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pins", "matching sha256// hash and sha256//AAAA");
        diagnostics.Arrange("server certificates", "the test certificate");

        string? matching;
        string? other;
        using (diagnostics.Phase("reported hash"))
        {
            matching = PinnedPublicKey.ReportedHash("sha256//" + Hash, [s_certificate.RawData]);
            other = PinnedPublicKey.ReportedHash("sha256//AAAA", [s_certificate.RawData]);
        }

        var expected = "sha256//" + Hash;
        diagnostics.Act("report with matching pin", matching == expected ? "the server key's sha256// hash" : "something else");
        diagnostics.Act("report with other pin", other == expected ? "the server key's sha256// hash" : "something else");
        diagnostics.Assert("matching pin reports server key hash", true, matching == expected);
        diagnostics.Assert("other pin reports server key hash", true, other == expected);
        Assert.AreEqual(expected, matching);
        Assert.AreEqual(expected, other);
    }

    [TestMethod]
    public void ReportedHash_WithNoPinAFilePinNoCertificateOrOneThatDoesNotParse_IsNull()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("cases", "no pin; a file pin; no certificate; a certificate that does not parse");

        string? noPin;
        string? filePin;
        string? noCertificate;
        string? unparsable;
        using (diagnostics.Phase("reported hash"))
        {
            noPin = PinnedPublicKey.ReportedHash(null, [s_certificate.RawData]);
            filePin = PinnedPublicKey.ReportedHash(Path.Combine(_directory, "key.pem"), [s_certificate.RawData]);
            noCertificate = PinnedPublicKey.ReportedHash("sha256//AAAA", []);
            unparsable = PinnedPublicKey.ReportedHash("sha256//AAAA", [new byte[] { 0x30, 0x03, 0x02, 0x01, 0x00 }]);
        }

        diagnostics.Act("no pin reports null", noPin is null);
        diagnostics.Act("file pin reports null", filePin is null);
        diagnostics.Act("no certificate reports null", noCertificate is null);
        diagnostics.Act("unparsable certificate reports null", unparsable is null);
        diagnostics.Assert("no pin is null", true, noPin is null);
        diagnostics.Assert("file pin is null", true, filePin is null);
        diagnostics.Assert("no certificate is null", true, noCertificate is null);
        diagnostics.Assert("unparsable certificate is null", true, unparsable is null);
        Assert.IsNull(noPin);
        Assert.IsNull(filePin);
        Assert.IsNull(noCertificate);
        Assert.IsNull(unparsable);
    }

    [TestMethod]
    public void Matches_WithTheRightHash_Matches()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "sha256// hash of the test certificate's key");

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches("sha256//" + Hash, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", true, matches);
        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void Matches_WithTheRightHashSecondInAList_Matches()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "sha256//AAAA;sha256// hash of the test certificate's key");

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches("sha256//AAAA;sha256//" + Hash, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", true, matches);
        Assert.IsTrue(matches);
    }

    [TestMethod]
    [DataRow("sha256//AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [DataRow("sha256//")]
    [DataRow("sha256//!!!")]
    public void Matches_WithAWrongHash_DoesNotMatch(string pin)
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", pin);

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches(pin, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", false, matches);
        Assert.IsFalse(matches);
    }

    // curl compares each hash whole and exactly: case matters, and only ";sha256//" separates.
    [TestMethod]
    public void Matches_WithTheRightHashInAnotherCaseOrAfterABareSemicolon_DoesNotMatch()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pins", "upper-cased hash plus x; sha256//AAAA; then the hash with no sha256// prefix; SHA256// prefix in capitals");

        bool upper;
        bool bareSemicolon;
        bool capitalPrefix;
        using (diagnostics.Phase("matches"))
        {
            upper = PinnedPublicKey.Matches("sha256//" + Hash.ToUpperInvariant() + "x", s_subjectPublicKeyInfo);
            bareSemicolon = PinnedPublicKey.Matches("sha256//AAAA;" + Hash, s_subjectPublicKeyInfo);
            capitalPrefix = PinnedPublicKey.Matches("SHA256//" + Hash, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("upper-cased matches", upper);
        diagnostics.Act("bare semicolon matches", bareSemicolon);
        diagnostics.Act("capital prefix matches", capitalPrefix);
        diagnostics.Assert("upper-cased matches", false, upper);
        diagnostics.Assert("bare semicolon matches", false, bareSemicolon);
        diagnostics.Assert("capital prefix matches", false, capitalPrefix);
        Assert.IsFalse(upper);
        Assert.IsFalse(bareSemicolon);
        Assert.IsFalse(capitalPrefix);
    }

    [TestMethod]
    public void Matches_WithTheKeysPemFile_Matches()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "a PEM file holding the test certificate's public key");
        var path = Write("key.pem", PemEncoding.WriteString("PUBLIC KEY", s_subjectPublicKeyInfo));

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches(path, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", true, matches);
        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void Matches_WithTheKeysPemFileWithCrlfLinesAfterOtherText_Matches()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "a PEM file of the test certificate's key, CRLF lines, preceded by junk");
        var pem = "junk\r\n" + PemEncoding.WriteString("PUBLIC KEY", s_subjectPublicKeyInfo).Replace("\n", "\r\n", StringComparison.Ordinal);
        var path = Write("key.pem", pem);

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches(path, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", true, matches);
        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void Matches_WithTheKeysDerFile_Matches()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "a DER file holding the test certificate's public key");
        var path = Write("key.der", s_subjectPublicKeyInfo);

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches(path, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", true, matches);
        Assert.IsTrue(matches);
    }

    [TestMethod]
    public void Matches_WithAnotherKeysDerFileOfTheSameSize_DoesNotMatch()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "a DER file the size of the test key with its last byte flipped");
        var other = (byte[])s_subjectPublicKeyInfo.Clone();
        other[^1] ^= 0xFF;
        var path = Write("key.der", other);

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches(path, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", false, matches);
        Assert.IsFalse(matches);
    }

    [TestMethod]
    public void Matches_WithAnotherKeysPemFile_DoesNotMatch()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "a PEM file holding a different freshly generated RSA key");
        using var otherKey = RSA.Create(2048);
        var path = Write("other.pem", otherKey.ExportSubjectPublicKeyInfoPem());

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches(path, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", false, matches);
        Assert.IsFalse(matches);
    }

    [TestMethod]
    public void Matches_WithAFileHoldingNoKey_DoesNotMatch()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pins", "a small file of text; a 1000-character file of x");
        var small = Write("small", "not a key");
        var large = Write("large", new string('x', 1000));

        bool smallMatches;
        bool largeMatches;
        using (diagnostics.Phase("matches"))
        {
            smallMatches = PinnedPublicKey.Matches(small, s_subjectPublicKeyInfo);
            largeMatches = PinnedPublicKey.Matches(large, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("small file matches", smallMatches);
        diagnostics.Act("large file matches", largeMatches);
        diagnostics.Assert("small file matches", false, smallMatches);
        diagnostics.Assert("large file matches", false, largeMatches);
        Assert.IsFalse(smallMatches);
        Assert.IsFalse(largeMatches);
    }

    [TestMethod]
    public void Matches_WithAMissingFileOrADirectory_DoesNotMatch()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pins", "a path that does not exist; an existing directory");

        bool missingMatches;
        bool directoryMatches;
        using (diagnostics.Phase("matches"))
        {
            missingMatches = PinnedPublicKey.Matches(Path.Combine(_directory, "nope.pem"), s_subjectPublicKeyInfo);
            directoryMatches = PinnedPublicKey.Matches(_directory, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("missing file matches", missingMatches);
        diagnostics.Act("directory matches", directoryMatches);
        diagnostics.Assert("missing file matches", false, missingMatches);
        diagnostics.Assert("directory matches", false, directoryMatches);
        Assert.IsFalse(missingMatches);
        Assert.IsFalse(directoryMatches);
    }

    [TestMethod]
    public void Matches_WithAFileOver1MiB_DoesNotMatch()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pin", "a PEM file of the test certificate's key followed by 1048576 characters of x");
        var pem = PemEncoding.WriteString("PUBLIC KEY", s_subjectPublicKeyInfo) + new string('x', 1_048_576);
        var path = Write("big.pem", pem);

        bool matches;
        using (diagnostics.Phase("matches"))
        {
            matches = PinnedPublicKey.Matches(path, s_subjectPublicKeyInfo);
        }

        diagnostics.Act("matches", matches);
        diagnostics.Assert("matches", false, matches);
        Assert.IsFalse(matches);
    }

    [TestMethod]
    public void PemToDer_DecodesTheFirstPublicKeyBlock()
    {
        var diagnostics = Diagnostics;
        diagnostics.Arrange("pem", "a PUBLIC KEY block of the test certificate's key");
        var pem = Encoding.ASCII.GetBytes(PemEncoding.WriteString("PUBLIC KEY", s_subjectPublicKeyInfo));

        byte[]? der;
        using (diagnostics.Phase("decode"))
        {
            der = PinnedPublicKey.PemToDer(pem);
        }

        var equal = der is not null && der.AsSpan().SequenceEqual(s_subjectPublicKeyInfo);
        diagnostics.Act("decoded equals the subject public key info", equal);
        diagnostics.Assert("decoded equals the subject public key info", true, equal);
        CollectionAssert.AreEqual(s_subjectPublicKeyInfo, der);
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
        var diagnostics = Diagnostics;
        diagnostics.Arrange("text", text);

        byte[]? der;
        using (diagnostics.Phase("decode"))
        {
            der = PinnedPublicKey.PemToDer(Encoding.Latin1.GetBytes(text));
        }

        diagnostics.Act("decoded is null", der is null);
        diagnostics.Assert("decoded is null", true, der is null);
        Assert.IsNull(der);
    }

    private string Write(string name, string content) => Write(name, Encoding.ASCII.GetBytes(content));

    private string Write(string name, byte[] content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllBytes(path, content);
        return path;
    }
}
