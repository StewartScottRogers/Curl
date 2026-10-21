namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Upstream's debug build draws a libtest program's MIME boundary from the forced CURL_ENTROPY sequence its test
    // runner sets. A libtest case naming Mime (test1662) gets Debug, and CreateEngine gives its engine that boundary
    // whenever the case sets CURL_ENTROPY (ForcedEntropyFormBoundary, BL-2695, BL-2835).
    private static bool RunsMimeLibTest(string text) =>
        text.Contains("\nMime\n", StringComparison.Ordinal) && text.Contains("<tool>\nlib", StringComparison.Ordinal);

    [TestMethod]
    [DataRow("<features>\nMime\nDebug\n</features>\n<tool>\nlib1662\n</tool>\n", true)]
    [DataRow("<features>\nMime\nDebug\n</features>\n", false)]
    [DataRow("<features>\nDebug\n</features>\n<tool>\nlib1662\n</tool>\n", false)]
    public void PlatformFor_MimeLibTestCase_ReportsDebugOnlyThen(string client, bool reportsDebug)
    {
        byte[] testFile = System.Text.Encoding.Latin1.GetBytes($"<testcase>\n<client>\n<server>\nhttp\n</server>\n{client}</client>\n</testcase>\n");

        UpstreamCurlPlatform platform = PlatformFor(testFile);

        Assert.AreEqual(reportsDebug, platform.Features.Contains("Debug"));
    }
}
