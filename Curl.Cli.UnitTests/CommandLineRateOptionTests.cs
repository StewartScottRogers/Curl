namespace Curl.Cli;

/// <summary>
/// Pins <c>--rate</c> as curl 8.21.0 reads it, measured with the local curl 8.21.0 (Schannel) on
/// 2026-09-29 (BL-650 Notes): the least time between two serial transfer starts, the refusals and their
/// messages, and that it is global and the last one wins.
/// </summary>
[TestClass]
public sealed class CommandLineRateOptionTests
{
    private const string Url = "http://127.0.0.1:1/";

    private const string TryHelpLine = "curl: try 'curl --help' or 'curl --manual' for more information";

    private const string ProperNumber = "curl: option --rate: expected a proper numerical parameter";

    private const string BadlyUsed = "curl: option --rate: is badly used here";

    private const string TooLarge = "curl: option --rate: too large number";

    private const string UnsupportedUnit = "curl: unsupported --rate unit";

    private const string TooLargeUnit = "curl: too large --rate unit";

    private static CommandLineOptions Accept(params string[] arguments)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);
        Assert.IsTrue(result.IsAccepted, result.IsAccepted ? string.Empty : result.Refusal.StandardErrorLines[0]);
        return result.Options;
    }

    private static void AssertRefused(string[] arguments, params string[] expectedLinesBeforeTryHelp)
    {
        CommandLineParseResult result = CommandLineParser.Parse(arguments);

        Assert.IsFalse(result.IsAccepted);
        CollectionAssert.AreEqual(expectedLinesBeforeTryHelp.Append(TryHelpLine).ToArray(), result.Refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void Parse_NoRate_LeavesTheIntervalUnset()
    {
        Assert.IsNull(Accept(Url).MillisecondsBetweenTransferStarts);
    }

    [TestMethod]
    [DataRow("2/s", 500L)]
    [DataRow("1/3s", 3000L)]
    [DataRow("5/15s", 3000L)]
    [DataRow("2/3s", 1500L)]
    [DataRow("3/s", 333L)]
    [DataRow("1000/s", 1L)]
    [DataRow("2/m", 30000L)]
    [DataRow("14/m", 4285L)]
    [DataRow("3/h", 1200000L)]
    [DataRow("1/d", 86400000L)]
    [DataRow("1/25d", 2160000000L)]
    [DataRow("3600000/h", 1L)]
    [DataRow("2", 1800000L)]
    [DataRow("2x", 1800000L)]
    [DataRow("1x/s", 1000L)]
    [DataRow("2 /s", 500L)]
    [DataRow("010/s", 100L)]
    [DataRow("2/sx", 500L)]
    [DataRow("1/sm", 1000L)]
    [DataRow("1/s/s", 1000L)]
    [DataRow("1/1001s", 1001000L)]
    [DataRow("1/00000000000000000000000000000001s", 1000L)]
    [DataRow("0000000000000000000000001/s", 1000L)]
    [DataRow("1/9223372036854775s", 9223372036854775000L)]
    [DataRow("1/106751991167d", 9223372036828800000L)]
    public void Parse_Rate_SetsTheLeastTimeBetweenStarts(string rate, long milliseconds)
    {
        Assert.AreEqual(milliseconds, Accept("--rate", rate, Url).MillisecondsBetweenTransferStarts);
    }

    [TestMethod]
    public void Parse_TwoRates_LastOneWins()
    {
        Assert.AreEqual(500L, Accept("--rate", "1/s", "--rate", "2/s", Url).MillisecondsBetweenTransferStarts);
    }

    [TestMethod]
    public void Parse_RateInTheFirstGroup_ReachesTheNext()
    {
        CommandLineParseResult result = CommandLineParser.Parse(["--rate", "2/s", Url, "--next", Url]);

        Assert.IsTrue(result.IsAccepted);
        Assert.AreEqual(500L, result.Groups[^1].MillisecondsBetweenTransferStarts);
    }

    [TestMethod]
    [DataRow("abc")]
    [DataRow("")]
    [DataRow(" 2/s")]
    [DataRow("+2/s")]
    [DataRow("-1/s")]
    [DataRow("/s")]
    [DataRow("99999999999999999999/s")]
    [DataRow("9223372036854775808/d")]
    public void Parse_RateWithoutLeadingDigits_IsNotAProperNumber(string rate)
    {
        AssertRefused(["--rate", rate, Url], ProperNumber);
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("0/s")]
    [DataRow("0x10/s")]
    public void Parse_ZeroTransfers_IsBadlyUsed(string rate)
    {
        AssertRefused(["--rate", rate, Url], BadlyUsed);
    }

    [TestMethod]
    [DataRow("1/x")]
    [DataRow("2/3")]
    [DataRow("2/")]
    [DataRow("2/ s")]
    [DataRow("2/S")]
    [DataRow("1/-1s")]
    [DataRow("5/0x2s")]
    [DataRow("1/99999999999999999999s")]
    [DataRow("1/2562047788015x")]
    public void Parse_UnknownUnit_IsBadlyUsedWithItsMessage(string rate)
    {
        AssertRefused(["--rate", rate, Url], UnsupportedUnit, BadlyUsed);
    }

    [TestMethod]
    public void Parse_UnknownUnitSilently_DropsItsMessage()
    {
        AssertRefused(["-s", "--rate", "1/x", Url], BadlyUsed);
    }

    [TestMethod]
    public void Parse_UnknownUnitSilentlyShowingErrors_KeepsItsMessage()
    {
        AssertRefused(["-sS", "--rate", "1/x", Url], UnsupportedUnit, BadlyUsed);
    }

    [TestMethod]
    [DataRow("1/9223372036854775807s")]
    [DataRow("1/9223372036854776s")]
    [DataRow("1/106751991168d")]
    public void Parse_PeriodBeyondALong_IsTooLargeWithItsMessage(string rate)
    {
        AssertRefused(["--rate", rate, Url], TooLargeUnit, TooLarge);
    }

    [TestMethod]
    public void Parse_UnknownUnitAndPeriodBeyondALong_PrintsBothMessages()
    {
        AssertRefused(["--rate", "1/2562047788016x", Url], UnsupportedUnit, TooLargeUnit, TooLarge);
    }

    [TestMethod]
    public void Parse_PeriodBeyondALongSilently_DropsItsMessage()
    {
        AssertRefused(["-s", "--rate", "1/9223372036854776s", Url], TooLarge);
    }

    [TestMethod]
    [DataRow("1001/s")]
    [DataRow("1/0s")]
    [DataRow("3601000/h")]
    [DataRow("2147483647/s")]
    [DataRow("3000000000/d")]
    [DataRow("9223372036854775807/d")]
    public void Parse_MoreTransfersThanMillisecondsInThePeriod_IsTooLarge(string rate)
    {
        AssertRefused(["--rate", rate, Url], TooLarge);
    }
}
