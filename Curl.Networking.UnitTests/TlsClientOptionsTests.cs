namespace Curl.Networking;

/// <summary>
/// <see cref="TlsClientOptions" />: curl's defaults, and copies made with <c>with</c>.
/// </summary>
[TestClass]
public sealed class TlsClientOptionsTests
{
    [TestMethod]
    public void Constructor_WithNoArguments_VerifiesAgainstTheSystemStoreAtTheSystemDefaultVersionWithNoClientCertificate()
    {
        var options = new TlsClientOptions();

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
}
