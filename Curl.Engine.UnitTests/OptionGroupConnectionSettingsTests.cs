using Curl.Api.Options.Http;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;

namespace Curl.Engine;

/// <summary>
/// Pins that two option groups whose <c>--cert-type</c>, <c>--key-type</c>,
/// <c>--proxy-cert-type</c> or <c>--proxy-key-type</c> differ only in letter case read equal
/// settings, and so share a pooled connection, as curl 8.22.0's <c>match_ssl_primary_config</c>
/// compares them with <c>curl_strequal</c> (<c>lib/vtls/vtls_config.c</c>, BL-2974), while
/// different types do not.
/// </summary>
[TestClass]
public sealed class OptionGroupConnectionSettingsTests
{
    [TestMethod]
    public void Of_CertificateTypesDifferingOnlyInCase_AreEqual() =>
        Assert.AreEqual(SettingsOf(Origin(certificateType: "PEM")), SettingsOf(Origin(certificateType: "pem")));

    [TestMethod]
    public void Of_KeyTypesDifferingOnlyInCase_AreEqual() =>
        Assert.AreEqual(SettingsOf(Origin(keyType: "DER")), SettingsOf(Origin(keyType: "der")));

    [TestMethod]
    public void Of_DifferentCertificateTypes_AreNotEqual() =>
        Assert.AreNotEqual(SettingsOf(Origin(certificateType: "PEM")), SettingsOf(Origin(certificateType: "DER")));

    [TestMethod]
    public void Of_DifferentKeyTypes_AreNotEqual() =>
        Assert.AreNotEqual(SettingsOf(Origin(keyType: "PEM")), SettingsOf(Origin(keyType: "DER")));

    [TestMethod]
    public void Of_ProxyCertificateAndKeyTypesDifferingOnlyInCase_AreEqual() =>
        Assert.AreEqual(SettingsOf(Proxy("P12", "Eng")), SettingsOf(Proxy("p12", "ENG")));

    [TestMethod]
    public void Of_DifferentProxyCertificateTypes_AreNotEqual() =>
        Assert.AreNotEqual(SettingsOf(Proxy("PEM", null)), SettingsOf(Proxy("P12", null)));

    [TestMethod]
    public void Of_NoTypes_AreEqual() =>
        Assert.AreEqual(SettingsOf(new EasyTlsOptions()), SettingsOf(new EasyTlsOptions()));

    private static EasyTlsOptions Origin(string? certificateType = null, string? keyType = null) =>
        new() { Origin = new TlsPeerOptions { Certificate = "client.pem", CertificateType = certificateType, KeyType = keyType } };

    private static EasyTlsOptions Proxy(string? certificateType, string? keyType) =>
        new() { Proxy = new TlsPeerOptions { Certificate = "client.pem", CertificateType = certificateType, KeyType = keyType } };

    private static OptionGroupConnectionSettings SettingsOf(EasyTlsOptions tls) =>
        OptionGroupConnectionSettings.Of(new EngineToolOptions(false, false, null, null), new EasyNetworkOptions(), tls, new EasyHttpOptions(), new EasyProxyOptions());
}
