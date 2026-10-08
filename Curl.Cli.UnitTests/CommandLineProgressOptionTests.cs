using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <c>--no-progress-meter</c> / <c>--progress-meter</c> and <c>-#</c> / <c>--progress-bar</c> /
/// <c>--no-progress-bar</c> as curl 8.21.0 parses them, measured against the local curl 8.21.0 on
/// 2026-09-26 with a 3 MB <c>file://</c> download to <c>-o</c> at <c>--limit-rate 1M</c>, reading
/// standard error: the last of <c>--no-progress-meter</c> / <c>--progress-meter</c> wins, the last
/// of <c>-#</c> / <c>--no-progress-bar</c> wins, a value attached to either long name is ignored,
/// and <c>--no-progress-meter</c> hides the bar too, in either order.
/// </summary>
[TestClass]
public sealed class CommandLineProgressOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NoProgressOption_LeavesMeterOnAndNotBar()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("progress meter off", false, result.Options?.ProgressMeterOff);
        Diagnostics.Assert("progress bar", false, result.Options?.ProgressBar);
        Assert.IsNotNull(result.Options);
        Assert.IsFalse(result.Options.ProgressMeterOff);
        Assert.IsFalse(result.Options.ProgressBar);
    }

    [TestMethod]
    [DataRow("--no-progress-meter")]
    [DataRow("--progress-meter --no-progress-meter")]
    [DataRow("--no-progress-meter=x")]
    public void Parse_NoProgressMeterLast_TurnsMeterOff(string progressArguments)
    {
        CommandLineParseResult result = Parse([.. progressArguments.Split(' '), Url]);

        Diagnostics.Assert("progress meter off", true, result.Options?.ProgressMeterOff);
        Assert.IsNotNull(result.Options);
        Assert.IsTrue(result.Options.ProgressMeterOff);
    }

    [TestMethod]
    [DataRow("--progress-meter")]
    [DataRow("--no-progress-meter --progress-meter")]
    [DataRow("--no-progress-meter --progress-meter=x")]
    public void Parse_ProgressMeterLast_LeavesMeterOn(string progressArguments)
    {
        CommandLineParseResult result = Parse([.. progressArguments.Split(' '), Url]);

        Diagnostics.Assert("progress meter off", false, result.Options?.ProgressMeterOff);
        Assert.IsNotNull(result.Options);
        Assert.IsFalse(result.Options.ProgressMeterOff);
    }

    [TestMethod]
    [DataRow("-#")]
    [DataRow("--progress-bar")]
    [DataRow("--progress-bar=x")]
    [DataRow("--no-progress-bar -#")]
    [DataRow("-#s")]
    public void Parse_ProgressBarLast_ChoosesBar(string progressArguments)
    {
        CommandLineParseResult result = Parse([.. progressArguments.Split(' '), Url]);

        Diagnostics.Assert("progress bar", true, result.Options?.ProgressBar);
        Assert.IsNotNull(result.Options);
        Assert.IsTrue(result.Options.ProgressBar);
    }

    [TestMethod]
    [DataRow("--no-progress-bar")]
    [DataRow("-# --no-progress-bar")]
    [DataRow("--progress-bar --no-progress-bar=x")]
    public void Parse_NoProgressBarLast_DoesNotChooseBar(string progressArguments)
    {
        CommandLineParseResult result = Parse([.. progressArguments.Split(' '), Url]);

        Diagnostics.Assert("progress bar", false, result.Options?.ProgressBar);
        Assert.IsNotNull(result.Options);
        Assert.IsFalse(result.Options.ProgressBar);
    }

    [TestMethod]
    [DataRow("-# --no-progress-meter")]
    [DataRow("--no-progress-meter -#")]
    public void Parse_ProgressBarAndNoProgressMeter_RecordsBothInEitherOrder(string progressArguments)
    {
        CommandLineParseResult result = Parse([.. progressArguments.Split(' '), Url]);

        Diagnostics.Assert("progress meter off", true, result.Options?.ProgressMeterOff);
        Diagnostics.Assert("progress bar", true, result.Options?.ProgressBar);
        Assert.IsNotNull(result.Options);
        Assert.IsTrue(result.Options.ProgressMeterOff);
        Assert.IsTrue(result.Options.ProgressBar);
    }

    [TestMethod]
    [DataRow("--no-#")]
    [DataRow("--Progress-bar")]
    public void Parse_MisspelledProgressOption_IsRefusedAsUnknown(string argument)
    {
        CommandLineParseResult result = Parse([argument, Url]);

        Diagnostics.Assert("accepted", false, result.IsAccepted);
        Diagnostics.Assert("first stderr line", $"curl: option {argument}: is unknown", CommandLineParseDiagnostics.Peek(result.Refusal)?.StandardErrorLines[0]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {argument}: is unknown", CommandLineRefusal.TryHelpLine },
            result.Refusal.StandardErrorLines.ToArray());
    }

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        return result;
    }
}
