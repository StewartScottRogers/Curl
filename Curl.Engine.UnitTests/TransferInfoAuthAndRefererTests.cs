using Curl.Api.Info;
using Curl.Console;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// <c>CURLINFO_HTTPAUTH_AVAIL/USED</c> and <c>CURLINFO_PROXYAUTH_AVAIL/USED</c> from the report's offered
/// and picked schemes, as libcurl 8.22.0 answers them (BL-3258): curl 8.22.0 with <c>--anyauth</c>
/// against a 401 offering Basic and Digest resends with Digest, so AVAIL is Basic|Digest and USED Digest.
/// </summary>
[TestClass]
public sealed class TransferInfoAuthAndRefererTests
{
    private const long CurlAuthBasic = 1;
    private const long CurlAuthDigest = 2;

    private static TransferInfo Of(TransferReport report) =>
        TransferInfoFromReport.Of(new TransferResult(CurlExitCode.Ok, 0) { Report = report }, "http://h/x", fileTimeRequested: false, new ManualTimeProvider());

    [TestMethod]
    public void Of_401OfferingBasicAndDigestAnsweredWithAnyAuth_AnswersBothAvailableAndDigestUsed()
    {
        var info = Of(new TransferReport
        {
            ResponseCode = 200,
            AuthSchemesOffered = HttpAuthSchemes.Basic | HttpAuthSchemes.Digest,
            AuthSchemePicked = HttpAuthSchemes.Digest,
        });

        Assert.AreEqual(CurlAuthBasic | CurlAuthDigest, info.HttpAuthAvailable);
        Assert.AreEqual(CurlAuthDigest, info.HttpAuthUsed);
        Assert.AreEqual(0, info.ProxyAuthAvailable);
        Assert.AreEqual(0, info.ProxyAuthUsed);
    }

    [TestMethod]
    public void Of_PlainBasicSentUnchallenged_AnswersNothingAvailableAndBasicUsed()
    {
        var info = Of(new TransferReport { ResponseCode = 200, AuthSchemePicked = HttpAuthSchemes.Basic });

        Assert.AreEqual(0, info.HttpAuthAvailable);
        Assert.AreEqual(CurlAuthBasic, info.HttpAuthUsed);
    }

    [TestMethod]
    public void Of_401OfferingBasicAnsweredWithBasic_AnswersBasicAvailableAndUsed()
    {
        var info = Of(new TransferReport { ResponseCode = 200, AuthSchemesOffered = HttpAuthSchemes.Basic, AuthSchemePicked = HttpAuthSchemes.Basic });

        Assert.AreEqual(CurlAuthBasic, info.HttpAuthAvailable);
        Assert.AreEqual(CurlAuthBasic, info.HttpAuthUsed);
    }

    [TestMethod]
    public void Of_407OfferingBasicAndDigestAnsweredWithProxyAnyAuth_AnswersBothProxyAvailableAndDigestUsed()
    {
        var info = Of(new TransferReport
        {
            ResponseCode = 200,
            ProxyAuthSchemesOffered = HttpAuthSchemes.Basic | HttpAuthSchemes.Digest,
            ProxyAuthSchemePicked = HttpAuthSchemes.Digest,
        });

        Assert.AreEqual(CurlAuthBasic | CurlAuthDigest, info.ProxyAuthAvailable);
        Assert.AreEqual(CurlAuthDigest, info.ProxyAuthUsed);
        Assert.AreEqual(0, info.HttpAuthAvailable);
        Assert.AreEqual(0, info.HttpAuthUsed);
    }

    [TestMethod]
    public void Of_407OfferingBasicAnsweredWithBasic_AnswersBasicProxyAvailableAndUsed()
    {
        var info = Of(new TransferReport { ResponseCode = 200, ProxyAuthSchemesOffered = HttpAuthSchemes.Basic, ProxyAuthSchemePicked = HttpAuthSchemes.Basic });

        Assert.AreEqual(CurlAuthBasic, info.ProxyAuthAvailable);
        Assert.AreEqual(CurlAuthBasic, info.ProxyAuthUsed);
    }

    [TestMethod]
    public void Of_Final401WithChallengeHeadersButNoOfferedSchemes_AnswersNothingAvailable() =>
        Assert.AreEqual(0, Of(new TransferReport { ResponseCode = 401, ResponseHeaders = [new("WWW-Authenticate", "Basic")] }).HttpAuthAvailable);

    [TestMethod]
    public void Of_EveryOfferedScheme_AnswersEveryCurlAuthBit() =>
        Assert.AreEqual(1 | 2 | 4 | 8 | 64, Of(new TransferReport { ProxyAuthSchemesOffered = HttpAuthSchemes.Any | HttpAuthSchemes.Bearer }).ProxyAuthAvailable);
}
