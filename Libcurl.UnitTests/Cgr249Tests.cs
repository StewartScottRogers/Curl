using Curl.Api.Info;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Libcurl.Info;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-249: <c>CURLINFO_EFFECTIVE_METHOD</c> answers <c>CURLOPT_CUSTOMREQUEST</c> when one is set, as libcurl's
/// <c>getinfo_char</c> reads it from the option, even when a redirect hop under <c>CURLFOLLOW_FIRSTONLY</c> sent GET.
/// </summary>
[TestClass]
public sealed class Cgr249Tests
{
    [TestMethod]
    public void EffectiveMethod_CustomRequestSetAndLastHopSentGet_AnswersCustomRequest()
    {
        var options = new EasyOptions { Transfer = new EasyTransferOptions { CustomRequest = "PUT" } };

        object? method = EasyInfoReaders.Create()[CurlInfo.EffectiveMethod](new TransferInfo { EffectiveMethod = "GET" }, options);

        Assert.AreEqual("PUT", method);
    }

    [TestMethod]
    public void EffectiveMethod_NoCustomRequest_AnswersTheTransfersMethod()
    {
        object? method = EasyInfoReaders.Create()[CurlInfo.EffectiveMethod](new TransferInfo { EffectiveMethod = "POST" }, new EasyOptions());

        Assert.AreEqual("POST", method);
    }
}
