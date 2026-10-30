using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Tlsa;

/// <summary>
/// Checks <see cref="TlsaCertificateMatcher" /> against RFC 6698 sections 2.1 and 4.1 and RFC 7671 sections 5.1 to
/// 5.4 with a generated two-certificate chain (a CA and the server's certificate it issued).
/// </summary>
[TestClass]
public sealed class TlsaCertificateMatcherTests
{
    private static readonly GeneratedChain Chain = GeneratedChain.Create();

    [TestMethod]
    public void Match_Rfc7671Section5_1_DaneEeFullCertificateExactMatchesTheServerCertificate()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(Chain.Der, [new TlsaRecordData(3, 0, 0, Chain.LeafDer)]);

        Assert.AreEqual(TlsaMatchResult.Match, result);
    }

    [TestMethod]
    public void Match_Rfc7671Section5_1_DaneEeSpkiSha256MatchesTheServerKey()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(
            Chain.Der, [new TlsaRecordData(3, 1, 1, SHA256.HashData(Chain.LeafSpki))]);

        Assert.AreEqual(TlsaMatchResult.Match, result);
    }

    [TestMethod]
    public void Match_Rfc7671Section5_1_DaneEeIgnoresAnExpiredCertificate()
    {
        GeneratedChain expired = GeneratedChain.Create(DateTimeOffset.UnixEpoch);

        TlsaMatchResult result = TlsaCertificateMatcher.Match(
            expired.Der, [new TlsaRecordData(3, 1, 2, SHA512.HashData(expired.LeafSpki))]);

        Assert.AreEqual(TlsaMatchResult.Match, result);
    }

    [TestMethod]
    public void Match_Rfc7671Section5_1_DaneEeDoesNotMatchTheIssuer()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(Chain.Der, [new TlsaRecordData(3, 0, 0, Chain.IssuerDer)]);

        Assert.AreEqual(TlsaMatchResult.NoMatch, result);
    }

    [TestMethod]
    public void Match_Rfc7671Section5_2_DaneTaFullCertificateSha256MatchesTheIssuer()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(
            Chain.Der, [new TlsaRecordData(2, 0, 1, SHA256.HashData(Chain.IssuerDer))]);

        Assert.AreEqual(TlsaMatchResult.Match, result);
    }

    [TestMethod]
    public void Match_Rfc7671Section5_2_DaneTaDoesNotMatchTheServerCertificate()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(Chain.Der, [new TlsaRecordData(2, 0, 0, Chain.LeafDer)]);

        Assert.AreEqual(TlsaMatchResult.NoMatch, result);
    }

    [TestMethod]
    public void Match_Rfc7671Section5_3_PkixEeSpkiExactMatchesTheServerKey()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(Chain.Der, [new TlsaRecordData(1, 1, 0, Chain.LeafSpki)]);

        Assert.AreEqual(TlsaMatchResult.Match, result);
    }

    [TestMethod]
    public void Match_Rfc7671Section5_4_PkixTaSpkiSha512MatchesTheIssuerKey()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(
            Chain.Der, [new TlsaRecordData(0, 1, 2, SHA512.HashData(Chain.IssuerSpki))]);

        Assert.AreEqual(TlsaMatchResult.Match, result);
    }

    [TestMethod]
    public void Match_Rfc6698Section2_1_3_ADigestOfOtherDataIsNoMatch()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(
            Chain.Der, [new TlsaRecordData(3, 0, 1, SHA256.HashData(Chain.IssuerDer))]);

        Assert.AreEqual(TlsaMatchResult.NoMatch, result);
    }

    [TestMethod]
    public void Match_Rfc6698Section2_1_AnyMatchingRecordInTheSetIsAMatch()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(
            Chain.Der, [new TlsaRecordData(3, 0, 0, [1, 2, 3]), new TlsaRecordData(2, 1, 0, Chain.IssuerSpki)]);

        Assert.AreEqual(TlsaMatchResult.Match, result);
    }

    [TestMethod]
    [DataRow((byte)4, (byte)0, (byte)0)]
    [DataRow((byte)3, (byte)2, (byte)0)]
    [DataRow((byte)3, (byte)0, (byte)3)]
    public void Match_Rfc6698Section4_1_OnlyUnusableRecordsAreNoUsableRecords(byte usage, byte selector, byte matchingType)
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(
            Chain.Der, [new TlsaRecordData(usage, selector, matchingType, Chain.LeafDer)]);

        Assert.AreEqual(TlsaMatchResult.NoUsableRecords, result);
    }

    [TestMethod]
    public void Match_Rfc6698Section4_1_AnEmptySetIsNoUsableRecords()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(Chain.Der, []);

        Assert.AreEqual(TlsaMatchResult.NoUsableRecords, result);
    }

    [TestMethod]
    public void Match_Rfc6698Section4_1_UnusableRecordsAreIgnoredBesideAUsableOne()
    {
        TlsaMatchResult result = TlsaCertificateMatcher.Match(
            Chain.Der, [new TlsaRecordData(255, 0, 0, Chain.LeafDer), new TlsaRecordData(3, 0, 0, [1, 2, 3])]);

        Assert.AreEqual(TlsaMatchResult.NoMatch, result);
    }

    [TestMethod]
    public void Match_NullChain_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsaCertificateMatcher.Match(null!, []));
    }

    [TestMethod]
    public void Match_NullRecords_ThrowsArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => TlsaCertificateMatcher.Match(Chain.Der, null!));
    }

    private sealed record GeneratedChain(byte[] LeafDer, byte[] LeafSpki, byte[] IssuerDer, byte[] IssuerSpki)
    {
        public byte[][] Der => [LeafDer, IssuerDer];

        public static GeneratedChain Create() => Create(DateTimeOffset.UtcNow.AddDays(-1));

        public static GeneratedChain Create(DateTimeOffset notBefore)
        {
            using ECDsa issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            using ECDsa leafKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var issuerRequest = new CertificateRequest("CN=Test CA", issuerKey, HashAlgorithmName.SHA256);
            issuerRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
            using X509Certificate2 issuer = issuerRequest.CreateSelfSigned(notBefore, notBefore.AddDays(30));
            var leafRequest = new CertificateRequest("CN=example.com", leafKey, HashAlgorithmName.SHA256);
            using X509Certificate2 leaf = leafRequest.Create(issuer, notBefore, notBefore.AddDays(2), [1, 2, 3, 4]);
            return new GeneratedChain(
                leaf.RawData, leafKey.ExportSubjectPublicKeyInfo(), issuer.RawData, issuerKey.ExportSubjectPublicKeyInfo());
        }
    }
}
