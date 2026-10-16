using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;
using static Libcurl.UnitTests.Options.Tls.TlsOptionAssert;

namespace Libcurl.UnitTests.Options.Tls;

[TestClass]
public sealed class EasyTlsOptionSettersTests
{
    [TestMethod]
    public void SetOption_Sslengine_AnswersNotBuiltInForANameAndClearsForNullOrEmpty()
    {
        Answers(CurlCode.NotBuiltIn, CurlOption.Sslengine, "pkcs11");

        Assert.IsNull(SetOk(CurlOption.Sslengine, null).Engine);
        Assert.IsNull(SetOk(CurlOption.Sslengine, string.Empty).Engine);
    }

    [TestMethod]
    public void SetOption_SslengineDefault_AnswersNotBuiltIn()
    {
        Answers(CurlCode.NotBuiltIn, CurlOption.SslengineDefault, 1L);
    }

    [TestMethod]
    public void SetOption_SslVerifystatus_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.SslVerifystatus, t => t.VerifyStatus);
    }

    [TestMethod]
    public void SetOption_CaCacheTimeout_TakesSecondsWithMinusOneForeverAndRefusesBelowMinusOne()
    {
        Assert.AreEqual(TimeSpan.FromSeconds(60), SetOk(CurlOption.CaCacheTimeout, 60L).CertificateAuthorityCacheTimeout);
        Assert.AreEqual(TimeSpan.Zero, SetOk(CurlOption.CaCacheTimeout, 0).CertificateAuthorityCacheTimeout);
        Assert.AreEqual(Timeout.InfiniteTimeSpan, SetOk(CurlOption.CaCacheTimeout, -1L).CertificateAuthorityCacheTimeout);
        Assert.AreEqual(TimeSpan.FromSeconds(int.MaxValue), SetOk(CurlOption.CaCacheTimeout, long.MaxValue).CertificateAuthorityCacheTimeout);
        Answers(CurlCode.BadFunctionArgument, CurlOption.CaCacheTimeout, -2L);
    }

    [TestMethod]
    public void SetOption_Certinfo_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.Certinfo, t => t.GatherCertificateInfo);
    }

    [TestMethod]
    public void SetOption_SslEcCurves_SetsTheCurvesAndNullClearsThem()
    {
        Text(CurlOption.SslEcCurves, t => t.EllipticCurves);
    }

    [TestMethod]
    public void SetOption_SslSignatureAlgorithms_SetsTheAlgorithmsAndNullClearsThem()
    {
        Text(CurlOption.SslSignatureAlgorithms, t => t.SignatureAlgorithms);
    }

    [TestMethod]
    public void SetOption_SslSessionidCache_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.SslSessionidCache, t => t.SessionIdCache);
    }

    [TestMethod]
    public void SetOption_SslFalsestart_AnswersNotBuiltIn()
    {
        Answers(CurlCode.NotBuiltIn, CurlOption.SslFalsestart, 1L);
    }

    [TestMethod]
    public void SetOption_Ech_SetsTheModeAndNullTurnsItOff()
    {
        foreach (var mode in new[] { "false", "grease", "true", "hard" })
        {
            Assert.AreEqual(mode, SetOk(CurlOption.Ech, mode).EncryptedClientHello);
        }

        var options = SetOk(new EasyOptions(), CurlOption.Ech, "hard");
        Assert.IsNull(SetOk(options, CurlOption.Ech, null).Tls.EncryptedClientHello);
    }

    [TestMethod]
    public void SetOption_Ech_KeepsTheConfigListAndPublicNameBesideTheMode()
    {
        var options = SetOk(new EasyOptions(), CurlOption.Ech, "true");
        options = SetOk(options, CurlOption.Ech, "ecl:AEX+");
        options = SetOk(options, CurlOption.Ech, "pn:example.com");

        Assert.AreEqual("true", options.Tls.EncryptedClientHello);
        Assert.AreEqual("AEX+", options.Tls.EncryptedClientHelloConfigList);
        Assert.AreEqual("example.com", options.Tls.EncryptedClientHelloPublicName);
    }

    [TestMethod]
    public void SetOption_Ech_ChangesNothingForAnUnknownWordOrAnEmptyPrefix()
    {
        var unset = new EasyOptions().Tls;

        foreach (var text in new[] { "maybe", "ecl:", "ecl:x", "pn:", "pn:x" })
        {
            Assert.AreEqual(unset, SetOk(CurlOption.Ech, text), text);
        }
    }

    [TestMethod]
    public void SetOption_DohSslVerifypeer_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.DohSslVerifypeer, t => t.DohVerifyPeer);
    }

    [TestMethod]
    public void SetOption_DohSslVerifyhost_IsOnForEitherLowBit()
    {
        Switch(CurlOption.DohSslVerifyhost, t => t.DohVerifyHost);
        Assert.IsTrue(SetOk(CurlOption.DohSslVerifyhost, 1L).DohVerifyHost);
        Assert.IsFalse(SetOk(CurlOption.DohSslVerifyhost, 4L).DohVerifyHost);
    }

    [TestMethod]
    public void SetOption_DohSslVerifystatus_IsOnForNonZeroAndOffForZero()
    {
        Switch(CurlOption.DohSslVerifystatus, t => t.DohVerifyStatus);
    }
}
