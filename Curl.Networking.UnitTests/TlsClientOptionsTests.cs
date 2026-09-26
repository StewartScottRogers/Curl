namespace Curl.Networking;

/// <summary>
/// <see cref="TlsClientOptions" />: curl's defaults, and copies made with <c>with</c>.
/// </summary>
[TestClass]
public sealed class TlsClientOptionsTests
{
    [TestMethod]
    public void Constructor_WithNoArguments_VerifiesAgainstTheSystemStoreAtTheSystemDefaultVersion()
    {
        var options = new TlsClientOptions();

        Assert.IsFalse(options.Insecure);
        Assert.AreEqual(TlsMinimumVersion.SystemDefault, options.MinimumVersion);
        Assert.IsNull(options.CaCertificateFile);
    }

    [TestMethod]
    public void With_EveryProperty_CopiesTheOptionsWithTheNewValues()
    {
        var original = new TlsClientOptions();

        var changed = original with
        {
            Insecure = true,
            MinimumVersion = TlsMinimumVersion.Tls13,
            CaCertificateFile = "ca.pem",
        };

        Assert.IsTrue(changed.Insecure);
        Assert.AreEqual(TlsMinimumVersion.Tls13, changed.MinimumVersion);
        Assert.AreEqual("ca.pem", changed.CaCertificateFile);
        Assert.AreEqual(new TlsClientOptions(), original);
    }
}
