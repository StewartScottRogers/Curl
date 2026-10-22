using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Curl serves FTP, and cases such as test549 name `ftp` in <features>, so no platform leaves it off (BL-2610).
    [TestMethod]
    public void AllPlatforms_ListFtp()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains("ftp"));

        diagnostics.Act("listed on every platform", onEvery);
        diagnostics.Assert("listed on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
    }
}
