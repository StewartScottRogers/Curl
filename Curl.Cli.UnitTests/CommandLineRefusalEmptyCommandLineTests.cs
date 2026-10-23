using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins what bare <c>curl</c> (no arguments) prints: curl 8.22.0's try-help line alone on standard error,
/// with no <c>--ai-help</c> in it, and exit 2 (BL-3185, superseding ADR-0762). Measured on Windows on
/// 2026-10-22 with curl 8.22.0 (Schannel); off Windows the line is upstream 8.22.0's <c>helpf</c> in
/// <c>src/tool_helpers.c</c> for a build with the manual, which the OpenSSL builds have.
/// </summary>
[TestClass]
public sealed class CommandLineRefusalEmptyCommandLineTests
{
    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void EmptyCommandLine_OnWindows_IsCurlsTryHelpLineAloneWithExit2() =>
        AssertEmptyCommandLineRefusal("curl: try 'curl --help' or 'curl --manual' for more information");

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void EmptyCommandLine_OffWindows_IsCurlsTryHelpLineAloneWithExit2() =>
        AssertEmptyCommandLineRefusal("curl: try 'curl --help' or 'curl --manual' for more information");

    [TestMethod]
    public void UsagePage_Always_StillNamesAiHelpInItsFooter()
    {
        string usage = string.Join("\n", CurlHelpText.Lines(null, 80));

        StringAssert.Contains(usage, CurlHelpText.AiHelpUsageLine);
        StringAssert.Contains(CurlHelpText.AiHelpUsageLine, "--ai-help");
    }

    private static void AssertEmptyCommandLineRefusal(string expectedLine)
    {
        CommandLineRefusal refusal = CommandLineRefusal.EmptyCommandLine();

        CollectionAssert.AreEqual(new[] { expectedLine }, refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        Assert.AreEqual(2, (int)refusal.ExitCode);
        Assert.IsFalse(refusal.StandardErrorLines[0].Contains("--ai-help", StringComparison.Ordinal));
    }
}
