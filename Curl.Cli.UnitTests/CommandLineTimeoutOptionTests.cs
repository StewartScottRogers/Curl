using Curl.Protocol.Abstractions;

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

    [TestMethod]
    public void Parse_NeitherOption_LeavesBothNotGiven()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.ConnectTimeout);
        Assert.IsNull(result.Options.MaxTime);
    }

    [TestMethod]
    public void Parse_ConnectTimeoutInWholeSeconds_RecordsThem()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--connect-timeout", "10", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromSeconds(10), result.Options.ConnectTimeout);
        Assert.IsNull(result.Options.MaxTime);
    }

    [TestMethod]
    public void Parse_ConnectTimeoutWithFraction_RecordsMilliseconds()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--connect-timeout", "3.14", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(3140), result.Options.ConnectTimeout);
    }

    [TestMethod]
    [DataRow("-m")]
    [DataRow("--max-time")]
    public void Parse_MaxTimeWithFraction_RecordsMilliseconds(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, "2.5", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(2500), result.Options.MaxTime);
        Assert.IsNull(result.Options.ConnectTimeout);
    }

    [TestMethod]
    public void Parse_MaxTimeBundledAndWithEquals_Accepted()
    {
        CommandLineParseResult bundled = CommandLineParser.Parse(["-sm2", Url]);
        CommandLineParseResult withEquals = CommandLineParser.Parse(["--max-time=7", Url]);

        Assert.IsTrue(bundled.IsAccepted);
        Assert.IsTrue(withEquals.IsAccepted);
        Assert.AreEqual(TimeSpan.FromSeconds(2), bundled.Options.MaxTime);
        Assert.AreEqual(TimeSpan.FromSeconds(7), withEquals.Options.MaxTime);
    }

    [TestMethod]
    public void Parse_EachGivenTwice_KeepsTheLast()
    {
        CommandLineParseResult result = CommandLineParser.Parse(
            ["--connect-timeout", "5", "-m", "1", "--connect-timeout", "0.5", "--max-time", "0", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(500), result.Options.ConnectTimeout);
        Assert.AreEqual(TimeSpan.Zero, result.Options.MaxTime);
    }

    [TestMethod]
    [DataRow("--connect-timeout", "abc", ProperNumber)]
    [DataRow("--connect-timeout", "-1", ProperNumber)]
    [DataRow("--connect-timeout", "99999999999999999999", ProperNumber)]
    [DataRow("--connect-timeout", "2147483", ProperNumber)]
    [DataRow("--connect-timeout", "", ProperNumber)]
    [DataRow("--connect-timeout", "1.", TooLarge)]
    [DataRow("-m", "abc", ProperNumber)]
    [DataRow("-m", "-1", ProperNumber)]
    [DataRow("-m", "99999999999999999999", ProperNumber)]
    [DataRow("-m", "2147483", ProperNumber)]
    [DataRow("-m", "", ProperNumber)]
    [DataRow("-m", "1.", TooLarge)]
    [DataRow("--max-time", "abc", ProperNumber)]
    public void Parse_UnreadableValue_IsRefusedAsCurlRefusesIt(string spelling, string value, string reason)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, value, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { $"curl: option {spelling}: {reason}", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [DataRow("--no-connect-timeout")]
    [DataRow("--no-max-time")]
    public void Parse_NegatedSpelling_CannotBeReversed(string spelling)
    {
        CommandLineParseResult result = CommandLineParser.Parse([spelling, Url]);

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
        Assert.IsNull(CommandLineNumber.ParseSeconds("-m", value, out TimeSpan duration));
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
        CommandLineRefusal? refusal = CommandLineNumber.ParseSeconds("--connect-timeout", value, out TimeSpan duration);

        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { $"curl: option --connect-timeout: {reason}", TryHelp },
            refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(TimeSpan.Zero, duration);
    }

    [TestMethod]
    public void ParseSeconds_NullArgument_Throws()
    {
        Assert.AreEqual(
            "spelledOption",
            Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseSeconds(null!, "1", out _)).ParamName);
        Assert.AreEqual(
            "value",
            Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseSeconds("-m", null!, out _)).ParamName);
    }
}
