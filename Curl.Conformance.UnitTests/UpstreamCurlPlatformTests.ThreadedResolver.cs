using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Curl's Features line names threadsafe and AsynchDNS without c-ares, which runtests.pl reads as
    // threaded-resolver, so case 3026 runs and case 506 is skipped as for the reference curl (BL-2700, ADR-0695).
    [TestMethod]
    [DataRow("threadsafe", true)]
    [DataRow("threaded-resolver", true)]
    [DataRow("c-ares", false)]
    public void AllPlatforms_ListTheThreadSafeLibraryAndThreadedResolver(string feature, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("feature", feature);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains(feature) == expected);

        diagnostics.Act("matches on every platform", onEvery);
        diagnostics.Assert("matches on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
    }
}
