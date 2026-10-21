using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Curl refuses --http3 on every reference build, none of which has QUIC (ADR-0540), so runtests.pl skips
    // the http/3 cases for it as for the reference curl (BL-2370, GF-0094).
    [TestMethod]
    public void AllPlatforms_LeaveOffHttp3()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        bool onAny = UpstreamCurlPlatform.All.Any(platform => platform.Features.Contains("http/3"));

        diagnostics.Act("listed on any platform", onAny);
        diagnostics.Assert("listed on any platform", false, onAny);
        Assert.IsFalse(onAny);
    }
}
