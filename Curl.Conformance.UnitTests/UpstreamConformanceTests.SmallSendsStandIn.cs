namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Upstream's debug build reads CURL_SMALLREQSEND and CURL_SMALLSENDS to split what curl sends (363, 1294, 1295,
    // 5028). The harness stands in for both with UpstreamSmallSendsFront, so a case setting either gets Debug (BL-2832).
    private static bool SetsSmallSendsVariable(string text) =>
        text.Contains("\n" + UpstreamSmallSendsFront.FirstWriteVariableName + "=", StringComparison.Ordinal)
        || text.Contains("\n" + UpstreamSmallSendsFront.EveryWriteVariableName + "=", StringComparison.Ordinal);

    [TestMethod]
    [DataRow("CURL_SMALLREQSEND=128\n", true)]
    [DataRow("CURL_SMALLSENDS=1\n", true)]
    [DataRow("CURL_SMALLREQSEND=8\nCURL_SMALLSENDS=8\n", true)]
    [DataRow("CURL_OTHER=8\n", false)]
    public void PlatformFor_CaseSettingASmallSendsVariable_ReportsDebugOnlyThen(string setenv, bool reportsDebug)
    {
        byte[] testFile = System.Text.Encoding.Latin1.GetBytes($"<testcase>\n<client>\n<server>\nhttp\n</server>\n<setenv>\n{setenv}</setenv>\n</client>\n</testcase>\n");

        UpstreamCurlPlatform platform = PlatformFor(testFile);

        Assert.AreEqual(reportsDebug, platform.Features.Contains("Debug"));
    }
}
