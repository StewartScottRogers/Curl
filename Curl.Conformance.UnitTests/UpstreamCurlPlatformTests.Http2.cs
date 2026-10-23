using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    [TestMethod]
    public void All_ReportHttp2AndH2c()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("platforms", "Windows, Unix, MacOS");

        bool everyPlatformHasBoth = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains("http/2") && platform.Features.Contains("h2c"));

        diagnostics.Assert("every platform has http/2 and h2c", true, everyPlatformHasBoth);
        Assert.IsTrue(everyPlatformHasBoth);
        Assert.IsTrue(UpstreamCurlPlatform.Windows.Features.Contains("http/2"));
        Assert.IsTrue(UpstreamCurlPlatform.Windows.Features.Contains("h2c"));
    }
}
