using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Curl writes the C program --libcurl names, so the cases that compare it run (BL-2862).
    [TestMethod]
    public void AllPlatforms_ListTheLibcurlOption()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains("--libcurl"));

        diagnostics.Act("listed on every platform", onEvery);
        diagnostics.Assert("listed on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
    }
}
