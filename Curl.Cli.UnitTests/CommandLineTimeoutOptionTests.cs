using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--connect-timeout</c> and <c>-m</c>/<c>--max-time</c>, and
/// <see cref="CommandLineNumber.ParseSeconds"/> underneath them. Every value and refusal was
/// measured against the local curl 8.21.0 on 2026-09-26: accepted values through the
/// milliseconds <c>--libcurl</c> writes for <c>CURLOPT_CONNECTTIMEOUT_MS</c> and
/// <c>CURLOPT_TIMEOUT_MS</c>, refusals from standard error and the exit code.
/// </summary>
[TestClass]
public sealed class CommandLineTimeoutOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    private const string ProperNumber = "expected a proper numerical parameter";

    private const string TooLarge = "too large number";

    private static readonly long WindowsLongMaximum = CommandLineNumber.LongMaximumFor(isWindows: true);

    private static readonly long UnixLongMaximum = CommandLineNumber.LongMaximumFor(isWindows: false);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Parse_NeitherOption_LeavesBothNotGiven()
    {
        CommandLineParseResult result = Parse([Url]);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.ConnectTimeout);
        Assert.IsNull(result.Options.MaxTime);
    }

    [TestMethod]
    public void Parse_ConnectTimeoutInWholeSeconds_RecordsThem()
    {
        CommandLineParseResult result = Parse(["--connect-timeout", "10", Url]);

        Diagnostics.Assert("connect timeout", TimeSpan.FromSeconds(10), CommandLineParseDiagnostics.Peek(result.Options)?.ConnectTimeout);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromSeconds(10), result.Options.ConnectTimeout);
        Assert.IsNull(result.Options.MaxTime);
    }

    [TestMethod]
    public void Parse_ConnectTimeoutWithFraction_RecordsMilliseconds()
    {
        CommandLineParseResult result = Parse(["--connect-timeout", "3.14", Url]);

        Diagnostics.Assert("connect timeout", TimeSpan.FromMilliseconds(3140), CommandLineParseDiagnostics.Peek(result.Options)?.ConnectTimeout);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(3140), result.Options.ConnectTimeout);
    }

    [TestMethod]
    [DataRow("-m")]
    [DataRow("--max-time")]
    public void Parse_MaxTimeWithFraction_RecordsMilliseconds(string spelling)
    {
        CommandLineParseResult result = Parse([spelling, "2.5", Url]);

        Diagnostics.Assert("max time", TimeSpan.FromMilliseconds(2500), CommandLineParseDiagnostics.Peek(result.Options)?.MaxTime);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2500), result.Options.MaxTime);
        Assert.IsNull(result.Options.ConnectTimeout);
    }

    [TestMethod]
    public void Parse_MaxTimeBundledAndWithEquals_Accepted()
    {
        CommandLineParseResult bundled = Parse(["-sm2", Url]);
        CommandLineParseResult withEquals = Parse(["--max-time=7", Url]);

        Diagnostics.Assert("bundled max time", TimeSpan.FromSeconds(2), CommandLineParseDiagnostics.Peek(bundled.Options)?.MaxTime);
        Diagnostics.Assert("with-equals max time", TimeSpan.FromSeconds(7), CommandLineParseDiagnostics.Peek(withEquals.Options)?.MaxTime);
        Assert.IsTrue(bundled.IsAccepted);
        Assert.IsTrue(withEquals.IsAccepted);
        Assert.AreEqual(TimeSpan.FromSeconds(2), bundled.Options.MaxTime);
        Assert.AreEqual(TimeSpan.FromSeconds(7), withEquals.Options.MaxTime);
    }

    [TestMethod]
    public void Parse_EachGivenTwice_KeepsTheLast()
    {
        CommandLineParseResult result = Parse(
            ["--connect-timeout", "5", "-m", "1", "--connect-timeout", "0.5", "--max-time", "0", Url]);

        Diagnostics.Assert("connect timeout", TimeSpan.FromMilliseconds(500), CommandLineParseDiagnostics.Peek(result.Options)?.ConnectTimeout);
        Diagnostics.Assert("max time", TimeSpan.Zero, CommandLineParseDiagnostics.Peek(result.Options)?.MaxTime);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), result.Options.ConnectTimeout);
        Assert.AreEqual(TimeSpan.Zero, result.Options.MaxTime);
    }

    [TestMethod]
    [DataRow("--connect-timeout", "abc", ProperNumber)]
    [DataRow("--connect-timeout", "-1", ProperNumber)]
    [DataRow("--connect-timeout", "99999999999999999999", ProperNumber)]
    [DataRow("--connect-timeout", "", ProperNumber)]
    [DataRow("--connect-timeout", "1.", TooLarge)]
    [DataRow("-m", "abc", ProperNumber)]
    [DataRow("-m", "-1", ProperNumber)]
    [DataRow("-m", "99999999999999999999", ProperNumber)]
    [DataRow("-m", "", ProperNumber)]
    [DataRow("-m", "1.", TooLarge)]
    [DataRow("--max-time", "abc", ProperNumber)]
    public void Parse_UnreadableValue_IsRefusedAsCurlRefusesIt(string spelling, string value, string reason)
    {
        CommandLineParseResult result = Parse([spelling, value, Url]);

        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [$"curl: option {spelling}: {reason}", TryHelp]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelling}: {reason}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("--connect-timeout")]
    [DataRow("-m")]
    public void Parse_OnWindows_MoreThanMaximumWholeSeconds_IsRefused(string spelling)
    {
        CommandLineParseResult result = Parse([spelling, "2147483", Url]);

        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [$"curl: option {spelling}: {ProperNumber}", TryHelp]);
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelling}: {ProperNumber}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Parse_OnLinuxOrMacOS_SecondsPastTheWindowsMaximum_AreRecorded()
    {
        CommandLineParseResult result = Parse(["--connect-timeout", "2147483", "-m", "9223372036854774", Url]);

        Diagnostics.Assert("connect timeout", TimeSpan.FromSeconds(2147483), CommandLineParseDiagnostics.Peek(result.Options)?.ConnectTimeout);
        Diagnostics.Assert("max time", TimeSpan.FromMilliseconds(long.MaxValue / TimeSpan.TicksPerMillisecond), CommandLineParseDiagnostics.Peek(result.Options)?.MaxTime);
        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromSeconds(2147483), result.Options.ConnectTimeout);
        Assert.AreEqual(TimeSpan.FromMilliseconds(long.MaxValue / TimeSpan.TicksPerMillisecond), result.Options.MaxTime);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Parse_OnLinuxOrMacOS_MoreThanMaximumWholeSeconds_IsRefused()
    {
        CommandLineParseResult result = Parse(["-m", "9223372036854775", Url]);

        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [$"curl: option -m: {ProperNumber}", TryHelp]);
        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { $"curl: option -m: {ProperNumber}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void MaximumWholeSecondsFor_WindowsCeiling_Is2147482()
    {
        Diagnostics.Arrange("long maximum", WindowsLongMaximum);

        long maximum = CommandLineNumber.MaximumWholeSecondsFor(WindowsLongMaximum);

        Diagnostics.Act("maximum whole seconds", maximum);
        Diagnostics.Assert("maximum whole seconds", 2147482L, maximum);
        Assert.AreEqual(2147482L, CommandLineNumber.MaximumWholeSecondsFor(WindowsLongMaximum));
    }

    [TestMethod]
    public void MaximumWholeSecondsFor_LinuxAndMacOSCeiling_Is9223372036854774()
    {
        Diagnostics.Arrange("long maximum", UnixLongMaximum);

        long maximum = CommandLineNumber.MaximumWholeSecondsFor(UnixLongMaximum);

        Diagnostics.Act("maximum whole seconds", maximum);
        Diagnostics.Assert("maximum whole seconds", 9223372036854774L, maximum);
        Assert.AreEqual(9223372036854774L, CommandLineNumber.MaximumWholeSecondsFor(UnixLongMaximum));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void MaximumWholeSeconds_OnWindows_IsTheWindowsValue()
    {
        Diagnostics.Arrange("platform", "Windows");

        long maximum = CommandLineNumber.MaximumWholeSeconds;

        Diagnostics.Act("maximum whole seconds", maximum);
        Diagnostics.Assert("maximum whole seconds", 2147482L, maximum);
        Assert.AreEqual(2147482L, CommandLineNumber.MaximumWholeSeconds);
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void MaximumWholeSeconds_OnLinuxOrMacOS_IsThe64BitValue()
    {
        Diagnostics.Arrange("platform", "Linux or macOS");

        long maximum = CommandLineNumber.MaximumWholeSeconds;

        Diagnostics.Act("maximum whole seconds", maximum);
        Diagnostics.Assert("maximum whole seconds", 9223372036854774L, maximum);
        Assert.AreEqual(9223372036854774L, CommandLineNumber.MaximumWholeSeconds);
    }

    [TestMethod]
    [DataRow("2147483", 2147483000L)]
    [DataRow("922337203685.476", 922337203685476L)]
    public void ParseSeconds_UnderUnixCeiling_KeepsWholeMilliseconds(string value, long expectedMilliseconds)
    {
        CommandLineRefusal? refusal = ParseSeconds("-m", value, UnixLongMaximum, out TimeSpan duration);

        Diagnostics.Assert("refusal", "none", DescribeRefusal(refusal));
        Diagnostics.Assert("duration", TimeSpan.FromMilliseconds(expectedMilliseconds), duration);
        Assert.IsNull(refusal);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), duration);
    }

    [TestMethod]
    [DataRow("922337203686")]
    [DataRow("9223372036854774.999")]
    public void ParseSeconds_UnderUnixCeilingLongerThanATimeSpan_IsTheLongestTimeSpan(string value)
    {
        CommandLineRefusal? refusal = ParseSeconds("-m", value, UnixLongMaximum, out TimeSpan duration);

        Diagnostics.Assert("refusal", "none", DescribeRefusal(refusal));
        Diagnostics.Assert("duration", TimeSpan.FromMilliseconds(long.MaxValue / TimeSpan.TicksPerMillisecond), duration);
        Assert.IsNull(refusal);
        Assert.AreEqual(TimeSpan.FromMilliseconds(long.MaxValue / TimeSpan.TicksPerMillisecond), duration);
    }

    [TestMethod]
    [DataRow("9223372036854775")]
    [DataRow("9223372036854775807")]
    public void ParseSeconds_UnderUnixCeilingMoreThanMaximumWholeSeconds_IsRefused(string value)
    {
        CommandLineRefusal? refusal = ParseSeconds("-m", value, UnixLongMaximum, out TimeSpan duration);

        Diagnostics.Assert("refusal", CommandLineParseDiagnostics.QuoteEach([$"curl: option -m: {ProperNumber}", TryHelp]), DescribeRefusal(refusal));
        Diagnostics.Assert("duration", TimeSpan.Zero, duration);
        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(new[] { $"curl: option -m: {ProperNumber}", TryHelp }, refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(TimeSpan.Zero, duration);
    }

    [TestMethod]
    [DataRow("--no-connect-timeout")]
    [DataRow("--no-max-time")]
    public void Parse_NegatedSpelling_CannotBeReversed(string spelling)
    {
        CommandLineParseResult result = Parse([spelling, Url]);

        Diagnostics.AssertRefusal(
            result,
            CurlExitCode.FailedInit,
            [$"curl: option {spelling}: the given option cannot be reversed with a --no- prefix", TryHelp]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            CommandLineRefusal.CannotBeReversed(spelling).StandardErrorLines.ToArray(),
            result.Refusal.StandardErrorLines.ToArray());
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelling}: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("10", 10000L)]
    [DataRow("3.14", 3140L)]
    [DataRow("2.5", 2500L)]
    [DataRow("1,5", 1000L)]
    [DataRow("1e999", 1000L)]
    [DataRow("0x10", 0L)]
    [DataRow("1 ", 1000L)]
    [DataRow("0.0001", 0L)]
    [DataRow("0.99999999", 999L)]
    [DataRow("1.123456789012", 1123L)]
    [DataRow("12.3456789012345678", 12345L)]
    [DataRow("2147482.999", 2147482999L)]
    public void ParseSeconds_ReadableValue_KeepsWholeMilliseconds(string value, long expectedMilliseconds)
    {
        CommandLineRefusal? refusal = ParseSeconds("-m", value, WindowsLongMaximum, out TimeSpan duration);

        Diagnostics.Assert("refusal", "none", DescribeRefusal(refusal));
        Diagnostics.Assert("duration", TimeSpan.FromMilliseconds(expectedMilliseconds), duration);
        Assert.IsNull(refusal);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), duration);
    }

    [TestMethod]
    [DataRow("+1", ProperNumber)]
    [DataRow(" 1", ProperNumber)]
    [DataRow(".5", ProperNumber)]
    [DataRow("nan", ProperNumber)]
    [DataRow("inf", ProperNumber)]
    [DataRow("9223372036854775807", ProperNumber)]
    [DataRow("1.abc", TooLarge)]
    [DataRow("1.-5", TooLarge)]
    [DataRow("1.9999999999999999999", TooLarge)]
    public void ParseSeconds_UnreadableValue_IsRefusedWithZero(string value, string reason)
    {
        CommandLineRefusal? refusal = ParseSeconds("--connect-timeout", value, WindowsLongMaximum, out TimeSpan duration);

        Diagnostics.Assert("refusal", CommandLineParseDiagnostics.QuoteEach([$"curl: option --connect-timeout: {reason}", TryHelp]), DescribeRefusal(refusal));
        Diagnostics.Assert("duration", TimeSpan.Zero, duration);
        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { $"curl: option --connect-timeout: {reason}", TryHelp },
            refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(TimeSpan.Zero, duration);
    }

    [TestMethod]
    public void ParseSeconds_NullArgument_Throws()
    {
        Diagnostics.Arrange("calls", "ParseSeconds(null, \"1\", ...) and ParseSeconds(\"-m\", null, ...)");

        string? spelledOptionParamName = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseSeconds(null!, "1", WindowsLongMaximum, out _)).ParamName;
        string? valueParamName = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseSeconds("-m", null!, WindowsLongMaximum, out _)).ParamName;

        Diagnostics.Act("null spelled option throws for", spelledOptionParamName);
        Diagnostics.Act("null value throws for", valueParamName);
        Diagnostics.Assert("null spelled option throws for", "spelledOption", spelledOptionParamName);
        Diagnostics.Assert("null value throws for", "value", valueParamName);
        Assert.AreEqual(
            "spelledOption",
            Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseSeconds(null!, "1", WindowsLongMaximum, out _)).ParamName);
        Assert.AreEqual(
            "value",
            Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseSeconds("-m", null!, WindowsLongMaximum, out _)).ParamName);
    }

    private static string DescribeRefusal(CommandLineRefusal? refusal) =>
        refusal is null ? "none" : CommandLineParseDiagnostics.QuoteEach(refusal.StandardErrorLines);

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            Diagnostics.Act("connect timeout", result.Options.ConnectTimeout);
            Diagnostics.Act("max time", result.Options.MaxTime);
        }

        return result;
    }

    private CommandLineRefusal? ParseSeconds(string spelledOption, string value, long longMaximum, out TimeSpan duration)
    {
        Diagnostics.Arrange("spelled option", spelledOption);
        Diagnostics.Arrange("value", "\"" + value + "\"");
        Diagnostics.Arrange("long maximum", longMaximum);
        CommandLineRefusal? refusal = CommandLineNumber.ParseSeconds(spelledOption, value, longMaximum, out duration);
        Diagnostics.Act("refusal", DescribeRefusal(refusal));
        Diagnostics.Act("duration", duration);
        return refusal;
    }
}
