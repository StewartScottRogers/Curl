using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how a numeric option value is read: an optional leading minus, then one or more
/// ASCII digits and nothing else, fitting in an <see cref="int"/>. Anything malformed is
/// refused as "expected a proper numerical parameter", a negative value as "expected a
/// positive numerical parameter", both naming the option as it was spelled and leaving
/// the number at zero.
/// </summary>
[TestClass]
public sealed class CommandLineNumberTests
{
    private const string BlockSizeOption = "--tftp-blksize";

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
}
