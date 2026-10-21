using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Libcurl, which runs the ported libtest programs, has curl_multi_wakeup, so cases 1564, 1565, 2412 and 2414 run (BL-2661).
    [TestMethod]
    public void AllPlatforms_ListWakeup()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains("wakeup"));

        diagnostics.Act("listed on every platform", onEvery);
        diagnostics.Assert("listed on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
    }
}
