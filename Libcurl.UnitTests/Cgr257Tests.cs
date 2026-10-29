using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Libcurl.Share;
using Libcurl.UnitTests.Share;

namespace Libcurl.UnitTests;

/// <summary>CGR-257: <c>CURLSHOPT_UNSHARE</c> of <c>CURL_LOCK_DATA_PSL</c> answers <c>CURLSHE_BAD_OPTION</c>, as libcurl 8.22.0 does.</summary>
[TestClass]
public sealed class Cgr257Tests
{
    [TestMethod]
    public void SetOption_UnsharePsl_AnswersBadOption()
    {
        var share = new CurlShare(new FakeShareHandle());

        Assert.AreEqual(CurlShareCode.BadOption, share.SetOption(CurlShareOption.Unshare, CurlLockData.Psl));
    }
}
