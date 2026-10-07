using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="TlsClientOptions" />: curl's defaults, and copies made with <c>with</c>.
/// </summary>
[TestClass]
public sealed class TlsClientOptionsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Constructor_WithNoArguments_VerifiesAgainstTheSystemStoreAtTheSystemDefaultVersionWithNoClientCertificate()
    {
        Diagnostics.Arrange("options", "new TlsClientOptions()");

        var options = new TlsClientOptions();

        Diagnostics.Act("insecure", options.Insecure);
        Diagnostics.Act("minimum version", options.MinimumVersion);
        Diagnostics.Act("options", options);
        Diagnostics.Assert("insecure", false, options.Insecure);
        Diagnostics.Assert("minimum version", TlsVersion.SystemDefault, options.MinimumVersion);

        Assert.IsFalse(options.Insecure);
        Assert.AreEqual(TlsVersion.SystemDefault, options.MinimumVersion);
        Assert.IsNull(options.CaCertificateFile);
        Assert.IsNull(options.CaCertificateDirectory);
        Assert.IsNull(options.ClientCertificate);
        Assert.IsNull(options.PrivateKey);
        Assert.IsNull(options.Ciphers);
        Assert.IsNull(options.Tls13Ciphers);
    }

    [TestMethod]
    public void With_EveryProperty_CopiesTheOptionsWithTheNewValues()
    {
        var original = new TlsClientOptions();
        Diagnostics.Arrange("original", original);
        Diagnostics.Arrange("changed values", "Insecure, Tls13, ca.pem, certs, client.p12:secret, key.pem, two cipher lists");

        var changed = original with
        {
            Insecure = true,
            MinimumVersion = TlsVersion.Tls13,
            CaCertificateFile = "ca.pem",
            CaCertificateDirectory = "certs",
            ClientCertificate = "client.p12:secret",
            PrivateKey = "key.pem",
            Ciphers = "ECDHE-RSA-AES128-GCM-SHA256",
            Tls13Ciphers = "TLS_AES_128_GCM_SHA256",
        };

        Diagnostics.Act("changed", changed);
        Diagnostics.Act("original", original);
        Diagnostics.Assert("changed insecure", true, changed.Insecure);
        Diagnostics.Assert("changed minimum version", TlsVersion.Tls13, changed.MinimumVersion);
        Diagnostics.Assert("original unchanged", new TlsClientOptions(), original);

        Assert.IsTrue(changed.Insecure);
        Assert.AreEqual(TlsVersion.Tls13, changed.MinimumVersion);
        Assert.AreEqual("ca.pem", changed.CaCertificateFile);
        Assert.AreEqual("certs", changed.CaCertificateDirectory);
        Assert.AreEqual("client.p12:secret", changed.ClientCertificate);
        Assert.AreEqual("key.pem", changed.PrivateKey);
        Assert.AreEqual("ECDHE-RSA-AES128-GCM-SHA256", changed.Ciphers);
        Assert.AreEqual("TLS_AES_128_GCM_SHA256", changed.Tls13Ciphers);
        Assert.AreEqual(new TlsClientOptions(), original);
    }

    [TestMethod]
    public void Constructor_WithNoArguments_LeavesTheTenOptionsOfAdr0151NotGiven()
    {
        Diagnostics.Arrange("options", "new TlsClientOptions()");

        var options = new TlsClientOptions();

        Diagnostics.Act("allow early data", options.AllowEarlyData);
        Diagnostics.Act("curves", options.Curves ?? "null");
        Diagnostics.Act("tls user", options.TlsUser ?? "null");
        Diagnostics.Assert("allow early data", false, options.AllowEarlyData);
        Diagnostics.Assert("curves", "null", options.Curves ?? "null");

        Assert.IsNull(options.Curves);
        Assert.IsNull(options.SignatureAlgorithms);
        Assert.IsFalse(options.AllowEarlyData);
        Assert.IsNull(options.Ech);
        Assert.IsNull(options.EchPublicName);
        Assert.IsNull(options.EchConfigList);
        Assert.IsNull(options.SslSessionsFile);
        Assert.IsNull(options.Engine);
        Assert.IsNull(options.TlsUser);
        Assert.IsNull(options.TlsPassword);
        Assert.IsNull(options.TlsAuthType);
    }

    [TestMethod]
    public void With_TheTenOptionsOfAdr0151_CopiesTheOptionsWithTheNewValues()
    {
        Diagnostics.Arrange("changed values", "X25519, ECDSA+SHA256, early data, hard, example.com, AEX+DQ==, sess.bin, pkcs11, user, secret, SRP");

        var changed = new TlsClientOptions() with
        {
            Curves = "X25519",
            SignatureAlgorithms = "ECDSA+SHA256",
            AllowEarlyData = true,
            Ech = "hard",
            EchPublicName = "example.com",
            EchConfigList = "AEX+DQ==",
            SslSessionsFile = "sess.bin",
            Engine = "pkcs11",
            TlsUser = "user",
            TlsPassword = "secret",
            TlsAuthType = "SRP",
        };

        Diagnostics.Act("curves", changed.Curves);
        Diagnostics.Act("ech", changed.Ech);
        Diagnostics.Act("tls auth type", changed.TlsAuthType);
        Diagnostics.Assert("curves", "X25519", changed.Curves);
        Diagnostics.Assert("allow early data", true, changed.AllowEarlyData);

        Assert.AreEqual("X25519", changed.Curves);
        Assert.AreEqual("ECDSA+SHA256", changed.SignatureAlgorithms);
        Assert.IsTrue(changed.AllowEarlyData);
        Assert.AreEqual("hard", changed.Ech);
        Assert.AreEqual("example.com", changed.EchPublicName);
        Assert.AreEqual("AEX+DQ==", changed.EchConfigList);
        Assert.AreEqual("sess.bin", changed.SslSessionsFile);
        Assert.AreEqual("pkcs11", changed.Engine);
        Assert.AreEqual("user", changed.TlsUser);
        Assert.AreEqual("secret", changed.TlsPassword);
        Assert.AreEqual("SRP", changed.TlsAuthType);
    }
}
