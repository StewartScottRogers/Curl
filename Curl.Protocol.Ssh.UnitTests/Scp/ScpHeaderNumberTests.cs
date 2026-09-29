namespace Curl.Protocol.Ssh.Scp;

/// <summary>
/// Pins <see cref="ScpHeaderNumber" /> to C's <c>strtol</c> and <c>strtoll</c> as libssh2
/// 1.11.1 uses them on an SCP <c>C</c> line: the whole field must be a number.
/// </summary>
[TestClass]
public sealed class ScpHeaderNumberTests
{
    [TestMethod]
    [DataRow("0644", 8, 420L, DisplayName = "octal mode")]
    [DataRow("107777", 8, 36863L, DisplayName = "octal mode with file type bits")]
    [DataRow("11", 10, 11L, DisplayName = "size")]
    [DataRow("+5", 10, 5L, DisplayName = "plus sign")]
    [DataRow("-5", 10, -5L, DisplayName = "minus sign")]
    [DataRow("05", 10, 5L, DisplayName = "leading zero")]
    [DataRow(" \t\n\v\f\r5", 10, 5L, DisplayName = "leading white space")]
    [DataRow("99999999999999999999", 10, long.MaxValue, DisplayName = "too large, clamped")]
    [DataRow("-99999999999999999999", 10, long.MinValue, DisplayName = "too small, clamped")]
    [DataRow("-9223372036854775808", 10, long.MinValue, DisplayName = "smallest")]
    [DataRow("9223372036854775807", 10, long.MaxValue, DisplayName = "largest")]
    public void TryParse_Number_ReadsIt(string text, int radix, long expected)
    {
        Assert.IsTrue(ScpHeaderNumber.TryParse(text, radix, out long value));
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    [DataRow("", 10, DisplayName = "empty")]
    [DataRow("-", 10, DisplayName = "sign alone")]
    [DataRow("+", 10, DisplayName = "plus alone")]
    [DataRow(" ", 10, DisplayName = "space alone")]
    [DataRow("8", 8, DisplayName = "digit outside octal")]
    [DataRow("0x1", 8, DisplayName = "hexadecimal prefix")]
    [DataRow("5x", 10, DisplayName = "letter after the digits")]
    [DataRow("abc", 8, DisplayName = "letters")]
    [DataRow("5 ", 10, DisplayName = "space after the digits")]
    [DataRow("/", 10, DisplayName = "character below the digits")]
    public void TryParse_NotANumber_ReturnsFalse(string text, int radix)
    {
        Assert.IsFalse(ScpHeaderNumber.TryParse(text, radix, out long value));
        Assert.AreEqual(0L, value);
    }
}
