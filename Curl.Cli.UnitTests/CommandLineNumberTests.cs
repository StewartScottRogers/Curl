using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how a numeric option value is read: an optional leading minus, then one or more
/// ASCII digits and nothing else, no larger than the platform's C <c>LONG_MAX</c>: 2^31-1 on
/// Windows, 2^63-1 on Linux and macOS (ADR-0019), each reading tested here by passing its ceiling. Anything malformed is
/// refused as "expected a proper numerical parameter", a negative value as "expected a
/// positive numerical parameter", both naming the option as it was spelled and leaving
/// the number at zero. An octal value is unsigned digits 0-7 up to a maximum: past the
/// maximum is "too large number", anything else malformed is "expected a proper numerical
/// parameter" (both measured against curl 8.21.0's <c>--create-file-mode</c>).
/// </summary>
[TestClass]
public sealed class CommandLineNumberTests
{
    private const string BlockSizeOption = "--tftp-blksize";

    private const string CreateFileModeOption = "--create-file-mode";

    private const int Octal777 = 0b111_111_111;

    private static readonly long WindowsLongMaximum = CommandLineNumber.LongMaximumFor(isWindows: true);

    private static readonly long UnixLongMaximum = CommandLineNumber.LongMaximumFor(isWindows: false);

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ParseNonNegative_Abc_RefusesAsNotProperNumerical()
    {
        CommandLineRefusal? refusal = ParseNonNegative(BlockSizeOption, "abc", WindowsLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0, number);
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" 5")]
    [DataRow("5 ")]
    [DataRow("+5")]
    [DataRow("0x10")]
    [DataRow("1.5")]
    [DataRow("-")]
    [DataRow("-abc")]
    [DataRow("2147483648")]
    [DataRow("99999999999999999999")]
    [DataRow("５")]
    public void ParseNonNegative_Malformed_RefusesAsNotProperNumerical(string value)
    {
        CommandLineRefusal? refusal = ParseNonNegative(BlockSizeOption, value, WindowsLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0, number);
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("-5")]
    public void ParseNonNegative_Negative_RefusesAsNotPositiveNumerical(string value)
    {
        CommandLineRefusal? refusal = ParseNonNegative(BlockSizeOption, value, WindowsLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize: expected a positive numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0, number);
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("007", 7)]
    [DataRow("-0", 0)]
    [DataRow("2147483647", int.MaxValue)]
    public void ParseNonNegative_Valid_ReturnsNumber(string value, long expected)
    {
        CommandLineRefusal? refusal = ParseNonNegative(BlockSizeOption, value, WindowsLongMaximum, out long number);

        Assert.IsNull(refusal);
        Diagnostics.Assert("number", expected, number);
        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    public void ParseNonNegative_TwoToThe31UnderWindowsCeiling_RefusesAsNotProperNumerical()
    {
        CommandLineRefusal? refusal = ParseNonNegative(BlockSizeOption, "2147483648", WindowsLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0L, number);
        Assert.AreEqual(0L, number);
    }

    [TestMethod]
    [DataRow("2147483648", 2147483648L)]
    [DataRow("9223372036854775807", long.MaxValue)]
    public void ParseNonNegative_PastIntUnderUnixCeiling_ReturnsNumber(string value, long expected)
    {
        CommandLineRefusal? refusal = ParseNonNegative(BlockSizeOption, value, UnixLongMaximum, out long number);

        Assert.IsNull(refusal);
        Diagnostics.Assert("number", expected, number);
        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    [DataRow("9223372036854775808")]
    [DataRow("99999999999999999999")]
    public void ParseNonNegative_PastLongUnderUnixCeiling_RefusesAsNotProperNumerical(string value)
    {
        CommandLineRefusal? refusal = ParseNonNegative(BlockSizeOption, value, UnixLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0L, number);
        Assert.AreEqual(0L, number);
    }

    [TestMethod]
    [DataRow("2147483648", 2147483648L)]
    [DataRow("9223372036854775807", long.MaxValue)]
    [DataRow("-1", -1L)]
    public void ParseMinusOneOrMore_UnderUnixCeiling_ReturnsNumber(string value, long expected)
    {
        CommandLineRefusal? refusal = ParseMinusOneOrMore("--max-redirs", value, UnixLongMaximum, out long number);

        Assert.IsNull(refusal);
        Diagnostics.Assert("number", expected, number);
        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    [DataRow("9223372036854775808")]
    [DataRow("-9223372036854775808")]
    public void ParseMinusOneOrMore_PastLongUnderUnixCeiling_RefusesAsNotProperNumerical(string value)
    {
        CommandLineRefusal? refusal = ParseMinusOneOrMore("--max-redirs", value, UnixLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { "curl: option --max-redirs: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0L, number);
        Assert.AreEqual(0L, number);
    }

    [TestMethod]
    public void ParseMinusOneOrMore_TwoToThe31UnderWindowsCeiling_RefusesAsNotProperNumerical()
    {
        CommandLineRefusal? refusal = ParseMinusOneOrMore("--max-redirs", "2147483648", WindowsLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        Diagnostics.Assert("number", 0L, number);
        Assert.AreEqual(0L, number);
    }

    [TestMethod]
    public void LongMaximumFor_Windows_IsTwoToThe31MinusOne()
    {
        Diagnostics.Arrange("is windows", true);
        long ceiling = CommandLineNumber.LongMaximumFor(isWindows: true);
        Diagnostics.Act("ceiling", ceiling);
        Diagnostics.Assert("ceiling", 2147483647L, ceiling);

        Assert.AreEqual(2147483647L, CommandLineNumber.LongMaximumFor(isWindows: true));
    }

    [TestMethod]
    public void LongMaximumFor_LinuxAndMacOS_IsTwoToThe63MinusOne()
    {
        Diagnostics.Arrange("is windows", false);
        long ceiling = CommandLineNumber.LongMaximumFor(isWindows: false);
        Diagnostics.Act("ceiling", ceiling);
        Diagnostics.Assert("ceiling", 9223372036854775807L, ceiling);

        Assert.AreEqual(9223372036854775807L, CommandLineNumber.LongMaximumFor(isWindows: false));
    }

    [TestMethod]
    public void PlatformLongMaximum_IsTheCeilingOfThisOperatingSystem()
    {
        Diagnostics.Arrange("expected ceiling", "LongMaximumFor(OperatingSystem.IsWindows())");
        bool isThisOperatingSystemsCeiling = CommandLineNumber.PlatformLongMaximum == CommandLineNumber.LongMaximumFor(OperatingSystem.IsWindows());
        Diagnostics.Act("platform ceiling matches", isThisOperatingSystemsCeiling);
        Diagnostics.Assert("platform ceiling matches", true, isThisOperatingSystemsCeiling);

        Assert.AreEqual(CommandLineNumber.LongMaximumFor(OperatingSystem.IsWindows()), CommandLineNumber.PlatformLongMaximum);
    }

    [TestMethod]
    public void ParseNonNegative_SpelledWithEquals_NamesWholeArgument()
    {
        CommandLineRefusal? refusal = ParseNonNegative("--tftp-blksize=abc", "abc", WindowsLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize=abc: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0, number);
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    public void ParseNonNegative_NullSpelledOption_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "ParseNonNegative(null, \"5\", WindowsLongMaximum)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseNonNegative(null!, "5", WindowsLongMaximum, out _));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "spelledOption", exception.ParamName);
        Assert.AreEqual("spelledOption", exception.ParamName);
    }

    [TestMethod]
    public void ParseNonNegative_NullValue_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "ParseNonNegative(BlockSizeOption, null, WindowsLongMaximum)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseNonNegative(BlockSizeOption, null!, WindowsLongMaximum, out _));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "value", exception.ParamName);
        Assert.AreEqual("value", exception.ParamName);
    }

    [TestMethod]
    public void ParseMinusOneOrMore_NullSpelledOption_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "ParseMinusOneOrMore(null, \"5\", WindowsLongMaximum)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseMinusOneOrMore(null!, "5", WindowsLongMaximum, out _));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "spelledOption", exception.ParamName);
        Assert.AreEqual("spelledOption", exception.ParamName);
    }

    [TestMethod]
    public void ParseMinusOneOrMore_NullValue_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "ParseMinusOneOrMore(\"--max-redirs\", null, WindowsLongMaximum)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseMinusOneOrMore("--max-redirs", null!, WindowsLongMaximum, out _));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "value", exception.ParamName);
        Assert.AreEqual("value", exception.ParamName);
    }

    [TestMethod]
    public void ParseMinusOneOrMore_Refused_LeavesNumberZero()
    {
        CommandLineRefusal? refusal = ParseMinusOneOrMore("--max-redirs", "-2", WindowsLongMaximum, out long number);

        Assert.IsNotNull(refusal);
        Diagnostics.Assert("number", 0, number);
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("0000", 0)]
    [DataRow("7", 7)]
    [DataRow("0640", 0b110_100_000)]
    [DataRow("777", Octal777)]
    [DataRow("00000000777", Octal777)]
    public void ParseOctal_Valid_ReturnsNumber(string value, int expected)
    {
        CommandLineRefusal? refusal = ParseOctal(CreateFileModeOption, value, Octal777, out int number);

        Assert.IsNull(refusal);
        Diagnostics.Assert("number", expected, number);
        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("abc")]
    [DataRow("8")]
    [DataRow("18")]
    [DataRow("7a")]
    [DataRow("7 ")]
    [DataRow(" 7")]
    [DataRow("+7")]
    [DataRow("-0")]
    [DataRow("-1")]
    [DataRow("0x7")]
    [DataRow("=7")]
    public void ParseOctal_Malformed_RefusesAsNotProperNumerical(string value)
    {
        CommandLineRefusal? refusal = ParseOctal(CreateFileModeOption, value, Octal777, out int number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --create-file-mode: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0, number);
        Assert.AreEqual(0, number);
    }

    // curl refuses a value past the maximum as its digits are read, before it looks at
    // what follows them, so 10008 is too large rather than malformed.
    [TestMethod]
    [DataRow("1000")]
    [DataRow("1777")]
    [DataRow("07777")]
    [DataRow("10008")]
    [DataRow("777777777777777777777777777")]
    public void ParseOctal_PastMaximum_RefusesAsTooLarge(string value)
    {
        CommandLineRefusal? refusal = ParseOctal(CreateFileModeOption, value, Octal777, out int number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --create-file-mode: too large number", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Diagnostics.Assert("number", 0, number);
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    public void ParseOctal_NullSpelledOption_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "ParseOctal(null, \"7\", Octal777)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseOctal(null!, "7", Octal777, out _));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "spelledOption", exception.ParamName);
        Assert.AreEqual("spelledOption", exception.ParamName);
    }

    [TestMethod]
    public void ParseOctal_NullValue_ThrowsArgumentNull()
    {
        Diagnostics.Arrange("call", "ParseOctal(CreateFileModeOption, null, Octal777)");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseOctal(CreateFileModeOption, null!, Octal777, out _));

        Diagnostics.Act("exception", exception.GetType().Name);
        Diagnostics.Assert("parameter name", "value", exception.ParamName);
        Assert.AreEqual("value", exception.ParamName);
    }

    private CommandLineRefusal? ParseNonNegative(string spelledOption, string value, long ceiling, out long number)
    {
        ArrangeCall(spelledOption, value, ceiling);
        CommandLineRefusal? refusal = CommandLineNumber.ParseNonNegative(spelledOption, value, ceiling, out number);
        ActRefusal(refusal, number);
        return refusal;
    }

    private CommandLineRefusal? ParseMinusOneOrMore(string spelledOption, string value, long ceiling, out long number)
    {
        ArrangeCall(spelledOption, value, ceiling);
        CommandLineRefusal? refusal = CommandLineNumber.ParseMinusOneOrMore(spelledOption, value, ceiling, out number);
        ActRefusal(refusal, number);
        return refusal;
    }

    private CommandLineRefusal? ParseOctal(string spelledOption, string value, int maximum, out int number)
    {
        ArrangeCall(spelledOption, value, maximum);
        CommandLineRefusal? refusal = CommandLineNumber.ParseOctal(spelledOption, value, maximum, out number);
        ActRefusal(refusal, number);
        return refusal;
    }

    private void ArrangeCall(string spelledOption, string value, long ceiling)
    {
        Diagnostics.Arrange("spelled option", CommandLineParseDiagnostics.QuoteEach([spelledOption]));
        Diagnostics.Arrange("value", CommandLineParseDiagnostics.QuoteEach([value]));
        Diagnostics.Arrange("ceiling", ceiling);
    }

    private void ActRefusal(CommandLineRefusal? refusal, long number)
    {
        Diagnostics.Act("refused", refusal is not null);
        if (refusal is not null)
        {
            Diagnostics.Act("exit code", $"{(int)refusal.ExitCode} ({refusal.ExitCode})");
            foreach (string line in refusal.StandardErrorLines)
            {
                Diagnostics.Act("stderr", line);
            }
        }

        Diagnostics.Act("number", number);
    }
}
