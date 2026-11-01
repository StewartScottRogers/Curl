using System.Text;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Original.Protocol.Ssh.Sftp;

/// <summary>
/// Pins <see cref="SftpQuoteCommand.ReadPathname" /> to every row of curl 8.22.0's
/// <c>tests/unit/unit2604.c</c> (<c>Curl_get_pathname</c>) and
/// <see cref="SftpDownloadPart.ReadRange" /> to every row of <c>unit2605.c</c>
/// (<c>Curl_ssh_range</c>), so the library stands alone without the upstream ports
/// (BL-2905, ADR-0482).
/// </summary>
[TestClass]
public sealed class SshPathnameAndRangeTests
{
    private const string Sixty = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private const string FiveHundredForty = Sixty + Sixty + Sixty + Sixty + Sixty + Sixty + Sixty + Sixty + Sixty;

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(FiveHundredForty + " c", FiveHundredForty, "c", "/", CurlExitCode.Ok, DisplayName = "1: 540 a's then c")]
    [DataRow("\" " + FiveHundredForty + "\" c", " " + FiveHundredForty, "c", "/", CurlExitCode.Ok, DisplayName = "2: quoted space and 540 a's")]
    [DataRow("a a", "a", "a", "/home/", CurlExitCode.Ok, DisplayName = "3: a a")]
    [DataRow("b a", "b", "a", "/", CurlExitCode.Ok, DisplayName = "4: b a")]
    [DataRow("a", "a", "", "/home/", CurlExitCode.Ok, DisplayName = "5: a")]
    [DataRow("b", "b", "", "/", CurlExitCode.Ok, DisplayName = "6: b")]
    [DataRow("\"foo bar\"\tb", "foo bar", "b", "/", CurlExitCode.Ok, DisplayName = "7: quoted, then a tab")]
    [DataRow("/~/hej", "/home/user/hej", "", "/home/user", CurlExitCode.Ok, DisplayName = "8: /~/ is the home directory")]
    [DataRow("\"foo bar", "", "", "/", CurlExitCode.QuoteError, DisplayName = "9: unclosed quote")]
    [DataRow("\"foo\\\"bar\" a", "foo\"bar", "a", "/", CurlExitCode.Ok, DisplayName = "10: escaped double quote")]
    [DataRow("\"foo\\'bar\" b", "foo'bar", "b", "/", CurlExitCode.Ok, DisplayName = "11: escaped single quote")]
    [DataRow("\"foo\\\\bar\" c", "foo\\bar", "c", "/", CurlExitCode.Ok, DisplayName = "12: escaped backslash")]
    [DataRow("\"foo\\pbar\" c", "", "", "/", CurlExitCode.QuoteError, DisplayName = "13: unknown escape")]
    [DataRow("\"\" c", "", "", "", CurlExitCode.QuoteError, DisplayName = "14: empty quote")]
    [DataRow("foo\"", "foo\"", "", "/", CurlExitCode.Ok, DisplayName = "15: quote ending a word")]
    [DataRow("foo \"", "foo", "\"", "/", CurlExitCode.Ok, DisplayName = "16: quote after a word")]
    [DataRow("   \t\t   \t  ", "", "", "/", CurlExitCode.QuoteError, DisplayName = "17: spaces and tabs only")]
    [DataRow("              ", "", "", "/", CurlExitCode.QuoteError, DisplayName = "18: spaces only")]
    [DataRow("", "", "", "/", CurlExitCode.QuoteError, DisplayName = "19: empty")]
    [DataRow("       \r \n  ", "\r", "\n  ", "/", CurlExitCode.Ok, DisplayName = "20: carriage return is a word")]
    public void ReadPathname_EachRowOfUnit2604_GivesCurlsCodeWordAndRest(string value, string path, string rest, string home, CurlExitCode result) =>
        AssertPathname(value, home, result, path, rest);

    [TestMethod]
    public void ReadPathname_Unit2604Row0WordOf90720Bytes_IsTooLarge() =>
        AssertPathname(new string('a', 90720), "", CurlExitCode.TooLarge, "", "");

    [TestMethod]
    [DataRow(65534, CurlExitCode.Ok, DisplayName = "one byte under curl's MAX_PATHLENGTH")]
    [DataRow(65535, CurlExitCode.TooLarge, DisplayName = "curl's MAX_PATHLENGTH")]
    public void ReadPathname_WordAtCurlsPathLimit_IsTooLargeFromTheLimit(int length, CurlExitCode result)
    {
        string word = new('a', length);
        AssertPathname(word, "/", result, result == CurlExitCode.Ok ? word : "", "");
    }

    [TestMethod]
    [DataRow(65534, CurlExitCode.Ok, DisplayName = "one byte under curl's MAX_PATHLENGTH")]
    [DataRow(65535, CurlExitCode.TooLarge, DisplayName = "curl's MAX_PATHLENGTH")]
    [DataRow(90720, CurlExitCode.TooLarge, DisplayName = "unit2604's too-long length")]
    public void ReadPathname_QuotedPathAtCurlsPathLimit_IsTooLargeFromTheLimit(int length, CurlExitCode result)
    {
        string word = new('a', length);
        AssertPathname($"\"{word}\" c", "/", result, result == CurlExitCode.Ok ? word : "", result == CurlExitCode.Ok ? "c" : "");
    }

    [TestMethod]
    [DataRow("0-9", 100L, 0L, 10L, CurlExitCode.Ok)]
    [DataRow("1-10", 100L, 1L, 10L, CurlExitCode.Ok)]
    [DataRow("222222-222222", 300000L, 222222L, 1L, CurlExitCode.Ok)]
    [DataRow("4294967296 - 4294967297", 4294967298L, 4294967296L, 2L, CurlExitCode.Ok)]
    [DataRow("-10", 100L, 90L, 10L, CurlExitCode.Ok)]
    [DataRow("-20", 100L, 80L, 20L, CurlExitCode.Ok)]
    [DataRow("-1", 100L, 99L, 1L, CurlExitCode.Ok)]
    [DataRow("-0", 100L, 0L, 0L, CurlExitCode.RangeError)]
    [DataRow("--2", 100L, 0L, 0L, CurlExitCode.RangeError)]
    [DataRow("-100", 100L, 0L, 100L, CurlExitCode.Ok)]
    [DataRow("-101", 100L, 0L, 100L, CurlExitCode.Ok)]
    [DataRow("-1000", 100L, 0L, 100L, CurlExitCode.Ok)]
    [DataRow("2-1000", 100L, 2L, 98L, CurlExitCode.Ok)]
    [DataRow(".2-3", 100L, 0L, 0L, CurlExitCode.RangeError)]
    [DataRow("+2-3", 100L, 0L, 0L, CurlExitCode.RangeError)]
    [DataRow("2 - 3", 100L, 2L, 2L, CurlExitCode.Ok)]
    [DataRow(" 2 - 3", 100L, 0L, 0L, CurlExitCode.RangeError, DisplayName = "no leading space")]
    [DataRow("2 - 3 ", 100L, 0L, 0L, CurlExitCode.RangeError, DisplayName = "no trailing space")]
    [DataRow("3-2", 100L, 0L, 0L, CurlExitCode.RangeError)]
    [DataRow("2.-3", 100L, 0L, 0L, CurlExitCode.RangeError)]
    [DataRow("-3-2", 100L, 0L, 0L, CurlExitCode.RangeError)]
    [DataRow("101-102", 100L, 0L, 0L, CurlExitCode.RangeError)]
    [DataRow("0-", 100L, 0L, 100L, CurlExitCode.Ok)]
    [DataRow("1-", 100L, 1L, 99L, CurlExitCode.Ok)]
    [DataRow("99-", 100L, 99L, 1L, CurlExitCode.Ok)]
    [DataRow("100-", 100L, 0L, 0L, CurlExitCode.RangeError)]
    public void ReadRange_EachRowOfUnit2605_GivesCurlsCodeStartAndSize(string range, long fileSize, long start, long size, CurlExitCode result)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("range", range);
        diagnostics.Arrange("file size", fileSize.ToString(System.Globalization.CultureInfo.InvariantCulture));

        (CurlExitCode actualResult, long actualStart, long actualSize) = SftpDownloadPart.ReadRange(range, fileSize);
        diagnostics.Act("code, start, size", $"{actualResult}, {actualStart}, {actualSize}");

        Assert.AreEqual(result, actualResult);
        Assert.AreEqual(start, actualStart);
        Assert.AreEqual(size, actualSize);
    }

    private void AssertPathname(string value, string home, CurlExitCode result, string path, string rest)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("value", value.Length > 600 ? $"{value.Length} characters" : value);
        diagnostics.Arrange("home directory", home);

        (CurlExitCode actualResult, byte[] actualPath, string actualRest) = SftpQuoteCommand.ReadPathname(value, Encoding.UTF8.GetBytes(home));
        diagnostics.Act("code, rest", $"{actualResult}, '{actualRest}'");

        Assert.AreEqual(result, actualResult);
        Assert.AreEqual(path, Encoding.UTF8.GetString(actualPath));
        Assert.AreEqual(rest, actualRest);
    }
}
