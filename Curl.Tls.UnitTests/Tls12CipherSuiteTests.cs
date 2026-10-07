using Curl.Testing;

namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below suite table: its code points, which suites need TLS 1.2, the PRF
/// each version takes, and the record protection with and without encrypt-then-MAC.
/// </summary>
[TestClass]
public sealed class Tls12CipherSuiteTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void TheTableHoldsEveryFamilyAndNothingElse()
    {
        Diagnostics.Arrange("codes looked up", "0x1301, the renegotiation SCSV, 0xc030, 0x0034");

        Tls12CipherSuite? tls13Suite = Tls12CipherSuite.Find(0x1301);
        Tls12CipherSuite? scsv = Tls12CipherSuite.Find(Tls12CipherSuite.EmptyRenegotiationInfoScsv);
        Tls12CipherSuite? ecdheRsaAes256Gcm = Tls12CipherSuite.Find(0xc030);
        Tls12CipherSuite? dhAnonAes128Cbc = Tls12CipherSuite.Find(0x0034);
        Diagnostics.Act("suites in the table", Tls12CipherSuite.All.Count);
        Diagnostics.Act("0xc030", ecdheRsaAes256Gcm);
        Diagnostics.Act("0x0034", dhAnonAes128Cbc);

        Diagnostics.Assert("suites in the table", 113, Tls12CipherSuite.All.Count);
        Diagnostics.Assert("0x1301 and the SCSV are absent", "null, null", $"{tls13Suite?.ToString() ?? "null"}, {scsv?.ToString() ?? "null"}");
        Assert.HasCount(113, Tls12CipherSuite.All);
        Assert.IsNull(tls13Suite);
        Assert.IsNull(scsv);
        Assert.AreEqual(new Tls12CipherSuite(0xc030, Tls12KeyExchange.Ecdhe, Tls12Authentication.Rsa, Tls12BulkCipher.Aes256Gcm, Tls12MacAlgorithm.None, true), ecdheRsaAes256Gcm);
        Assert.AreEqual(new Tls12CipherSuite(0x0034, Tls12KeyExchange.Dhe, Tls12Authentication.Anonymous, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, false), dhAnonAes128Cbc);
    }

    [TestMethod]
    [DataRow((ushort)0x0013, Tls12BulkCipher.TripleDesEdeCbc, Tls12MacAlgorithm.HmacSha1, false, DisplayName = "TLS_DHE_DSS_WITH_3DES_EDE_CBC_SHA")]
    [DataRow((ushort)0x0032, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, false, DisplayName = "TLS_DHE_DSS_WITH_AES_128_CBC_SHA")]
    [DataRow((ushort)0x0038, Tls12BulkCipher.Aes256Cbc, Tls12MacAlgorithm.HmacSha1, false, DisplayName = "TLS_DHE_DSS_WITH_AES_256_CBC_SHA")]
    [DataRow((ushort)0x0040, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha256, false, DisplayName = "TLS_DHE_DSS_WITH_AES_128_CBC_SHA256")]
    [DataRow((ushort)0x006a, Tls12BulkCipher.Aes256Cbc, Tls12MacAlgorithm.HmacSha256, false, DisplayName = "TLS_DHE_DSS_WITH_AES_256_CBC_SHA256")]
    [DataRow((ushort)0x00a2, Tls12BulkCipher.Aes128Gcm, Tls12MacAlgorithm.None, false, DisplayName = "TLS_DHE_DSS_WITH_AES_128_GCM_SHA256")]
    [DataRow((ushort)0x00a3, Tls12BulkCipher.Aes256Gcm, Tls12MacAlgorithm.None, true, DisplayName = "TLS_DHE_DSS_WITH_AES_256_GCM_SHA384")]
    [DataRow((ushort)0x0044, Tls12BulkCipher.Camellia128Cbc, Tls12MacAlgorithm.HmacSha1, false, DisplayName = "TLS_DHE_DSS_WITH_CAMELLIA_128_CBC_SHA")]
    [DataRow((ushort)0x0087, Tls12BulkCipher.Camellia256Cbc, Tls12MacAlgorithm.HmacSha1, false, DisplayName = "TLS_DHE_DSS_WITH_CAMELLIA_256_CBC_SHA")]
    [DataRow((ushort)0x00bd, Tls12BulkCipher.Camellia128Cbc, Tls12MacAlgorithm.HmacSha256, false, DisplayName = "TLS_DHE_DSS_WITH_CAMELLIA_128_CBC_SHA256")]
    [DataRow((ushort)0x00c3, Tls12BulkCipher.Camellia256Cbc, Tls12MacAlgorithm.HmacSha256, false, DisplayName = "TLS_DHE_DSS_WITH_CAMELLIA_256_CBC_SHA256")]
    [DataRow((ushort)0xc056, Tls12BulkCipher.Aria128Gcm, Tls12MacAlgorithm.None, false, DisplayName = "TLS_DHE_DSS_WITH_ARIA_128_GCM_SHA256")]
    [DataRow((ushort)0xc057, Tls12BulkCipher.Aria256Gcm, Tls12MacAlgorithm.None, true, DisplayName = "TLS_DHE_DSS_WITH_ARIA_256_GCM_SHA384")]
    public void EachDheDssSuiteIsPinned(int code, Tls12BulkCipher bulkCipher, Tls12MacAlgorithm macAlgorithm, bool usesSha384Prf)
    {
        Tls12CipherSuite expected = new((ushort)code, Tls12KeyExchange.Dhe, Tls12Authentication.Dss, bulkCipher, macAlgorithm, usesSha384Prf);
        Diagnostics.Arrange("expected suite", expected);

        Tls12CipherSuite? found = Tls12CipherSuite.Find((ushort)code);
        Diagnostics.Act("found suite", found);

        Diagnostics.Assert("found suite equals the expected one", true, expected.Equals(found));
        Diagnostics.Assert("requires TLS 1.2", macAlgorithm is not Tls12MacAlgorithm.HmacSha1, expected.RequiresTls12);
        Assert.AreEqual(expected, found);
        Assert.AreEqual(macAlgorithm is not Tls12MacAlgorithm.HmacSha1, expected.RequiresTls12);
    }

    [TestMethod]
    [DataRow((ushort)0xc09c, Tls12KeyExchange.Rsa, Tls12Authentication.Rsa, Tls12BulkCipher.Aes128Ccm, DisplayName = "TLS_RSA_WITH_AES_128_CCM")]
    [DataRow((ushort)0xc09d, Tls12KeyExchange.Rsa, Tls12Authentication.Rsa, Tls12BulkCipher.Aes256Ccm, DisplayName = "TLS_RSA_WITH_AES_256_CCM")]
    [DataRow((ushort)0xc09e, Tls12KeyExchange.Dhe, Tls12Authentication.Rsa, Tls12BulkCipher.Aes128Ccm, DisplayName = "TLS_DHE_RSA_WITH_AES_128_CCM")]
    [DataRow((ushort)0xc09f, Tls12KeyExchange.Dhe, Tls12Authentication.Rsa, Tls12BulkCipher.Aes256Ccm, DisplayName = "TLS_DHE_RSA_WITH_AES_256_CCM")]
    [DataRow((ushort)0xc0a0, Tls12KeyExchange.Rsa, Tls12Authentication.Rsa, Tls12BulkCipher.Aes128Ccm8, DisplayName = "TLS_RSA_WITH_AES_128_CCM_8")]
    [DataRow((ushort)0xc0a1, Tls12KeyExchange.Rsa, Tls12Authentication.Rsa, Tls12BulkCipher.Aes256Ccm8, DisplayName = "TLS_RSA_WITH_AES_256_CCM_8")]
    [DataRow((ushort)0xc0a2, Tls12KeyExchange.Dhe, Tls12Authentication.Rsa, Tls12BulkCipher.Aes128Ccm8, DisplayName = "TLS_DHE_RSA_WITH_AES_128_CCM_8")]
    [DataRow((ushort)0xc0a3, Tls12KeyExchange.Dhe, Tls12Authentication.Rsa, Tls12BulkCipher.Aes256Ccm8, DisplayName = "TLS_DHE_RSA_WITH_AES_256_CCM_8")]
    [DataRow((ushort)0xc0ac, Tls12KeyExchange.Ecdhe, Tls12Authentication.Ecdsa, Tls12BulkCipher.Aes128Ccm, DisplayName = "TLS_ECDHE_ECDSA_WITH_AES_128_CCM")]
    [DataRow((ushort)0xc0ad, Tls12KeyExchange.Ecdhe, Tls12Authentication.Ecdsa, Tls12BulkCipher.Aes256Ccm, DisplayName = "TLS_ECDHE_ECDSA_WITH_AES_256_CCM")]
    [DataRow((ushort)0xc0ae, Tls12KeyExchange.Ecdhe, Tls12Authentication.Ecdsa, Tls12BulkCipher.Aes128Ccm8, DisplayName = "TLS_ECDHE_ECDSA_WITH_AES_128_CCM_8")]
    [DataRow((ushort)0xc0af, Tls12KeyExchange.Ecdhe, Tls12Authentication.Ecdsa, Tls12BulkCipher.Aes256Ccm8, DisplayName = "TLS_ECDHE_ECDSA_WITH_AES_256_CCM_8")]
    public void EachCcmSuiteIsPinnedAndNeedsTlsOneTwoWithASha256Prf(int code, Tls12KeyExchange keyExchange, Tls12Authentication authentication, Tls12BulkCipher bulkCipher)
    {
        Diagnostics.Arrange("code", $"0x{code:x4}");

        Tls12CipherSuite suite = Tls12CipherSuite.Find((ushort)code)!;
        Diagnostics.Act("found suite", suite);

        Diagnostics.Assert("requires TLS 1.2", true, suite.RequiresTls12);
        Diagnostics.Assert("TLS 1.2 PRF is SHA-256", true, ReferenceEquals(TlsPrf.Sha256, suite.PrfFor(TlsProtocolVersion.Tls12)));
        Assert.AreEqual(new Tls12CipherSuite((ushort)code, keyExchange, authentication, bulkCipher, Tls12MacAlgorithm.None, false), suite);
        Assert.IsTrue(suite.RequiresTls12);
        Assert.AreSame(TlsPrf.Sha256, suite.PrfFor(TlsProtocolVersion.Tls12));
    }

    [TestMethod]
    [DataRow((ushort)0x0004, Tls12KeyExchange.Rsa, Tls12Authentication.Rsa, Tls12MacAlgorithm.HmacMd5, DisplayName = "TLS_RSA_WITH_RC4_128_MD5")]
    [DataRow((ushort)0x0005, Tls12KeyExchange.Rsa, Tls12Authentication.Rsa, Tls12MacAlgorithm.HmacSha1, DisplayName = "TLS_RSA_WITH_RC4_128_SHA")]
    [DataRow((ushort)0xc011, Tls12KeyExchange.Ecdhe, Tls12Authentication.Rsa, Tls12MacAlgorithm.HmacSha1, DisplayName = "TLS_ECDHE_RSA_WITH_RC4_128_SHA")]
    [DataRow((ushort)0xc007, Tls12KeyExchange.Ecdhe, Tls12Authentication.Ecdsa, Tls12MacAlgorithm.HmacSha1, DisplayName = "TLS_ECDHE_ECDSA_WITH_RC4_128_SHA")]
    [DataRow((ushort)0x0018, Tls12KeyExchange.Dhe, Tls12Authentication.Anonymous, Tls12MacAlgorithm.HmacMd5, DisplayName = "TLS_DH_anon_WITH_RC4_128_MD5")]
    [DataRow((ushort)0xc016, Tls12KeyExchange.Ecdhe, Tls12Authentication.Anonymous, Tls12MacAlgorithm.HmacSha1, DisplayName = "TLS_ECDH_anon_WITH_RC4_128_SHA")]
    public void EachRc4SuiteIsPinnedAndRunsFromTlsOneZero(int code, Tls12KeyExchange keyExchange, Tls12Authentication authentication, Tls12MacAlgorithm macAlgorithm)
    {
        Diagnostics.Arrange("code", $"0x{code:x4}");

        Tls12CipherSuite suite = Tls12CipherSuite.Find((ushort)code)!;
        Diagnostics.Act("found suite", suite);

        Diagnostics.Assert("requires TLS 1.2", false, suite.RequiresTls12);
        Diagnostics.Assert("TLS 1.0 PRF is MD5 and SHA-1", true, ReferenceEquals(TlsPrf.Md5Sha1, suite.PrfFor(TlsProtocolVersion.Tls10)));
        Assert.AreEqual(new Tls12CipherSuite((ushort)code, keyExchange, authentication, Tls12BulkCipher.Rc4128, macAlgorithm, false), suite);
        Assert.IsFalse(suite.RequiresTls12);
        Assert.AreSame(TlsPrf.Md5Sha1, suite.PrfFor(TlsProtocolVersion.Tls10));
    }

    [TestMethod]
    [DataRow((ushort)0xc02b, true)]
    [DataRow((ushort)0xc023, true)]
    [DataRow((ushort)0xc024, true)]
    [DataRow((ushort)0xc009, false)]
    [DataRow((ushort)0x0001, false)]
    public void AeadAndShaTwoMacSuitesRequireTlsOneTwo(int code, bool requiresTls12)
    {
        Diagnostics.Arrange("code", $"0x{code:x4}");

        Tls12CipherSuite suite = Tls12CipherSuite.Find((ushort)code)!;
        Diagnostics.Act("found suite", suite);

        Diagnostics.Assert("requires TLS 1.2", requiresTls12, suite.RequiresTls12);
        Assert.AreEqual(requiresTls12, suite.RequiresTls12);
    }

    [TestMethod]
    public void EachVersionTakesItsPrf()
    {
        Tls12CipherSuite sha256 = Tls12CipherSuite.Find(0xc02b)!;
        Tls12CipherSuite sha384 = Tls12CipherSuite.Find(0xc02c)!;
        Diagnostics.Arrange("SHA-256 PRF suite", sha256);
        Diagnostics.Arrange("SHA-384 PRF suite", sha384);

        TlsPrf sha256AtTls12 = sha256.PrfFor(TlsProtocolVersion.Tls12);
        TlsPrf sha384AtTls12 = sha384.PrfFor(TlsProtocolVersion.Tls12);
        TlsPrf sha256AtTls11 = sha256.PrfFor(TlsProtocolVersion.Tls11);
        TlsPrf sha384AtTls10 = sha384.PrfFor(TlsProtocolVersion.Tls10);
        Diagnostics.Act("0xc02b at TLS 1.2 takes SHA-256", ReferenceEquals(TlsPrf.Sha256, sha256AtTls12));
        Diagnostics.Act("0xc02c at TLS 1.2 takes SHA-384", ReferenceEquals(TlsPrf.Sha384, sha384AtTls12));

        Diagnostics.Assert("0xc02b at TLS 1.1 takes MD5 and SHA-1", true, ReferenceEquals(TlsPrf.Md5Sha1, sha256AtTls11));
        Diagnostics.Assert("0xc02c at TLS 1.0 takes MD5 and SHA-1", true, ReferenceEquals(TlsPrf.Md5Sha1, sha384AtTls10));
        Assert.AreSame(TlsPrf.Sha256, sha256AtTls12);
        Assert.AreSame(TlsPrf.Sha384, sha384AtTls12);
        Assert.AreSame(TlsPrf.Md5Sha1, sha256AtTls11);
        Assert.AreSame(TlsPrf.Md5Sha1, sha384AtTls10);
    }

    [TestMethod]
    [DataRow((ushort)0xc013, true, true)]
    [DataRow((ushort)0xc013, false, false)]
    [DataRow((ushort)0xc02f, true, false)]
    [DataRow((ushort)0xc010, true, false)]
    public void EncryptThenMacCountsOnlyForCbcSuites(int code, bool agreed, bool expected)
    {
        Diagnostics.Arrange("code", $"0x{code:x4}");
        Diagnostics.Arrange("encrypt-then-MAC agreed", agreed);

        Tls12RecordProtectionParameters parameters = Tls12CipherSuite.Find((ushort)code)!.RecordProtectionFor(TlsProtocolVersion.Tls12, agreed);
        Diagnostics.Act("record protection", parameters);

        Diagnostics.Assert("encrypt-then-MAC", expected, parameters.EncryptThenMac);
        Diagnostics.Assert("version", TlsProtocolVersion.Tls12, parameters.Version);
        Assert.AreEqual(expected, parameters.EncryptThenMac);
        Assert.AreEqual(TlsProtocolVersion.Tls12, parameters.Version);
    }
}
