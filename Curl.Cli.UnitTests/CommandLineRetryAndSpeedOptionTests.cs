using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--retry</c>, <c>--retry-delay</c>, <c>--retry-max-time</c>,
/// <c>--retry-all-errors</c>, <c>--retry-connrefused</c>, <c>--limit-rate</c>,
/// <c>-Y</c>/<c>--speed-limit</c> and <c>-y</c>/<c>--speed-time</c>. Every value and refusal was
/// measured against the local curl 8.21.0 (mingw, Schannel) on 2026-09-26 with
/// <c>curl &lt;option&gt; &lt;value&gt; --libcurl lc.c file:///Z:/nonexistent_bl196</c>: an accepted
/// value exits 37 and, for the libcurl-side options, shows in <c>lc.c</c> as
/// <c>CURLOPT_MAX_RECV_SPEED_LARGE</c>, <c>CURLOPT_LOW_SPEED_LIMIT</c> or
/// <c>CURLOPT_LOW_SPEED_TIME</c>; a refused one exits 2 with the lines pinned here.
/// </summary>
[TestClass]
public sealed class CommandLineRetryAndSpeedOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    private const string ProperNumber = "expected a proper numerical parameter";

    private const string PositiveNumber = "expected a positive numerical parameter";

    private const string TooLarge = "too large number";

    private const string BadlyUsed = "is badly used here";

    private const string CannotBeReversed = "the given option cannot be reversed with a --no- prefix";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    private CommandLineParseResult Parse(IReadOnlyList<string> arguments)
    {
        Diagnostics.ArrangeArguments(arguments);
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Diagnostics.ActParse(result);
        if (result.IsAccepted)
        {
            CommandLineOptions options = result.Options;
            Diagnostics.Act("retry count", options.RetryCount);
            Diagnostics.Act("retry delay", Show(options.RetryDelay?.TotalMilliseconds));
            Diagnostics.Act("retry max time", Show(options.RetryMaxTime?.TotalMilliseconds));
            Diagnostics.Act("retry all errors", options.RetryAllErrors);
            Diagnostics.Act("retry connection refused", options.RetryConnectionRefused);
            Diagnostics.Act("limit rate", Show(options.LimitRate));
            Diagnostics.Act("speed limit", Show(options.SpeedLimit));
            Diagnostics.Act("speed time seconds", Show(options.SpeedTimeSeconds));
        }

        return result;
    }

    private static string Show(IFormattable? value) =>
        value?.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? "null";

    [TestMethod]
    public void Parse_NoneOfTheOptions_LeavesTheirDefaults()
    {
        CommandLineOptions options = Accepted(Url);

        Assert.AreEqual(0L, options.RetryCount);
        Assert.IsNull(options.RetryDelay);
        Assert.IsNull(options.RetryMaxTime);
        Assert.IsFalse(options.RetryAllErrors);
        Assert.IsFalse(options.RetryConnectionRefused);
        Assert.IsNull(options.LimitRate);
        Assert.IsNull(options.SpeedLimit);
        Assert.IsNull(options.SpeedTimeSeconds);
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("-0", 0L)]
    [DataRow("3", 3L)]
    [DataRow("2147483647", 2147483647L)]
    public void Parse_Retry_RecordsTheCount(string value, long expected)
    {
        Assert.AreEqual(expected, Accepted("--retry", value, Url).RetryCount);
    }

    [TestMethod]
    public void Parse_RetryGivenTwiceWithEquals_KeepsTheLast()
    {
        Assert.AreEqual(3L, Accepted("--retry", "5", "--retry=3", Url).RetryCount);
    }

    [TestMethod]
    [DataRow("--retry-delay")]
    [DataRow("--retry-max-time")]
    public void Parse_RetrySecondsWithFraction_RecordsMilliseconds(string spelling)
    {
        CommandLineOptions options = Accepted(spelling, "1.5", Url);

        TimeSpan? recorded = spelling == "--retry-delay" ? options.RetryDelay : options.RetryMaxTime;
        Assert.AreEqual(TimeSpan.FromMilliseconds(1500), recorded);
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("1,5", 1000L)]
    [DataRow("1e3", 1000L)]
    [DataRow("0x10", 0L)]
    [DataRow("2147482", 2147482000L)]
    [DataRow("1.99999999999", 1999L)]
    public void Parse_RetryDelayAndMaxTime_ReadSecondsAsMaxTimeDoes(string value, long expectedMilliseconds)
    {
        CommandLineOptions options = Accepted("--retry-delay", value, "--retry-max-time", value, Url);

        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), options.RetryDelay);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMilliseconds), options.RetryMaxTime);
    }

    [TestMethod]
    [DataRow("--retry-all-errors")]
    [DataRow("--retry-all-errors=x")]
    public void Parse_RetryAllErrors_TurnsItOn(string spelling)
    {
        Assert.IsTrue(Accepted(spelling, Url).RetryAllErrors);
    }

    [TestMethod]
    public void Parse_RetryConnrefused_TurnsItOn()
    {
        Assert.IsTrue(Accepted("--retry-connrefused", Url).RetryConnectionRefused);
    }

    [TestMethod]
    [DataRow("--no-retry-all-errors")]
    [DataRow("--no-retry-all-errors=x")]
    public void Parse_NoRetryAllErrorsAfterRetryAllErrors_TurnsItOff(string negation)
    {
        Assert.IsFalse(Accepted("--retry-all-errors", negation, Url).RetryAllErrors);
    }

    [TestMethod]
    [DataRow("--no-retry-connrefused")]
    [DataRow("--no-retry-connrefused=x")]
    public void Parse_NoRetryConnrefusedAfterRetryConnrefused_TurnsItOff(string negation)
    {
        Assert.IsFalse(Accepted("--retry-connrefused", negation, Url).RetryConnectionRefused);
    }

    [TestMethod]
    public void Parse_RetryFlagAfterItsNegation_TurnsItOn()
    {
        CommandLineOptions options = Accepted("--no-retry-all-errors", "--retry-all-errors", "--no-retry-connrefused", "--retry-connrefused", Url);

        Assert.IsTrue(options.RetryAllErrors);
        Assert.IsTrue(options.RetryConnectionRefused);
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("100", 100L)]
    [DataRow("1b", 1L)]
    [DataRow("1B", 1L)]
    [DataRow("1k", 1024L)]
    [DataRow("1K", 1024L)]
    [DataRow("1m", 1048576L)]
    [DataRow("1M", 1048576L)]
    [DataRow("1g", 1073741824L)]
    [DataRow("1G", 1073741824L)]
    [DataRow("1t", 1099511627776L)]
    [DataRow("1T", 1099511627776L)]
    [DataRow("1p", 1125899906842624L)]
    [DataRow("1.5k", 1536L)]
    [DataRow("0.3333k", 340L)]
    [DataRow("8191.99999p", 9223372025595734116L)]
    [DataRow("8589934591G", 9223372035781033984L)]
    [DataRow("8796093022207M", 9223372036853727232L)]
    [DataRow("9223372036854775807", 9223372036854775807L)]
    public void Parse_LimitRate_RecordsBytesPerSecondAsMeasured(string value, long expected)
    {
        Assert.AreEqual(expected, Accepted("--limit-rate", value, Url).LimitRate);
    }

    [TestMethod]
    public void Parse_LimitRateGivenTwice_KeepsTheLast()
    {
        Assert.AreEqual(2048L, Accepted("--limit-rate", "1k", "--limit-rate=2k", Url).LimitRate);
    }

    [TestMethod]
    [DataRow("-Y", "0", 0L)]
    [DataRow("-Y", "-0", 0L)]
    [DataRow("-Y", "1", 1L)]
    [DataRow("-Y", "2147483647", 2147483647L)]
    [DataRow("--speed-limit", "1", 1L)]
    public void Parse_SpeedLimit_RecordsBytesPerSecond(string spelling, string value, long expected)
    {
        CommandLineOptions options = Accepted(spelling, value, Url);

        Assert.AreEqual(expected, options.SpeedLimit);
        Assert.IsNull(options.SpeedTimeSeconds);
    }

    [TestMethod]
    [DataRow("-y", "0", 0L)]
    [DataRow("-y", "-0", 0L)]
    [DataRow("-y", "1", 1L)]
    [DataRow("-y", "2147483647", 2147483647L)]
    [DataRow("--speed-time", "1", 1L)]
    public void Parse_SpeedTime_RecordsSeconds(string spelling, string value, long expected)
    {
        CommandLineOptions options = Accepted(spelling, value, Url);

        Assert.AreEqual(expected, options.SpeedTimeSeconds);
        Assert.IsNull(options.SpeedLimit);
    }

    [TestMethod]
    [DataRow(new[] { "-Y", "0", "-y", "2" }, 1L, 2L)]
    [DataRow(new[] { "-y", "2", "-Y", "0" }, 0L, 2L)]
    [DataRow(new[] { "-Y", "100", "-y", "0" }, 100L, 0L)]
    [DataRow(new[] { "-y", "0", "-Y", "5" }, 5L, 30L)]
    [DataRow(new[] { "-Y", "0", "-y", "0" }, 1L, 0L)]
    [DataRow(new[] { "-y", "3", "-Y", "7", "-y", "4" }, 7L, 4L)]
    public void Parse_SpeedOptionsAfterAZeroOther_ApplyCurlsDefaultsInCommandLineOrder(string[] speedArguments, long expectedLimit, long expectedSeconds)
    {
        CommandLineOptions options = Accepted([.. speedArguments, Url]);

        Assert.AreEqual(expectedLimit, options.SpeedLimit);
        Assert.AreEqual(expectedSeconds, options.SpeedTimeSeconds);
    }

    [TestMethod]
    public void Parse_SpeedLettersAttachedAndBundled_Accepted()
    {
        CommandLineOptions attached = Accepted("-Y1", "-y2", Url);
        CommandLineOptions bundled = Accepted("-sY7", Url);

        Assert.AreEqual(1L, attached.SpeedLimit);
        Assert.AreEqual(2L, attached.SpeedTimeSeconds);
        Assert.AreEqual(7L, bundled.SpeedLimit);
    }

    [TestMethod]
    [DataRow("--retry", "-1", PositiveNumber)]
    [DataRow("--retry", "abc", ProperNumber)]
    [DataRow("--retry", "", ProperNumber)]
    [DataRow("--retry", " 1", ProperNumber)]
    [DataRow("--retry", "+1", ProperNumber)]
    [DataRow("--retry", "1.5", ProperNumber)]
    [DataRow("--retry", "0x10", ProperNumber)]
    [DataRow("--retry", "1k", ProperNumber)]
    [DataRow("--retry", "99999999999999999999", ProperNumber)]
    [DataRow("--retry-delay", "-0", ProperNumber)]
    [DataRow("--retry-delay", "-1", ProperNumber)]
    [DataRow("--retry-delay", "abc", ProperNumber)]
    [DataRow("--retry-delay", "", ProperNumber)]
    [DataRow("--retry-delay", ".5", ProperNumber)]
    [DataRow("--retry-delay", "1.99999999999999999999", TooLarge)]
    [DataRow("--retry-max-time", "-0", ProperNumber)]
    [DataRow("--retry-max-time", "-1", ProperNumber)]
    [DataRow("--retry-max-time", "abc", ProperNumber)]
    [DataRow("--retry-max-time", "", ProperNumber)]
    [DataRow("--retry-max-time", ".5", ProperNumber)]
    [DataRow("--retry-max-time", "1.99999999999999999999", TooLarge)]
    [DataRow("--limit-rate", "1x", BadlyUsed)]
    [DataRow("--limit-rate", "1kb", BadlyUsed)]
    [DataRow("--limit-rate", "1k/s", BadlyUsed)]
    [DataRow("--limit-rate", "1.5", BadlyUsed)]
    [DataRow("--limit-rate", "1.5b", BadlyUsed)]
    [DataRow("--limit-rate", "0x10", BadlyUsed)]
    [DataRow("--limit-rate", ".5k", ProperNumber)]
    [DataRow("--limit-rate", "1.k", ProperNumber)]
    [DataRow("--limit-rate", "-1", ProperNumber)]
    [DataRow("--limit-rate", "-0", ProperNumber)]
    [DataRow("--limit-rate", "", ProperNumber)]
    [DataRow("--limit-rate", "abc", ProperNumber)]
    [DataRow("--limit-rate", " 1", ProperNumber)]
    [DataRow("--limit-rate", "+1", ProperNumber)]
    [DataRow("--limit-rate", "8589934592G", TooLarge)]
    [DataRow("--limit-rate", "8796093022208M", TooLarge)]
    [DataRow("--limit-rate", "9223372036854775808", TooLarge)]
    [DataRow("--limit-rate", "99999999999999999999", TooLarge)]
    [DataRow("-Y", "-1", PositiveNumber)]
    [DataRow("-Y", "abc", ProperNumber)]
    [DataRow("-Y", "", ProperNumber)]
    [DataRow("-Y", "1.5", ProperNumber)]
    [DataRow("-Y", "1k", ProperNumber)]
    [DataRow("--speed-limit", "-1", PositiveNumber)]
    [DataRow("--speed-limit", "abc", ProperNumber)]
    [DataRow("-y", "-1", PositiveNumber)]
    [DataRow("-y", "abc", ProperNumber)]
    [DataRow("-y", "", ProperNumber)]
    [DataRow("-y", "1.5", ProperNumber)]
    [DataRow("-y", "1k", ProperNumber)]
    [DataRow("--speed-time", "-1", PositiveNumber)]
    [DataRow("--speed-time", "abc", ProperNumber)]
    public void Parse_UnreadableValue_IsRefusedAsCurlRefusesIt(string spelling, string value, string reason)
    {
        AssertRefused([spelling, value, Url], $"curl: option {spelling}: {reason}");
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("--retry", "2147483648")]
    [DataRow("-Y", "2147483648")]
    [DataRow("--speed-limit", "9223372036854775807")]
    [DataRow("-y", "2147483648")]
    [DataRow("--speed-time", "9223372036854775807")]
    [DataRow("--retry-delay", "2147483")]
    [DataRow("--retry-max-time", "2147483647")]
    public void Parse_OnWindows_PastTheWindowsCeiling_IsRefused(string spelling, string value)
    {
        AssertRefused([spelling, value, Url], $"curl: option {spelling}: {ProperNumber}");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.Windows)]
    public void Parse_OnLinuxOrMacOS_PastTheWindowsCeiling_IsRecorded()
    {
        CommandLineOptions options = Accepted("--retry", "2147483648", "-Y", "2147483648", "-y", "2147483648", "--retry-delay", "2147483", Url);

        Assert.AreEqual(2147483648L, options.RetryCount);
        Assert.AreEqual(2147483648L, options.SpeedLimit);
        Assert.AreEqual(2147483648L, options.SpeedTimeSeconds);
        Assert.AreEqual(TimeSpan.FromSeconds(2147483), options.RetryDelay);
    }

    [TestMethod]
    [DataRow("--no-retry")]
    [DataRow("--no-retry=x")]
    [DataRow("--no-retry-delay")]
    [DataRow("--no-retry-delay=x")]
    [DataRow("--no-retry-max-time")]
    [DataRow("--no-retry-max-time=x")]
    [DataRow("--no-limit-rate")]
    [DataRow("--no-limit-rate=x")]
    [DataRow("--no-speed-limit")]
    [DataRow("--no-speed-limit=x")]
    [DataRow("--no-speed-time")]
    [DataRow("--no-speed-time=x")]
    public void Parse_NoSpellingOfAValueOption_CannotBeReversed(string spelling)
    {
        AssertRefused([spelling, Url], $"curl: option {spelling}: {CannotBeReversed}");
    }

    private CommandLineOptions Accepted(params string[] arguments)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.Assert("accepted", true, result.IsAccepted);
        Diagnostics.Assert("warnings", "[]", CommandLineParseDiagnostics.QuoteEach(result.WarningLines));
        Assert.IsTrue(result.IsAccepted);
        Assert.IsEmpty(result.WarningLines);
        return result.Options;
    }

    private void AssertRefused(string[] arguments, string refusalLine)
    {
        CommandLineParseResult result = Parse(arguments);

        Diagnostics.AssertRefusal(result, CurlExitCode.FailedInit, [refusalLine, TryHelp]);
        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { refusalLine, TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }
}
