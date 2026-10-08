using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins <see cref="CommandLineNumber.ParseSize"/> (<c>--max-filesize</c>) and
/// <see cref="CommandLineNumber.ParseOffset"/> (<c>-C</c>/<c>--continue-at</c>). Every size
/// was measured against the local curl 8.21.0 on 2026-09-26, through the exit 63 message
/// (<c>Exceeded the maximum allowed file size (N) with N bytes</c>) or, for sizes too large to
/// reach with a test file, the value <c>--libcurl</c> writes for <c>CURLOPT_MAXFILESIZE_LARGE</c>.
/// </summary>
[TestClass]
public sealed class CommandLineSizeAndOffsetTests
{
    private const string MaxFileSizeOption = "--max-filesize";

    private const string ContinueAtOption = "-C";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("9", 9L)]
    [DataRow("0", 0L)]
    [DataRow("007", 7L)]
    [DataRow("1b", 1L)]
    [DataRow("1B", 1L)]
    [DataRow("1k", 1024L)]
    [DataRow("1K", 1024L)]
    [DataRow("1m", 1048576L)]
    [DataRow("1G", 1073741824L)]
    [DataRow("1.5t", 1649267441664L)]
    [DataRow("1.5p", 1688849860263936L)]
    [DataRow("0k", 0L)]
    [DataRow("9223372036854775807b", long.MaxValue)]
    [DataRow("9223372036854775807", long.MaxValue)]
    [DataRow("8191P", 9222246136947933184L)]
    public void ParseSize_WholeUnits_MultipliesByTheUnit(string value, long expected)
    {
        Assert.IsNull(ParseSize(value, out long size));
        Diagnostics.Assert("size", expected, size);
        Assert.AreEqual(expected, size);
    }

    // The fraction keeps 3 digits for k, 6 for m, 9 for g, 12 for t and 15 for p, rounds
    // down, and divides the unit first when the digits times the unit would not fit.
    [TestMethod]
    [DataRow("1.5k", 1536L)]
    [DataRow("0.5K", 512L)]
    [DataRow("1.05k", 1075L)]
    [DataRow("00.5k", 512L)]
    [DataRow("0.001k", 1L)]
    [DataRow("0.0009k", 0L)]
    [DataRow("0.0010k", 1L)]
    [DataRow("1.3333k", 1364L)]
    [DataRow("0.99999k", 1022L)]
    [DataRow("0.9999999999k", 1022L)]
    [DataRow("0.12345678901234567k", 125L)]
    [DataRow("0.1234567890123456789k", 125L)]
    [DataRow("0.0000000000000000001k", 0L)]
    [DataRow("0.5m", 524288L)]
    [DataRow("1.25M", 1310720L)]
    [DataRow("0.000001m", 1L)]
    [DataRow("0.0000001m", 0L)]
    [DataRow("0.9999999m", 1048574L)]
    [DataRow("0.999999999999g", 1073741822L)]
    [DataRow("0.9999999999g", 1073741822L)]
    [DataRow("0.9999999999999t", 999999999999L)]
    [DataRow("0.99999999999999999p", 999999999999999L)]
    [DataRow("8191.99999p", 9223372025595734116L)]
    [DataRow("8191.9999999999999999p", 9223246136947933183L)]
    [DataRow("9007199254740991.9999k", 9223372036854775806L)]
    [DataRow("9007199254740991.999k", 9223372036854775806L)]
    public void ParseSize_Fraction_KeepsTheUnitsDecimalPlacesAndRoundsDown(string value, long expected)
    {
        Assert.IsNull(ParseSize(value, out long size));
        Diagnostics.Assert("size", expected, size);
        Assert.AreEqual(expected, size);
    }

    [TestMethod]
    [DataRow("", "expected a proper numerical parameter")]
    [DataRow("abc", "expected a proper numerical parameter")]
    [DataRow("-1", "expected a proper numerical parameter")]
    [DataRow("-0", "expected a proper numerical parameter")]
    [DataRow("-1k", "expected a proper numerical parameter")]
    [DataRow(" 1", "expected a proper numerical parameter")]
    [DataRow("+1", "expected a proper numerical parameter")]
    [DataRow("1.", "expected a proper numerical parameter")]
    [DataRow("1.k", "expected a proper numerical parameter")]
    [DataRow("1.999999999999999999999k", "expected a proper numerical parameter")]
    [DataRow("1x", "is badly used here")]
    [DataRow("5.5", "is badly used here")]
    [DataRow("1.0", "is badly used here")]
    [DataRow("1.5", "is badly used here")]
    [DataRow("1.9b", "is badly used here")]
    [DataRow("1.5x", "is badly used here")]
    [DataRow("1kk", "is badly used here")]
    [DataRow("1kb", "is badly used here")]
    [DataRow("1 k", "is badly used here")]
    [DataRow("1K ", "is badly used here")]
    [DataRow("0x10", "is badly used here")]
    [DataRow("8E", "is badly used here")]
    [DataRow("99999999999999999999", "too large number")]
    [DataRow("8192P", "too large number")]
    [DataRow("8192p", "too large number")]
    [DataRow("8388608T", "too large number")]
    [DataRow("8589934592G", "too large number")]
    [DataRow("8796093022208M", "too large number")]
    [DataRow("9007199254740992k", "too large number")]
    public void ParseSize_Unreadable_IsRefusedWithCurlsReason(string value, string reason)
    {
        CommandLineRefusal? refusal = ParseSize(value, out long size);

        Diagnostics.Assert("stderr", CommandLineParseDiagnostics.QuoteEach([$"curl: option --max-filesize: {reason}", CommandLineRefusal.TryHelpLine]), CommandLineParseDiagnostics.QuoteEach(refusal?.StandardErrorLines ?? []));
        Diagnostics.Assert("size", 0L, size);
        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { $"curl: option --max-filesize: {reason}", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(0L, size);
    }

    [TestMethod]
    public void ParseSize_NullArguments_Throw()
    {
        Diagnostics.Arrange("first call", "ParseSize(null, \"1\")");
        Diagnostics.Arrange("second call", $"ParseSize(\"{MaxFileSizeOption}\", null)");

        string? nullOptionParameter = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseSize(null!, "1", out _)).ParamName;
        string? nullValueParameter = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseSize(MaxFileSizeOption, null!, out _)).ParamName;

        Diagnostics.Act("first call throws for", nullOptionParameter);
        Diagnostics.Act("second call throws for", nullValueParameter);
        Diagnostics.Assert("first call parameter", "spelledOption", nullOptionParameter);
        Diagnostics.Assert("second call parameter", "value", nullValueParameter);
        Assert.AreEqual("spelledOption", nullOptionParameter);
        Assert.AreEqual("value", nullValueParameter);
    }

    [TestMethod]
    [DataRow("0", 0L)]
    [DataRow("5", 5L)]
    [DataRow("9223372036854775807", long.MaxValue)]
    public void ParseOffset_Digits_ReadsThem(string value, long expected)
    {
        Assert.IsNull(ParseOffset(value, out long offset));
        Diagnostics.Assert("offset", expected, offset);
        Assert.AreEqual(expected, offset);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("-")]
    [DataRow("-0")]
    [DataRow("5x")]
    [DataRow("9223372036854775808")]
    public void ParseOffset_NotDigitsOrTooLarge_IsRefusedAsNotProperNumerical(string value)
    {
        CommandLineRefusal? refusal = ParseOffset(value, out long offset);

        Diagnostics.Assert("stderr", CommandLineParseDiagnostics.QuoteEach(["curl: option -C: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine]), CommandLineParseDiagnostics.QuoteEach(refusal?.StandardErrorLines ?? []));
        Diagnostics.Assert("offset", 0L, offset);
        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { "curl: option -C: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(0L, offset);
    }

    [TestMethod]
    public void ParseOffset_NullArguments_Throw()
    {
        Diagnostics.Arrange("first call", "ParseOffset(null, \"1\")");
        Diagnostics.Arrange("second call", $"ParseOffset(\"{ContinueAtOption}\", null)");

        string? nullOptionParameter = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseOffset(null!, "1", out _)).ParamName;
        string? nullValueParameter = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineNumber.ParseOffset(ContinueAtOption, null!, out _)).ParamName;

        Diagnostics.Act("first call throws for", nullOptionParameter);
        Diagnostics.Act("second call throws for", nullValueParameter);
        Diagnostics.Assert("first call parameter", "spelledOption", nullOptionParameter);
        Diagnostics.Assert("second call parameter", "value", nullValueParameter);
        Assert.AreEqual("spelledOption", nullOptionParameter);
        Assert.AreEqual("value", nullValueParameter);
    }

    [TestMethod]
    public void BadlyUsedHere_NamesTheOption()
    {
        Diagnostics.Arrange("spelled option", "--max-filesize=1x");

        CommandLineRefusal refusal = CommandLineRefusal.BadlyUsedHere("--max-filesize=1x");

        Diagnostics.Act("exit code", $"{(int)refusal.ExitCode} ({refusal.ExitCode})");
        Diagnostics.Assert(
            "stderr",
            CommandLineParseDiagnostics.QuoteEach(["curl: option --max-filesize=1x: is badly used here", CommandLineRefusal.TryHelpLine]),
            CommandLineParseDiagnostics.QuoteEach(refusal.StandardErrorLines));
        CollectionAssert.AreEqual(
            new[] { "curl: option --max-filesize=1x: is badly used here", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
    }

    [TestMethod]
    public void ContinueAtExclusiveWithRange_Null_Throws()
    {
        Diagnostics.Arrange("spelled option", "null");
        Diagnostics.Arrange("errors hidden", false);

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => CommandLineRefusal.ContinueAtExclusiveWithRange(null!, errorsHidden: false));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("exception", nameof(ArgumentNullException), exception.GetType().Name);
    }

    private CommandLineRefusal? ParseSize(string value, out long size)
    {
        Diagnostics.Arrange("option", MaxFileSizeOption);
        Diagnostics.Arrange("value", "\"" + value + "\"");
        CommandLineRefusal? refusal = CommandLineNumber.ParseSize(MaxFileSizeOption, value, out size);
        ActNumber("size", refusal, size);
        return refusal;
    }

    private CommandLineRefusal? ParseOffset(string value, out long offset)
    {
        Diagnostics.Arrange("option", ContinueAtOption);
        Diagnostics.Arrange("value", "\"" + value + "\"");
        CommandLineRefusal? refusal = CommandLineNumber.ParseOffset(ContinueAtOption, value, out offset);
        ActNumber("offset", refusal, offset);
        return refusal;
    }

    private void ActNumber(string label, CommandLineRefusal? refusal, long number)
    {
        Diagnostics.Act(label, number);
        Diagnostics.Act("refused", refusal is not null);
        foreach (string line in refusal?.StandardErrorLines ?? [])
        {
            Diagnostics.Act("stderr", line);
        }
    }
}
