namespace Curl.Tls;

/// <summary>
/// The TLS 1.2 and below suite table: its code points, which suites need TLS 1.2, the PRF
/// each version takes, and the record protection with and without encrypt-then-MAC.
/// </summary>
[TestClass]
public sealed class Tls12CipherSuiteTests
{
    [TestMethod]
    public void TheTableHoldsEveryFamilyAndNothingElse()
    {
        Assert.HasCount(73, Tls12CipherSuite.All);
        Assert.IsNull(Tls12CipherSuite.Find(0x1301));
        Assert.IsNull(Tls12CipherSuite.Find(Tls12CipherSuite.EmptyRenegotiationInfoScsv));
        Assert.AreEqual(new Tls12CipherSuite(0xc030, Tls12KeyExchange.Ecdhe, Tls12Authentication.Rsa, Tls12BulkCipher.Aes256Gcm, Tls12MacAlgorithm.None, true), Tls12CipherSuite.Find(0xc030));
        Assert.AreEqual(new Tls12CipherSuite(0x0034, Tls12KeyExchange.Dhe, Tls12Authentication.Anonymous, Tls12BulkCipher.Aes128Cbc, Tls12MacAlgorithm.HmacSha1, false), Tls12CipherSuite.Find(0x0034));
    }

    [TestMethod]
    [DataRow((ushort)0xc02b, true)]
    [DataRow((ushort)0xc023, true)]
    [DataRow((ushort)0xc024, true)]
    [DataRow((ushort)0xc009, false)]
    [DataRow((ushort)0x0001, false)]
    public void AeadAndShaTwoMacSuitesRequireTlsOneTwo(int code, bool requiresTls12)
    {
        Assert.AreEqual(requiresTls12, Tls12CipherSuite.Find((ushort)code)!.RequiresTls12);
    }

    [TestMethod]
    public void EachVersionTakesItsPrf()
    {
        Tls12CipherSuite sha256 = Tls12CipherSuite.Find(0xc02b)!;
        Tls12CipherSuite sha384 = Tls12CipherSuite.Find(0xc02c)!;

        Assert.AreSame(TlsPrf.Sha256, sha256.PrfFor(TlsProtocolVersion.Tls12));
        Assert.AreSame(TlsPrf.Sha384, sha384.PrfFor(TlsProtocolVersion.Tls12));
        Assert.AreSame(TlsPrf.Md5Sha1, sha256.PrfFor(TlsProtocolVersion.Tls11));
        Assert.AreSame(TlsPrf.Md5Sha1, sha384.PrfFor(TlsProtocolVersion.Tls10));
    }

    [TestMethod]
    [DataRow((ushort)0xc013, true, true)]
    [DataRow((ushort)0xc013, false, false)]
    [DataRow((ushort)0xc02f, true, false)]
    [DataRow((ushort)0xc010, true, false)]
    public void EncryptThenMacCountsOnlyForCbcSuites(int code, bool agreed, bool expected)
    {
        Tls12RecordProtectionParameters parameters = Tls12CipherSuite.Find((ushort)code)!.RecordProtectionFor(TlsProtocolVersion.Tls12, agreed);

        Assert.AreEqual(expected, parameters.EncryptThenMac);
        Assert.AreEqual(TlsProtocolVersion.Tls12, parameters.Version);
    }
}
