using Curl.Api.Client;
using Curl.Api.Options.Tls;

namespace Curl.Api.UnitTests.Options.Transport;

[TestClass]
public sealed class CurlTlsOptionsTests
{
    private static async Task<EasyTlsOptions> ReceivedByEngine(CurlTlsOptions tls)
    {
        FakeCurlEngine engine = new();
        await using CurlClient client = new(engine, new CurlClientOptions { Tls = tls }, ownsEngine: true);
        client.CreateEasy();
        return engine.CreatedEasyHandles.Single().Options.Tls;
    }

    private static CurlTlsPeerOptions Peer(string tag) => new()
    {
        Certificate = tag + ".crt",
        CertificateBlob = new byte[] { 1 },
        CertificateType = "DER",
        Key = tag + ".key",
        KeyBlob = new byte[] { 2 },
        KeyType = "ENG",
        KeyPassword = tag + "-pass",
        SslVersion = SslVersion.Tls12,
        SslVersionMaximum = SslVersionMaximum.Tls13,
        VerifyPeer = false,
        VerifyHost = false,
        CertificateAuthorityFile = tag + "-ca.pem",
        CertificateAuthorityBlob = new byte[] { 3 },
        CertificateAuthorityPath = tag + "-ca",
        RevocationListFile = tag + ".crl",
        IssuerCertificate = tag + "-issuer.pem",
        IssuerCertificateBlob = new byte[] { 4 },
        PinnedPublicKey = "sha256//" + tag,
        CipherList = tag + "-ciphers",
        Tls13Ciphers = tag + "-tls13",
        SslOptions = SslOptions.NoRevoke | SslOptions.NativeCa,
    };

    private static void AssertPeer(string tag, TlsPeerOptions easy)
    {
        Assert.AreEqual(tag + ".crt", easy.Certificate);
        CollectionAssert.AreEqual(new byte[] { 1 }, easy.CertificateBlob!.Value.ToArray());
        Assert.AreEqual("DER", easy.CertificateType);
        Assert.AreEqual(tag + ".key", easy.Key);
        CollectionAssert.AreEqual(new byte[] { 2 }, easy.KeyBlob!.Value.ToArray());
        Assert.AreEqual("ENG", easy.KeyType);
        Assert.AreEqual(tag + "-pass", easy.KeyPassword);
        Assert.AreEqual(SslVersion.Tls12, easy.SslVersion);
        Assert.AreEqual(SslVersionMaximum.Tls13, easy.SslVersionMaximum);
        Assert.IsFalse(easy.VerifyPeer);
        Assert.IsFalse(easy.VerifyHost);
        Assert.AreEqual(tag + "-ca.pem", easy.CertificateAuthorityFile);
        CollectionAssert.AreEqual(new byte[] { 3 }, easy.CertificateAuthorityBlob!.Value.ToArray());
        Assert.AreEqual(tag + "-ca", easy.CertificateAuthorityPath);
        Assert.AreEqual(tag + ".crl", easy.RevocationListFile);
        Assert.AreEqual(tag + "-issuer.pem", easy.IssuerCertificate);
        CollectionAssert.AreEqual(new byte[] { 4 }, easy.IssuerCertificateBlob!.Value.ToArray());
        Assert.AreEqual("sha256//" + tag, easy.PinnedPublicKey);
        Assert.AreEqual(tag + "-ciphers", easy.CipherList);
        Assert.AreEqual(tag + "-tls13", easy.Tls13Ciphers);
        Assert.AreEqual(SslOptions.NoRevoke | SslOptions.NativeCa, easy.SslOptions);
    }

    [TestMethod]
    public async Task EveryMember_ReachesTheEngineUnderItsEasyName()
    {
        CurlTlsOptions tls = new()
        {
            Origin = Peer("origin"),
            Proxy = Peer("proxy"),
            Engine = "pkcs11",
            EngineIsDefault = true,
            VerifyStatus = true,
            CertificateAuthorityCacheTimeout = TimeSpan.FromSeconds(30),
            GatherCertificateInfo = true,
            EllipticCurves = "X25519",
            SignatureAlgorithms = "ECDSA+SHA256",
            SessionIdCache = false,
            FalseStart = true,
            EncryptedClientHello = "hard",
            EncryptedClientHelloPublicName = "public.example",
            EncryptedClientHelloConfigList = "AEX+",
            DohVerifyPeer = false,
            DohVerifyHost = false,
            DohVerifyStatus = true,
        };

        EasyTlsOptions easy = await ReceivedByEngine(tls);

        AssertPeer("origin", easy.Origin);
        AssertPeer("proxy", easy.Proxy);
        Assert.AreEqual("pkcs11", easy.Engine);
        Assert.IsTrue(easy.EngineIsDefault);
        Assert.IsTrue(easy.VerifyStatus);
        Assert.AreEqual(TimeSpan.FromSeconds(30), easy.CertificateAuthorityCacheTimeout);
        Assert.IsTrue(easy.GatherCertificateInfo);
        Assert.AreEqual("X25519", easy.EllipticCurves);
        Assert.AreEqual("ECDSA+SHA256", easy.SignatureAlgorithms);
        Assert.IsFalse(easy.SessionIdCache);
        Assert.IsTrue(easy.FalseStart);
        Assert.AreEqual("hard", easy.EncryptedClientHello);
        Assert.AreEqual("public.example", easy.EncryptedClientHelloPublicName);
        Assert.AreEqual("AEX+", easy.EncryptedClientHelloConfigList);
        Assert.IsFalse(easy.DohVerifyPeer);
        Assert.IsFalse(easy.DohVerifyHost);
        Assert.IsTrue(easy.DohVerifyStatus);
    }

    [TestMethod]
    public async Task DefaultRecord_LeavesTheEngineAtLibcurlDefaults()
    {
        EasyTlsOptions easy = await ReceivedByEngine(new CurlTlsOptions());

        Assert.AreEqual(new EasyTlsOptions(), easy);
    }

    [TestMethod]
    public void InfiniteCertificateAuthorityCacheTimeout_IsAccepted()
    {
        CurlTlsOptions tls = new() { CertificateAuthorityCacheTimeout = Timeout.InfiniteTimeSpan };

        Assert.AreEqual(Timeout.InfiniteTimeSpan, tls.CertificateAuthorityCacheTimeout);
    }

    [TestMethod]
    public void InvalidValues_ThrowAsTheyAreSet()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlTlsOptions { Origin = null! });
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlTlsOptions { Proxy = null! });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlTlsOptions { CertificateAuthorityCacheTimeout = TimeSpan.FromSeconds(-5) });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlTlsPeerOptions { SslVersion = (SslVersion)8 });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlTlsPeerOptions { SslVersionMaximum = (SslVersionMaximum)(8 << 16) });
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CurlTlsPeerOptions { SslOptions = (SslOptions)(1 << 7) });
    }
}
