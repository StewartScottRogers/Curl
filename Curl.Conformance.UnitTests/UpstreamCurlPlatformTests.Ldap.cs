using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    // Curl's curl -V lists ldap, ldaps and rtsp on every platform, so case 445 runs (BL-2856).
    [TestMethod]
    public void AllPlatforms_ListLdapLdapsAndRtsp()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains("ldap") && platform.Features.Contains("ldaps") && platform.Features.Contains("rtsp"));

        diagnostics.Act("listed on every platform", onEvery);
        diagnostics.Assert("listed on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
    }

    // A case run under the feature mask sees the platform without the hidden features, the rest unchanged (BL-2856).
    [TestMethod]
    public void WithoutFeatures_Proxy_TakesOffProxyOnly()
    {
        var diagnostics = TestDiagnostics.For(TestContext);

        UpstreamCurlPlatform platform = UpstreamCurlPlatform.Windows.WithoutFeatures(["proxy"]);

        diagnostics.Act("feature count", platform.Features.Count);
        diagnostics.Assert("proxy is gone", false, platform.Features.Contains("proxy"));
        Assert.DoesNotContain("proxy", platform.Features);
        Assert.HasCount(UpstreamCurlPlatform.Windows.Features.Count - 1, platform.Features);
        Assert.AreEqual(UpstreamCurlPlatform.Windows.NullDevice, platform.NullDevice);
    }
}
