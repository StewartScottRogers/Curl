namespace Curl.Conformance;

public sealed partial class UpstreamConformanceTests
{
    // Upstream's DoH cases that run the curl tool with --doh-url (2100, 2101, 2117) name Debug so that the debug build
    // lets them use the DoH server without the usual checks. The harness routes --doh-url through its own dialer
    // (BL-2919), so a case naming the DoH feature and running --doh-url gets Debug (BL-2928). The DoH unit programs
    // (1650, 1655, 1658) run no --doh-url and see no Debug from this rule.
    private static bool RunsCurlWithDohUrl(string text) =>
        text.Contains("\nDoH\n", StringComparison.Ordinal) && text.Contains("--doh-url ", StringComparison.Ordinal);

    [TestMethod]
    [DataRow("<features>\nDebug\nDoH\n</features>\n<command>\nhttp://x/ --doh-url http://y/\n</command>\n", true)]
    [DataRow("<features>\nDebug\nDoH\n</features>\n<command>\nhttp://x/\n</command>\n", false)]
    [DataRow("<features>\nDebug\n</features>\n<command>\nhttp://x/ --doh-url http://y/\n</command>\n", false)]
    public void PlatformFor_CaseNamingDohAndRunningDohUrl_ReportsDebugOnlyThen(string client, bool reportsDebug)
    {
        byte[] testFile = System.Text.Encoding.Latin1.GetBytes($"<testcase>\n<client>\n<server>\nhttp\n</server>\n{client}</client>\n</testcase>\n");

        UpstreamCurlPlatform platform = PlatformFor(testFile);

        Assert.AreEqual(reportsDebug, platform.Features.Contains("Debug"));
    }

    [TestMethod]
    [DataRow(2100)]
    [DataRow(2101)]
    [DataRow(2117)]
    public void PlatformFor_VendoredDohUrlCase_ReportsDebug(int testNumber)
    {
        byte[] testFile = File.ReadAllBytes(Path.Combine(UpstreamTestDataFolder, $"test{testNumber}{UpstreamTestFileExtension}"));

        UpstreamCurlPlatform platform = PlatformFor(testFile);

        Assert.IsTrue(platform.Features.Contains("Debug"));
    }
}
