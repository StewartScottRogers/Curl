using Curl.Api.Codes;
using Libcurl.Multi;
using Libcurl.UnitTests.Easy;
using Libcurl.UnitTests.Multi.Settings;

namespace Libcurl.UnitTests;

/// <summary>
/// CGR-243: <c>curl_multi_setopt</c> takes <c>CURLMOPT_RESOLVE_THREADS_MAX</c> (20) and <c>CURLMOPT_QUICK_EXIT</c> (21)
/// and answers <c>CURLM_OK</c>, as libcurl 8.22.0 does.
/// </summary>
[TestClass]
public sealed class Cgr243Tests
{
    [TestMethod]
    [DataRow(20)]
    [DataRow(21)]
    public async Task SetOption_ResolveThreadsMaxOrQuickExit_AnswersOk(int option)
    {
        await using var multi = new CurlMulti(new SettingsFakeMultiTransfer());

        Assert.AreEqual(CurlMultiCode.Ok, multi.SetOption((CurlMultiOption)option, 1L));
    }
}
