using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Libcurl, which runs the ported libtest programs, has CURLOPT_HTTPPOST, so test516 runs (BL-2593).
    [TestMethod]
    public void AllPlatforms_ListTheFormApi()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains("form-api"));

        diagnostics.Act("listed on every platform", onEvery);
        diagnostics.Assert("listed on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
    }
}
