namespace Curl.Api.Options.Tls;

[TestClass]
public sealed class EasyTlsOptionsTests
{
    private static readonly EasyTlsOptions Defaults = new();

    [TestMethod]
    public void New_EasyTlsOptions_GivesOriginAndProxyLibcurlsDefaults()
    {
        Assert.AreEqual(new TlsPeerOptions(), Defaults.Origin);
        Assert.AreEqual(new TlsPeerOptions(), Defaults.Proxy);
    }

    [TestMethod]
    public void New_EasyTlsOptions_LeavesEngineAndStringSettingsUnset()
    {
        Assert.IsNull(Defaults.Engine);
        Assert.IsFalse(Defaults.EngineIsDefault);
        Assert.IsNull(Defaults.EllipticCurves);
        Assert.IsNull(Defaults.SignatureAlgorithms);
        Assert.IsNull(Defaults.EncryptedClientHello);
        Assert.IsNull(Defaults.EncryptedClientHelloPublicName);
        Assert.IsNull(Defaults.EncryptedClientHelloConfigList);
    }

    [TestMethod]
    public void New_EasyTlsOptions_DefaultsOriginOnlySwitchesToLibcurls()
    {
        Assert.IsFalse(Defaults.VerifyStatus);
        Assert.IsFalse(Defaults.GatherCertificateInfo);
        Assert.IsTrue(Defaults.SessionIdCache);
        Assert.IsFalse(Defaults.FalseStart);
    }

    [TestMethod]
    public void New_EasyTlsOptions_KeepsTheCaStoreFor86400Seconds()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(86400), Defaults.CertificateAuthorityCacheTimeout);
    }

    [TestMethod]
    public void New_EasyTlsOptions_VerifiesDohPeerAndHostButNotStatus()
    {
        Assert.IsTrue(Defaults.DohVerifyPeer);
        Assert.IsTrue(Defaults.DohVerifyHost);
        Assert.IsFalse(Defaults.DohVerifyStatus);
    }

    [TestMethod]
    public void With_EachProperty_ChangesOnlyThatProperty()
    {
        var changedPeer = new TlsPeerOptions { VerifyPeer = false };

        AssertChangesOnly(Defaults with { Origin = changedPeer }, o => o with { Origin = new TlsPeerOptions() });
        AssertChangesOnly(Defaults with { Proxy = changedPeer }, o => o with { Proxy = new TlsPeerOptions() });
        AssertChangesOnly(Defaults with { Engine = "pkcs11" }, o => o with { Engine = null });
        AssertChangesOnly(Defaults with { EngineIsDefault = true }, o => o with { EngineIsDefault = false });
        AssertChangesOnly(Defaults with { VerifyStatus = true }, o => o with { VerifyStatus = false });
        AssertChangesOnly(Defaults with { CertificateAuthorityCacheTimeout = Timeout.InfiniteTimeSpan }, o => o with { CertificateAuthorityCacheTimeout = TimeSpan.FromSeconds(86400) });
        AssertChangesOnly(Defaults with { GatherCertificateInfo = true }, o => o with { GatherCertificateInfo = false });
        AssertChangesOnly(Defaults with { EllipticCurves = "X25519" }, o => o with { EllipticCurves = null });
        AssertChangesOnly(Defaults with { SignatureAlgorithms = "ECDSA+SHA256" }, o => o with { SignatureAlgorithms = null });
        AssertChangesOnly(Defaults with { SessionIdCache = false }, o => o with { SessionIdCache = true });
        AssertChangesOnly(Defaults with { FalseStart = true }, o => o with { FalseStart = false });
        AssertChangesOnly(Defaults with { EncryptedClientHello = "grease" }, o => o with { EncryptedClientHello = null });
        AssertChangesOnly(Defaults with { EncryptedClientHelloPublicName = "public.example" }, o => o with { EncryptedClientHelloPublicName = null });
        AssertChangesOnly(Defaults with { EncryptedClientHelloConfigList = "AEX+DQBB" }, o => o with { EncryptedClientHelloConfigList = null });
        AssertChangesOnly(Defaults with { DohVerifyPeer = false }, o => o with { DohVerifyPeer = true });
        AssertChangesOnly(Defaults with { DohVerifyHost = false }, o => o with { DohVerifyHost = true });
        AssertChangesOnly(Defaults with { DohVerifyStatus = true }, o => o with { DohVerifyStatus = false });
    }

    [TestMethod]
    public void With_Origin_LeavesProxyAtItsDefaults()
    {
        var changed = Defaults with { Origin = new TlsPeerOptions { Certificate = "c.pem" } };

        Assert.AreEqual("c.pem", changed.Origin.Certificate);
        Assert.AreEqual(new TlsPeerOptions(), changed.Proxy);
    }

    private static void AssertChangesOnly(EasyTlsOptions changed, Func<EasyTlsOptions, EasyTlsOptions> restore)
    {
        Assert.AreNotEqual(Defaults, changed);
        Assert.AreEqual(Defaults, restore(changed));
    }
}
