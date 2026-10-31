using Curl.Extension.Dane.Wire;
using static Curl.Extension.Dane.Dnssec.DnssecTestRecords;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// Checks <see cref="DelegationVerifier" /> against RFC 4034 section 5.1.4 and RFC 4035 section 5.2 with RFC 6605
/// section 6's examples (digest types 2 and 4) and the recorded root DNSKEYs against <see cref="RootTrustAnchors" />.
/// </summary>
[TestClass]
public sealed class DelegationVerifierTests
{
    // RFC 6605 section 6.1: example.net. DNSKEY 257 3 13 and its DS 55648 13 2.
    private static readonly DnsResourceRecord P256Key = DnskeyRecord(
        "example.net.",
        new DnskeyRecordData(257, 3, 13, Convert.FromBase64String("GojIhhXUN/u4v54ZQqGSnyhWJwaubCvTmeexv7bR6edbkrSqQpF64cYbcB7wNcP+e+MAnLr+Wi9xMWyQLc8NAA==")));

    private static readonly DsRecordData P256Sha256 =
        new(55648, 13, 2, Convert.FromHexString("b4c8c1fe2e7477127b27115656ad6256f424625bf5c1e2770ce6d6e37df61d17"));

    // RFC 6605 section 6.2: example.net. DNSKEY 257 3 14 and its DS 10771 14 4.
    private static readonly DnsResourceRecord P384Key = DnskeyRecord(
        "example.net.",
        new DnskeyRecordData(257, 3, 14, Convert.FromBase64String("xKYaNhWdGOfJ+nPrL8/arkwf2EY3MDJ+SErKivBVSum1w/egsXvSADtNJhyem5RCOpgQ6K8X1DRSEkrbYQ+OB+v8/uX45NBwY8rp65F6Glur8I/mlVNgF6W/qTI37m40")));

    private static readonly DsRecordData P384Sha384 =
        new(10771, 14, 4, Convert.FromHexString("72d7b62976ce06438e9c0bf319013cf801f09ecc84b8d7e9495f27e305c6a9b0563a9b5f4d288405c3008a946df983d6"));

    [TestMethod]
    public void Verify_Rfc4509Section2DigestType2MatchingKey_IsSecure() =>
        Assert.AreEqual(DnssecValidationResult.Secure, DelegationVerifier.Verify(P256Key, [P256Sha256]));

    [TestMethod]
    public void Verify_Rfc6605Section6_2DigestType4MatchingKey_IsSecure() =>
        Assert.AreEqual(DnssecValidationResult.Secure, DelegationVerifier.Verify(P384Key, [P384Sha384]));

    [TestMethod]
    public void Verify_Rfc4034Section5_1_4OwnerInAnotherCase_IsSecure()
    {
        DnsResourceRecord upper = P256Key with { Owner = Name("EXAMPLE.Net.") };

        Assert.AreEqual(DnssecValidationResult.Secure, DelegationVerifier.Verify(upper, [P384Sha384, P256Sha256]));
    }

    [TestMethod]
    public void Verify_Rfc4034Section5_1RecordedRootKsksAgainstRootTrustAnchors_AreSecure()
    {
        List<DnsResourceRecord> rootKeys = Answers(Decode(RecordedDnssecResponses.RootDnskey), DnssecRecordType.Dnskey);

        DnssecValidationResult[] results = [.. rootKeys.Select(key => DelegationVerifier.Verify(key, RootTrustAnchors.DelegationSigners))];

        Assert.AreEqual(2, results.Count(result => result == DnssecValidationResult.Secure));
        Assert.AreEqual(1, results.Count(result => result == DnssecValidationResult.Bogus));
    }

    [TestMethod]
    public void Verify_Rfc4034Section5_1_4DigestMismatch_IsBogus()
    {
        DsRecordData altered = P256Sha256 with { Digest = [.. P256Sha256.Digest[..^1], (byte)(P256Sha256.Digest[^1] ^ 1)] };

        Assert.AreEqual(DnssecValidationResult.Bogus, DelegationVerifier.Verify(P256Key, [altered]));
    }

    [TestMethod]
    public void Verify_Rfc4034Section5_1_1KeyTagMismatch_IsBogus() =>
        Assert.AreEqual(DnssecValidationResult.Bogus, DelegationVerifier.Verify(P256Key, [P256Sha256 with { KeyTag = 55649 }]));

    [TestMethod]
    public void Verify_Rfc4034Section5_1_2AlgorithmMismatch_IsBogus() =>
        Assert.AreEqual(DnssecValidationResult.Bogus, DelegationVerifier.Verify(P256Key, [P256Sha256 with { Algorithm = 14 }]));

    [TestMethod]
    public void Verify_Rfc4034Section5_1RecordWithoutDnskeyData_IsBogus() =>
        Assert.AreEqual(DnssecValidationResult.Bogus, DelegationVerifier.Verify(Record("example.net.", DnssecRecordType.Dnskey, []), [P256Sha256]));

    [TestMethod]
    public void Verify_Rfc4035Section5_2UnsupportedDigestType1_IsInsecure() =>
        Assert.AreEqual(DnssecValidationResult.Insecure, DelegationVerifier.Verify(P256Key, [P256Sha256 with { DigestType = 1 }]));

    [TestMethod]
    public void Verify_Rfc4035Section5_2UnsupportedAlgorithm5_IsInsecure() =>
        Assert.AreEqual(DnssecValidationResult.Insecure, DelegationVerifier.Verify(P256Key, [P256Sha256 with { Algorithm = 5 }]));

    [TestMethod]
    public void Verify_Rfc4035Section5_2NoDelegationSigners_IsInsecure() =>
        Assert.AreEqual(DnssecValidationResult.Insecure, DelegationVerifier.Verify(P256Key, []));
}
