using Curl.Testing;

namespace Curl.Conformance;

public sealed partial class UpstreamCurlPlatformTests
{
    [TestMethod]
    [DataRow("sftp")]
    [DataRow("scp")]
    public void AllPlatforms_ListTheSshProtocolsTheHarnessEmulates(string feature)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("feature", feature);

        bool onEvery = UpstreamCurlPlatform.All.All(platform => platform.Features.Contains(feature));

        diagnostics.Act("listed on every platform", onEvery);
        diagnostics.Assert("listed on every platform", true, onEvery);
        Assert.IsTrue(onEvery);
    }
}
