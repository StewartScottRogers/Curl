using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how the parser records <c>--happy-eyeballs-timeout-ms</c>. Measured against the local curl 8.21.0
/// (Schannel, Windows) on 2026-09-29; every case, with the bytes curl printed, is in BL-644's Notes:
/// <c>0</c>, <c>007</c> and <c>2147483647</c> are accepted, <c>-1</c> is "expected a positive numerical
/// parameter", and <c>abc</c>, <c>1.5</c>, <c>+5</c>, <c>0x10</c>, <c>1e3</c>, <c> 7</c>, the empty value and
/// <c>2147483648</c> are "expected a proper numerical parameter".
/// </summary>
[TestClass]
public sealed class CommandLineHappyEyeballsTimeoutTests
{
    private const string Option = "--happy-eyeballs-timeout-ms";

    private const string Url = "http://localhost:18644/";

    private const string TryHelp = "curl: try 'curl --help' or 'curl --manual' for more information";

    [TestMethod]
    public void Parse_NotGiven_LeavesItNullForCurlsDefault()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.IsNull(result.Options.HappyEyeballsTimeout);
    }

    [TestMethod]
    [DataRow("50", 50L)]
    [DataRow("5000", 5000L)]
    [DataRow("0", 0L)]
    [DataRow("-0", 0L)]
    [DataRow("007", 7L)]
    [DataRow("2147483647", 2147483647L)]
    public void Parse_WholeMilliseconds_RecordsTheTimeout(string value, long milliseconds)
    {
        CommandLineParseResult result = CommandLineParser.Parse([Option, value, Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(milliseconds), result.Options.HappyEyeballsTimeout);
    }

    [TestMethod]
    public void Parse_GivenTwice_LastOneWins()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Option, "50", $"{Option}=75", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(75), result.Options.HappyEyeballsTimeout);
    }

    [TestMethod]
    public void Parse_BeforeNext_DoesNotReachTheNextGroup()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Option, "50", Url, "--next", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(TimeSpan.FromMilliseconds(50), result.Groups[0].HappyEyeballsTimeout);
        Assert.IsNull(result.Groups[1].HappyEyeballsTimeout);
    }

    [TestMethod]
    [DataRow("-1", "expected a positive numerical parameter")]
    [DataRow("abc", "expected a proper numerical parameter")]
    [DataRow("1.5", "expected a proper numerical parameter")]
    [DataRow("+5", "expected a proper numerical parameter")]
    [DataRow("0x10", "expected a proper numerical parameter")]
    [DataRow("1e3", "expected a proper numerical parameter")]
    [DataRow(" 7", "expected a proper numerical parameter")]
    [DataRow("", "expected a proper numerical parameter")]
    public void Parse_BadValue_RefusedAsCurlRefusesIt(string value, string reason)
    {
        CommandLineParseResult result = CommandLineParser.Parse([Option, value, Url]);

        Assert.IsFalse(result.IsAccepted);
        Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode);
        CollectionAssert.AreEqual(new[] { $"curl: option {Option}: {reason}", TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Parse_PastAWindowsLong_RefusedAsCurlRefusesIt()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Option, "2147483648", Url]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { $"curl: option {Option}: expected a proper numerical parameter", TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_WithoutAValue_RequiresAParameter()
    {
        CommandLineParseResult result = CommandLineParser.Parse([Url, Option]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(new[] { $"curl: option {Option}: requires parameter", TryHelp }, result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoPrefix_CannotBeReversed()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--no-happy-eyeballs-timeout-ms", "5", Url]);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(
            new[] { "curl: option --no-happy-eyeballs-timeout-ms: the given option cannot be reversed with a --no- prefix", TryHelp },
            result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void ParseMilliseconds_PastTimeSpanOnA64BitLong_CapsTheDuration()
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseMilliseconds(Option, "9223372036854775807", long.MaxValue, out TimeSpan duration);

        Assert.IsNull(refusal);
        Assert.AreEqual(TimeSpan.FromMilliseconds(long.MaxValue / TimeSpan.TicksPerMillisecond), duration);
    }

    [TestMethod]
    public void ParseMilliseconds_Refused_GivesZero()
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseMilliseconds(Option, "-5", long.MaxValue, out TimeSpan duration);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(TimeSpan.Zero, duration);
    }
}
