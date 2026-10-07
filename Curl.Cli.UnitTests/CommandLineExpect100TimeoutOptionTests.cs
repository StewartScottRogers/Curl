using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--expect100-timeout</c>, which curl 8.21.0 reads as it reads
/// <c>--connect-timeout</c>: measured against the local curl on 2026-09-29, <c>0.2</c> and
/// <c>3</c> waited 200 ms and 3 s, <c>abc</c> and <c>-1</c> exit 2 with "expected a proper
/// numerical parameter", and on Windows <c>2147482</c> is accepted and <c>2147483</c> refused
/// (BL-624 Notes).
/// </summary>
[TestClass]
public sealed class CommandLineExpect100TimeoutOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NotGiven_LeavesItNotGiven()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("expect100 timeout", null, result.Options?.Expect100Timeout);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.Expect100Timeout);
    }

    [TestMethod]
    [DataRow("0.2", 200)]
    [DataRow("3", 3000)]
    [DataRow("0", 0)]
    [DataRow("1,5", 1000)]
    [DataRow("2147482.999", 2147482999)]
    public void Parse_Seconds_RecordsThemToTheMillisecond(string value, long expectedMilliseconds)
    {
        CommandLineParseResult result = Parse(["--expect100-timeout", value, Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("expect100 timeout", TimeSpan.FromMilliseconds(expectedMilliseconds), result.Options?.Expect100Timeout);
        Diagnostics.Assert("connect timeout", null, result.Options?.ConnectTimeout);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), result.Options.Expect100Timeout);
        Assert.IsNull(result.Options.ConnectTimeout);
    }

    [TestMethod]
    public void Parse_GivenTwiceWithEquals_KeepsTheLast()
    {
        CommandLineParseResult result = Parse(["--expect100-timeout", "5", "--expect100-timeout=0.5", Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("expect100 timeout", TimeSpan.FromMilliseconds(500), result.Options?.Expect100Timeout);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), result.Options.Expect100Timeout);
    }

    [TestMethod]
    [DataRow("abc", "expected a proper numerical parameter")]
    [DataRow("-1", "expected a proper numerical parameter")]
    [DataRow("", "expected a proper numerical parameter")]
    [DataRow("1.", "too large number")]
    public void Parse_UnreadableValue_IsRefusedWithExit2(string value, string reason)
    {
        CommandLineParseResult result = Parse(["--expect100-timeout", value, Url]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("exit code", CurlExitCode.FailedInit, result.Refusal?.ExitCode);
        AssertStandardErrorLines(result, $"curl: option --expect100-timeout: {reason}");
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option --expect100-timeout: {reason}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Parse_OnWindows_MoreThanMaximumWholeSeconds_IsRefused()
    {
        CommandLineParseResult result = Parse(["--expect100-timeout", "2147483", Url]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        AssertStandardErrorLines(result, "curl: option --expect100-timeout: expected a proper numerical parameter");
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: option --expect100-timeout: expected a proper numerical parameter", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    private CommandLineParseResult Parse(string[] arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }

    private void AssertStandardErrorLines(CommandLineParseResult result, string optionLine) =>
        Diagnostics.Assert(
            "stderr lines",
            CommandLineParseDiagnostics.QuoteEach([optionLine, TryHelp]),
            CommandLineParseDiagnostics.QuoteEach(result.Refusal?.StandardErrorLines ?? []));
}
