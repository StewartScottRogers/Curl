using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;
using static Libcurl.UnitTests.Options.Tls.TlsOptionAssert;

namespace Libcurl.UnitTests.Options.Tls;

[TestClass]
public sealed class EasyTlsOptionSettersTests
{
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
}
