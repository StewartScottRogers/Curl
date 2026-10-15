namespace Curl.Api.Options.Tls;

[TestClass]
public sealed class TlsPeerOptionsTests
{
    private static readonly TlsPeerOptions Defaults = new();

    [TestMethod]
    public void New_TlsPeerOptions_LeavesEveryCertificateAndKeyUnset()
    {
        Assert.IsNull(Defaults.Certificate);
        Assert.IsNull(Defaults.CertificateBlob);
        Assert.IsNull(Defaults.Key);
        Assert.IsNull(Defaults.KeyBlob);
        Assert.IsNull(Defaults.KeyPassword);
    }

    [TestMethod]
    public void New_TlsPeerOptions_DefaultsCertificateAndKeyTypeToPem()
    {
        Assert.AreEqual("PEM", Defaults.CertificateType);
        Assert.AreEqual("PEM", Defaults.KeyType);
    }

    [TestMethod]
    public void New_TlsPeerOptions_DefaultsSslVersionToZero()
    {
        Assert.AreEqual(SslVersion.Default, Defaults.SslVersion);
        Assert.AreEqual(SslVersionMaximum.None, Defaults.SslVersionMaximum);
        Assert.AreEqual(0, (int)Defaults.SslVersion | (int)Defaults.SslVersionMaximum);
    }

    [TestMethod]
    public void New_TlsPeerOptions_VerifiesPeerAndHost()
    {
        Assert.IsTrue(Defaults.VerifyPeer);
        Assert.IsTrue(Defaults.VerifyHost);
    }

    [TestMethod]
    public void New_TlsPeerOptions_LeavesEveryTrustAndCipherSettingUnset()
    {
        Assert.IsNull(Defaults.CertificateAuthorityFile);
        Assert.IsNull(Defaults.CertificateAuthorityBlob);
        Assert.IsNull(Defaults.CertificateAuthorityPath);
        Assert.IsNull(Defaults.RevocationListFile);
        Assert.IsNull(Defaults.IssuerCertificate);
        Assert.IsNull(Defaults.IssuerCertificateBlob);
        Assert.IsNull(Defaults.PinnedPublicKey);
        Assert.IsNull(Defaults.CipherList);
        Assert.IsNull(Defaults.Tls13Ciphers);
    }

    [TestMethod]
    public void New_TlsPeerOptions_SetsNoSslOptionBit()
    {
        Assert.AreEqual(SslOptions.None, Defaults.SslOptions);
    }

    [TestMethod]
    public void With_EachProperty_ChangesOnlyThatProperty()
    {
        ReadOnlyMemory<byte> blob = new byte[] { 1, 2, 3 };

        AssertChangesOnly(Defaults with { Certificate = "c.pem" }, o => o with { Certificate = null });
        AssertChangesOnly(Defaults with { CertificateBlob = blob }, o => o with { CertificateBlob = null });
        AssertChangesOnly(Defaults with { CertificateType = "DER" }, o => o with { CertificateType = "PEM" });
        AssertChangesOnly(Defaults with { Key = "k.pem" }, o => o with { Key = null });
        AssertChangesOnly(Defaults with { KeyBlob = blob }, o => o with { KeyBlob = null });
        AssertChangesOnly(Defaults with { KeyType = "DER" }, o => o with { KeyType = "PEM" });
        AssertChangesOnly(Defaults with { KeyPassword = "secret" }, o => o with { KeyPassword = null });
        AssertChangesOnly(Defaults with { SslVersion = SslVersion.Tls12 }, o => o with { SslVersion = SslVersion.Default });
        AssertChangesOnly(Defaults with { SslVersionMaximum = SslVersionMaximum.Tls13 }, o => o with { SslVersionMaximum = SslVersionMaximum.None });
        AssertChangesOnly(Defaults with { VerifyPeer = false }, o => o with { VerifyPeer = true });
        AssertChangesOnly(Defaults with { VerifyHost = false }, o => o with { VerifyHost = true });
        AssertChangesOnly(Defaults with { CertificateAuthorityFile = "ca.pem" }, o => o with { CertificateAuthorityFile = null });
        AssertChangesOnly(Defaults with { CertificateAuthorityBlob = blob }, o => o with { CertificateAuthorityBlob = null });
        AssertChangesOnly(Defaults with { CertificateAuthorityPath = "certs" }, o => o with { CertificateAuthorityPath = null });
        AssertChangesOnly(Defaults with { RevocationListFile = "crl.pem" }, o => o with { RevocationListFile = null });
        AssertChangesOnly(Defaults with { IssuerCertificate = "issuer.pem" }, o => o with { IssuerCertificate = null });
        AssertChangesOnly(Defaults with { IssuerCertificateBlob = blob }, o => o with { IssuerCertificateBlob = null });
        AssertChangesOnly(Defaults with { PinnedPublicKey = "sha256//abc" }, o => o with { PinnedPublicKey = null });
        AssertChangesOnly(Defaults with { CipherList = "AES128-SHA" }, o => o with { CipherList = null });
        AssertChangesOnly(Defaults with { Tls13Ciphers = "TLS_AES_128_GCM_SHA256" }, o => o with { Tls13Ciphers = null });
        AssertChangesOnly(Defaults with { SslOptions = SslOptions.NoRevoke }, o => o with { SslOptions = SslOptions.None });
    }

    [TestMethod]
    public void SslVersionMembers_CarryLibcurlsValues()
    {
        int[] expected = [0, 1, 2, 3, 4, 5, 6, 7];
        SslVersion[] members = [SslVersion.Default, SslVersion.Tls1, SslVersion.Ssl2, SslVersion.Ssl3, SslVersion.Tls10, SslVersion.Tls11, SslVersion.Tls12, SslVersion.Tls13];
        CollectionAssert.AreEqual(expected, Array.ConvertAll(members, m => (int)m));
    }

    [TestMethod]
    public void SslVersionMaximumMembers_CarryLibcurlsValues()
    {
        int[] expected = [0, 0x10000, 0x40000, 0x50000, 0x60000, 0x70000];
        SslVersionMaximum[] members = [SslVersionMaximum.None, SslVersionMaximum.Default, SslVersionMaximum.Tls10, SslVersionMaximum.Tls11, SslVersionMaximum.Tls12, SslVersionMaximum.Tls13];
        CollectionAssert.AreEqual(expected, Array.ConvertAll(members, m => (int)m));
    }

    [TestMethod]
    public void SslOptionsMembers_CarryLibcurlsBits()
    {
        int[] expected = [0, 1, 2, 4, 8, 16, 32, 64];
        SslOptions[] members = [SslOptions.None, SslOptions.AllowBeast, SslOptions.NoRevoke, SslOptions.NoPartialChain, SslOptions.RevokeBestEffort, SslOptions.NativeCa, SslOptions.AutoClientCert, SslOptions.EarlyData];
        CollectionAssert.AreEqual(expected, Array.ConvertAll(members, m => (int)m));
    }

    private static void AssertChangesOnly(TlsPeerOptions changed, Func<TlsPeerOptions, TlsPeerOptions> restore)
    {
        Assert.AreNotEqual(Defaults, changed);
        Assert.AreEqual(Defaults, restore(changed));
    }
}
