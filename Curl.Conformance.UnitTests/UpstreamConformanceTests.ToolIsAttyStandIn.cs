using System.Text;

namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Upstream's debug build treats standard output as a terminal when CURL_ISATTY is set (src/tool_cb_wrt.c at
    // curl-8_22_0); cases 1425 and 1426 rely on it to see binary output to a terminal refused, or allowed by
    // --output -. The harness stands in for it: such a case gets Debug, and RunCurlAsync composes curl with a
    // terminal standard output (BL-2908). The curl tool and Libcurl never read the variable.
    internal const string ToolIsAttyVariableName = "CURL_ISATTY";

    private static bool SetsToolIsAttyVariable(string text) =>
        text.Contains("\n" + ToolIsAttyVariableName + "=", StringComparison.Ordinal);

    internal static bool StandardOutputIsTerminalFor(IReadOnlyDictionary<string, string> environment) =>
        environment.ContainsKey(ToolIsAttyVariableName);

    [TestMethod]
    [DataRow("CURL_ISATTY=yes\n", true)]
    [DataRow("CURL_OTHER=yes\n", false)]
    public void PlatformFor_CaseSettingToolIsAtty_ReportsDebugOnlyThen(string setenv, bool reportsDebug)
    {
        byte[] testFile = Encoding.Latin1.GetBytes($"<testcase>\n<client>\n<server>\nhttp\n</server>\n<setenv>\n{setenv}</setenv>\n</client>\n</testcase>\n");

        UpstreamCurlPlatform platform = PlatformFor(testFile);

        Assert.AreEqual(reportsDebug, platform.Features.Contains("Debug"));
    }

    [TestMethod]
    public void StandardOutputIsTerminalFor_ToolIsAttySetOrNot_IsTrueOnlyThen()
    {
        Assert.IsTrue(StandardOutputIsTerminalFor(new Dictionary<string, string> { [ToolIsAttyVariableName] = "yes" }));
        Assert.IsFalse(StandardOutputIsTerminalFor(new Dictionary<string, string>()));
    }
}
