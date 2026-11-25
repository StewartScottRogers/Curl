using Curl.Extension.Abstractions;
using Curl.Networking;

namespace Curl.Extension.EncryptedDns.DnsOverTls;

[TestClass]
public sealed class DnsOverTlsTrustTests
{
    private static ExtensionOptionValues ValuesWith(ExtensionTlsTrust trust) =>
        new(new Dictionary<string, string> { ["dns-over-tls"] = "dot.example" }, trust);

    [TestMethod]
    public void ActivateDnsOverTls_CaCertificatePathAndSslNoRevoke_VerifiesWithThem()
    {
        var hook = (DnsOverTlsResolverHook)EncryptedDnsExtension.ActivateDnsOverTls(ValuesWith(new ExtensionTlsTrust(false, "ca.pem", "capath", SkipRevocationCheck: true)))!;

        Assert.AreEqual(new TlsClientOptions(false, CaCertificateFile: "ca.pem", CaCertificateDirectory: "capath", SkipRevocationCheck: true), hook.TlsOptions);
    }

    [TestMethod]
    public void ActivateDnsOverTls_Insecure_SkipsVerification() =>
        Assert.IsTrue(((DnsOverTlsResolverHook)EncryptedDnsExtension.ActivateDnsOverTls(ValuesWith(new ExtensionTlsTrust(true)))!).TlsOptions.Insecure);

    [TestMethod]
    public void ActivateDnsOverTls_NoTrustOptions_UsesTheSystemStoreAndVerifies()
    {
        var hook = (DnsOverTlsResolverHook)EncryptedDnsExtension.ActivateDnsOverTls(ValuesWith(new ExtensionTlsTrust()))!;

        Assert.AreEqual(new TlsClientOptions(), hook.TlsOptions);
        Assert.AreEqual(new TlsClientOptions(), new DnsOverTlsResolverHook(new DnsOverTlsServer("dot.example")).TlsOptions);
    }

    [TestMethod]
    public void ExtensionOptionValues_WithoutTrust_HoldsTheDefaultTrust() =>
        Assert.AreEqual(new ExtensionTlsTrust(), ExtensionOptionValues.None.TlsTrust);
}
