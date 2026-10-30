using Curl.Api.Codes;
using Curl.Api.Options.Tls;
using Libcurl.Options;
using static Libcurl.UnitTests.Options.Tls.TlsOptionAssert;

namespace Libcurl.UnitTests.Options.Tls;

[TestClass]
public sealed class TlsPeerOptionSettersTests
{
    [TestMethod]
    public void SetOption_Sslcert_SetsTheOriginAndProxySslcertTheProxy()
    {
        TwinText(CurlOption.Sslcert, CurlOption.ProxySslcert, p => p.Certificate);
    }

    [TestMethod]
    public void SetOption_SslcertBlob_CopiesTheBytesIntoTheOriginAndProxySslcertBlobIntoTheProxy()
    {
        TwinBlob(CurlOption.SslcertBlob, CurlOption.ProxySslcertBlob, p => p.CertificateBlob);
    }

    [TestMethod]
    public void SetOption_Sslcerttype_SetsTheOriginAndProxySslcerttypeTheProxy()
    {
        TwinText(CurlOption.Sslcerttype, CurlOption.ProxySslcerttype, p => p.CertificateType);
    }

    [TestMethod]
    public void SetOption_Sslkey_SetsTheOriginAndProxySslkeyTheProxy()
    {
        TwinText(CurlOption.Sslkey, CurlOption.ProxySslkey, p => p.Key);
    }

    [TestMethod]
    public void SetOption_SslkeyBlob_CopiesTheBytesIntoTheOriginAndProxySslkeyBlobIntoTheProxy()
    {
        TwinBlob(CurlOption.SslkeyBlob, CurlOption.ProxySslkeyBlob, p => p.KeyBlob);
    }

    [TestMethod]
    public void SetOption_Sslkeytype_SetsTheOriginAndProxySslkeytypeTheProxy()
    {
        TwinText(CurlOption.Sslkeytype, CurlOption.ProxySslkeytype, p => p.KeyType);
    }

    [TestMethod]
    public void SetOption_Keypasswd_SetsTheOriginAndProxyKeypasswdTheProxy()
    {
        TwinText(CurlOption.Keypasswd, CurlOption.ProxyKeypasswd, p => p.KeyPassword);
    }

    [TestMethod]
    public void SetOption_SslcertpasswdAndSslkeypasswd_AreKeypasswd()
    {
        Assert.AreEqual("a", SetOk(CurlOption.Sslcertpasswd, "a").Origin.KeyPassword);
        Assert.AreEqual("b", SetOk(CurlOption.Sslkeypasswd, "b").Origin.KeyPassword);
    }

    [TestMethod]
    public void SetOption_Sslversion_SplitsTheMinimumAndMaximumForTheOriginAndProxySslversionForTheProxy()
    {
        var value = (long)SslVersion.Tls12 | (long)SslVersionMaximum.Tls13;

        Twins(CurlOption.Sslversion, CurlOption.ProxySslversion, value, p => (p.SslVersion, p.SslVersionMaximum), (SslVersion.Tls12, SslVersionMaximum.Tls13));
        Twins(CurlOption.Sslversion, CurlOption.ProxySslversion, SslVersion.Tls1, p => (p.SslVersion, p.SslVersionMaximum), (SslVersion.Tls1, SslVersionMaximum.None));
        Twins(CurlOption.Sslversion, CurlOption.ProxySslversion, 0, p => p.SslVersion, SslVersion.Default);
    }

    [TestMethod]
    public void SetOption_Sslversion_RefusesSslTwoSslThreeAndVersionsPastTls13()
    {
        foreach (var option in new[] { CurlOption.Sslversion, CurlOption.ProxySslversion })
        {
            Answers(CurlCode.BadFunctionArgument, option, (long)SslVersion.Ssl2);
            Answers(CurlCode.BadFunctionArgument, option, (long)SslVersion.Ssl3);
            Answers(CurlCode.BadFunctionArgument, option, 8L);
            Answers(CurlCode.BadFunctionArgument, option, 8L << 16);
            Answers(CurlCode.BadFunctionArgument, option, -1L);
        }
    }

    [TestMethod]
    public void SetOption_SslVerifypeer_SwitchesTheOriginAndProxySslVerifypeerTheProxy()
    {
        TwinSwitch(CurlOption.SslVerifypeer, CurlOption.ProxySslVerifypeer, p => p.VerifyPeer);
    }

    [TestMethod]
    public void SetOption_SslVerifyhost_IsOnForEitherLowBitForTheOriginAndProxySslVerifyhostForTheProxy()
    {
        TwinSwitch(CurlOption.SslVerifyhost, CurlOption.ProxySslVerifyhost, p => p.VerifyHost);
        Twins(CurlOption.SslVerifyhost, CurlOption.ProxySslVerifyhost, 1L, p => p.VerifyHost, true);
        Twins(CurlOption.SslVerifyhost, CurlOption.ProxySslVerifyhost, 4L, p => p.VerifyHost, false);
    }

    [TestMethod]
    public void SetOption_Cainfo_SetsTheOriginAndProxyCainfoTheProxy()
    {
        TwinText(CurlOption.Cainfo, CurlOption.ProxyCainfo, p => p.CertificateAuthorityFile);
    }

    [TestMethod]
    public void SetOption_CainfoBlob_CopiesTheBytesIntoTheOriginAndProxyCainfoBlobIntoTheProxy()
    {
        TwinBlob(CurlOption.CainfoBlob, CurlOption.ProxyCainfoBlob, p => p.CertificateAuthorityBlob);
    }

    [TestMethod]
    public void SetOption_Pinnedpublickey_SetsTheOriginAndProxyPinnedpublickeyTheProxy()
    {
        TwinText(CurlOption.Pinnedpublickey, CurlOption.ProxyPinnedpublickey, p => p.PinnedPublicKey);
    }

    [TestMethod]
    public void SetOption_SslCipherList_SetsTheOriginAndProxySslCipherListTheProxy()
    {
        TwinText(CurlOption.SslCipherList, CurlOption.ProxySslCipherList, p => p.CipherList);
    }

    [TestMethod]
    public void SetOption_SslOptions_KeepsTheLowEightBitsForTheOriginAndProxySslOptionsForTheProxy()
    {
        var bits = SslOptions.NoRevoke | SslOptions.NativeCa;

        Twins(CurlOption.SslOptions, CurlOption.ProxySslOptions, (long)bits, p => p.SslOptions, bits);
        Twins(CurlOption.SslOptions, CurlOption.ProxySslOptions, 0x102L, p => p.SslOptions, SslOptions.NoRevoke);
    }
}
