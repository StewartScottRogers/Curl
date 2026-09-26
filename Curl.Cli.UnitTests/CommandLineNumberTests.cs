using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how a numeric option value is read: an optional leading minus, then one or more
/// ASCII digits and nothing else, fitting in an <see cref="int"/>. Anything malformed is
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

    [TestMethod]
    public void ParseNonNegative_Abc_RefusesAsNotProperNumerical()
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseNonNegative(BlockSizeOption, "abc", out int number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
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
        CommandLineRefusal? refusal = CommandLineNumber.ParseNonNegative(BlockSizeOption, value, out int number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    [DataRow("-1")]
    [DataRow("-5")]
    public void ParseNonNegative_Negative_RefusesAsNotPositiveNumerical(string value)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseNonNegative(BlockSizeOption, value, out int number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize: expected a positive numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("007", 7)]
    [DataRow("-0", 0)]
    [DataRow("2147483647", int.MaxValue)]
    public void ParseNonNegative_Valid_ReturnsNumber(string value, int expected)
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseNonNegative(BlockSizeOption, value, out int number);

        Assert.IsNull(refusal);
        Assert.AreEqual(expected, number);
    }

    [TestMethod]
    public void ParseNonNegative_SpelledWithEquals_NamesWholeArgument()
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseNonNegative("--tftp-blksize=abc", "abc", out int number);

        Assert.IsNotNull(refusal);
        CollectionAssert.AreEqual(
            new[] { "curl: option --tftp-blksize=abc: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    public void ParseNonNegative_NullSpelledOption_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseNonNegative(null!, "5", out _));

        Assert.AreEqual("spelledOption", exception.ParamName);
    }

    [TestMethod]
    public void ParseNonNegative_NullValue_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseNonNegative(BlockSizeOption, null!, out _));

        Assert.AreEqual("value", exception.ParamName);
    }

    [TestMethod]
    public void ParseMinusOneOrMore_NullSpelledOption_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseMinusOneOrMore(null!, "5", out _));

        Assert.AreEqual("spelledOption", exception.ParamName);
    }

    [TestMethod]
    public void ParseMinusOneOrMore_NullValue_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseMinusOneOrMore("--max-redirs", null!, out _));

        Assert.AreEqual("value", exception.ParamName);
    }

    [TestMethod]
    public void ParseMinusOneOrMore_Refused_LeavesNumberZero()
    {
        CommandLineRefusal? refusal = CommandLineNumber.ParseMinusOneOrMore("--max-redirs", "-2", out int number);

        Assert.IsNotNull(refusal);
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
        CommandLineRefusal? refusal = CommandLineNumber.ParseOctal(CreateFileModeOption, value, Octal777, out int number);

        Assert.IsNull(refusal);
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
        CommandLineRefusal? refusal = CommandLineNumber.ParseOctal(CreateFileModeOption, value, Octal777, out int number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --create-file-mode: expected a proper numerical parameter", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
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
        CommandLineRefusal? refusal = CommandLineNumber.ParseOctal(CreateFileModeOption, value, Octal777, out int number);

        Assert.IsNotNull(refusal);
        Assert.AreEqual(CurlExitCode.FailedInit, refusal.ExitCode);
        CollectionAssert.AreEqual(
            new[] { "curl: option --create-file-mode: too large number", CommandLineRefusal.TryHelpLine },
            refusal.StandardErrorLines.ToArray());
        Assert.AreEqual(0, number);
    }

    [TestMethod]
    public void ParseOctal_NullSpelledOption_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseOctal(null!, "7", Octal777, out _));

        Assert.AreEqual("spelledOption", exception.ParamName);
    }

    [TestMethod]
    public void ParseOctal_NullValue_ThrowsArgumentNull()
    {
        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(
            () => CommandLineNumber.ParseOctal(CreateFileModeOption, null!, Octal777, out _));

        Assert.AreEqual("value", exception.ParamName);
    }
}
