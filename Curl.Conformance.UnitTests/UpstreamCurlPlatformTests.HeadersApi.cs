using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Curl writes -w %header{name} and %{header_json}, which curl writes only with libcurl's headers API (BL-2264).
    [TestMethod]
    public void AllPlatforms_ListTheHeadersApi()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains("headers-api"));

        diagnostics.Act("listed on every platform", onEvery);
        diagnostics.Assert("listed on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
    }
}
