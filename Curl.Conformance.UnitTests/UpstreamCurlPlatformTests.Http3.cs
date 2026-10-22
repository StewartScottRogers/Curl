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

    // The harness reports http/3 only to a case naming its http/3 stand-in server, as for 2500 to 2503 (ADR-0720, BL-2838).
    [TestMethod]
    [DataRow("<client>\n<server>\nhttp/3\n</server>\n</client>\n", true)]
    [DataRow("<client>\n<server>\nhttp\nhttp/3\n</server>\n</client>\n", true)]
    [DataRow("<client>\n<server>\nhttp\n</server>\n<features>\nhttp/3\n</features>\n</client>\n", false)]
    [DataRow("<client>\n<server>\nhttp/2\n</server>\n</client>\n", false)]
    [DataRow("<client>\n<name>\nno server part\n</name>\n</client>\n", false)]
    [DataRow("<client>\n<server>\nhttp/3\n", true)]
    public void WithHttp3StandIn_CaseServers_ReportsHttp3OnlyForTheHttp3Server(string testFileText, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("test file text", testFileText);

        UpstreamCurlPlatform platform = UpstreamConformanceTests.WithHttp3StandIn(UpstreamCurlPlatform.Unix, testFileText);

        diagnostics.Act("reports http/3", platform.Features.Contains("http/3"));
        diagnostics.Assert("reports http/3", expected, platform.Features.Contains("http/3"));
        Assert.AreEqual(expected, platform.Features.Contains("http/3"));
        Assert.AreEqual(expected, platform.Features.Contains("Debug"));
    }
}
