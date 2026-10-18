using System.Text;
using Libcurl.Misc.Printf;

namespace Libcurl.UnitTests.Misc.Printf;

// Every curl_msnprintf check of curl 8.22.0's tests/libtest/lib557.c in test_string_formatting,
// test_pos_arguments, test_weird_arguments (with test_width_precision), test_float_formatting,
// test_oct_hex_formatting, test_return_codes and test_vversions, through the public API.
public sealed partial class CurlPrintfTests
{
    // lib557's MAXIMIZE: -DBL_MAX.
    private const double Maximize = -1.7976931348623157081452E+308;

    [TestMethod]
    [DataRow("%0*d%s", new object[] { 2, 9, "foo" }, "09foo")]
    [DataRow("%*.*s", new object[] { 5, 2, "foo" }, "   fo")]
    [DataRow("%*.*s", new object[] { 2, 5, "foo" }, "foo")]
    [DataRow("%*.*s", new object[] { 0, 10, "foo" }, "foo")]
    [DataRow("%-10s", new object[] { "foo" }, "foo       ")]
    [DataRow("%10s", new object[] { "foo" }, "       foo")]
    [DataRow("%*.*s", new object[] { -10, -10, "foo" }, "foo       ")]
    public void MSNPrintf_StringFormatting_MatchesLib557(string format, object[] arguments, string expected)
    {
        Assert.AreEqual(expected, Format(256, format, arguments).Text);
    }

    [TestMethod]
    [DataRow("%3$d %2$d %1$d", "502 501 500")]
    [DataRow("%3$d %1$d %2$d", "502 500 501")]
    [DataRow("%3$d %d %2$d", "")]
    public void MSNPrintf_PositionalArguments_MatchesLib557(string format, string expected)
    {
        Assert.AreEqual(expected, Format(256, format, 500, 501, 502).Text);
    }

    [TestMethod]
    public void MSNPrintf_WeirdArguments_MatchesLib557()
    {
        Assert.AreEqual("500                 % right? %", Format(256, "%-20d%% right? %%", 500).Text);
        Assert.AreEqual(new string('%', 50), Format(256, new string('%', 100)).Text);
        Assert.AreEqual("%2 AA 500 %K", Format(256, "%2 AA %d %K", 500, 501, 502).Text);
        Assert.AreEqual("%2 500 %K", Format(256, "%2 %d %K", 500, 501, 502).Text);
    }

    [TestMethod]
    public void MSNPrintf_ExactlyMaxParameters_PrintsAll128()
    {
        object[] arguments = Enumerable.Range(0, 128).Select(n => (object)(n % 10)).ToArray();
        string expected = string.Concat(Enumerable.Range(0, 128).Select(n => n % 10));

        (int count, string text) = Format(256, string.Concat(Enumerable.Repeat("%d", 128)), arguments);

        Assert.AreEqual(128, count);
        Assert.AreEqual(expected, text);
    }

    [TestMethod]
    public void MSNPrintf_MoreThanMaxParameters_PrintsNothing()
    {
        object[] arguments = Enumerable.Range(0, 129).Select(n => (object)(n % 10)).ToArray();

        (int count, string text) = Format(256, string.Concat(Enumerable.Repeat("%d", 129)), arguments);

        Assert.AreEqual(0, count);
        Assert.AreEqual(string.Empty, text);
    }

    [TestMethod]
    [DataRow("%325.325f", 325)]
    [DataRow("%326.326f", 325)]
    [DataRow("%1000.1000f", 325)]
    [DataRow("%324.324f", 324)]
    [DataRow("%324.0f", 324)]
    [DataRow("%0.324f", 325)]
    public void MSNPrintf_WidthAndPrecisionLimits_MatchLib557(string format, int expectedCount)
    {
        (int count, string text) = Format(1024, format, 0.1);

        Assert.AreEqual(expectedCount, count);
        if (expectedCount == 325 && format != "%0.324f")
        {
            Assert.AreEqual(new string(' ', 324) + "0", text);
        }
    }

    [TestMethod]
    [DataRow("%f", 9.0, "9.000000")]
    [DataRow("%F", 9.0, "9.000000")]
    [DataRow("%.1f", 9.1, "9.1")]
    [DataRow("%.2f", 9.1, "9.10")]
    [DataRow("%.0f", 9.1, "9")]
    [DataRow("%0f", 9.1, "9.100000")]
    [DataRow("%10f", 9.1, "  9.100000")]
    [DataRow("%10.3f", 9.1, "     9.100")]
    [DataRow("%-10.3f", 9.1, "9.100     ")]
    [DataRow("%-10.3f", 9.123456, "9.123     ")]
    [DataRow("%.-2f", 9.1, "9.100000")]
    public void MSNPrintf_FloatFormatting_MatchesLib557(string format, double value, string expected)
    {
        Assert.AreEqual(expected, Format(512, format, value).Text);
    }

    [TestMethod]
    [DataRow("%*f", 10, 9.1, "  9.100000")]
    [DataRow("%*f", 3, 9.1, "9.100000")]
    [DataRow("%*f", 6, 9.2987654, "9.298765")]
    [DataRow("%*f", 6, 9.298765, "9.298765")]
    [DataRow("%*f", 6, 9.29876, "9.298760")]
    [DataRow("%.*f", 6, 9.2987654, "9.298765")]
    [DataRow("%.*f", 5, 9.2987654, "9.29877")]
    [DataRow("%.*f", 4, 9.2987654, "9.2988")]
    [DataRow("%.*f", 3, 9.2987654, "9.299")]
    [DataRow("%.*f", 2, 9.2987654, "9.30")]
    [DataRow("%.*f", 1, 9.2987654, "9.3")]
    [DataRow("%.*f", 0, 9.2987654, "9")]
    public void MSNPrintf_FloatWidthOrPrecisionArgument_MatchesLib557(string format, int size, double value, string expected)
    {
        Assert.AreEqual(expected, Format(512, format, size, value).Text);
    }

    [TestMethod]
    public void MSNPrintf_FloatLengthLimits_MatchLib557()
    {
        Assert.AreEqual(325, Format(512, "%.*f", 1 << 30, 9.2987654).Text.Length);
        Assert.AreEqual(325, Format(512, "%10000.10000f", 9.2987654).Text.Length);
        Assert.AreEqual(325, Format(512, "%240.10000f", 123456789123456789123456789.2987654).Text.Length);
        Assert.AreEqual("9.100000" + new string(' ', 317), Format(512, "%*f", int.MinValue, 9.1).Text);
        Assert.AreEqual(new string(' ', 317) + "9.100000", Format(512, "%*f", 1 << 30, 9.1).Text);
        Assert.AreEqual(new string(' ', 317) + "9.100000", Format(512, "%100000f", 9.1).Text);
    }

    [TestMethod]
    [DataRow(512, 317)]
    [DataRow(2, 1)]
    [DataRow(3, 2)]
    [DataRow(4, 3)]
    [DataRow(5, 4)]
    [DataRow(6, 5)]
    public void MSNPrintf_MinusDoubleMax_IsCutToTheBuffer(int size, int expectedLength)
    {
        Assert.AreEqual(expectedLength, Format(size, "%f", Maximize).Text.Length);
    }

    [TestMethod]
    [DataRow("%.17Lf", "1.23456789012345", "")]
    [DataRow("%.17Le", "1.234567890123456", "e+00")]
    [DataRow("%.17LE", "1.234567890123456", "E+00")]
    [DataRow("%.17Lg", "1.234567890123456", "")]
    [DataRow("%.17LG", "1.234567890123456", "")]
    public void MSNPrintf_LongDoublePrecision_MatchesLib557DoubleCheck(string format, string prefix, string suffix)
    {
        string text = Format(128, format, 1.234567890123456789).Text;

        Assert.IsTrue(text.StartsWith(prefix, StringComparison.Ordinal), text);
        Assert.IsTrue(text.EndsWith(suffix, StringComparison.Ordinal), text);
    }

    [TestMethod]
    public void MSNPrintf_OctalAndHex_MatchesLib557()
    {
        Assert.AreEqual(
            "175020 fa10 FA10 +FA10 +fa10",
            Format(256, "%ho %hx %hX %+X %+x", 0xFA10U, 0xFA10U, 0xFA10U, 0xFA10, 0xFA10).Text);
        Assert.AreEqual(
            "37257011060 fabc1230 FABC1230 +2322",
            Format(256, "%o %x %X %+o", 0xFABC1230U, 0xFABC1230U, 0xFABC1230U, 1234U).Text);
    }

    [TestMethod]
    [DataRow(100, "%d", 9999, 4)]
    [DataRow(100, "%d", 99999, 5)]
    [DataRow(5, "%d", 99999, 4)]
    [DataRow(5, "%s", "helloooooooo", 4)]
    [DataRow(6, "%s", "helloooooooo", 5)]
    public void MSNPrintf_ReturnCodes_MatchLib557(int size, string format, object value, int expected)
    {
        Assert.AreEqual(expected, Format(size, format, value).Count);
    }

    [TestMethod]
    public void EveryFunction_Lib557VVersions_AnswersTheSameLength()
    {
        const string format = "testing %s and %d is %s and %ld\n";
        object[] arguments = ["a string", -443, "super fun", 9876543L];
        var buffer = new byte[80];
        using var stream = new MemoryStream();

        Assert.AreEqual(51, CurlPrintf.MFPrintf(stream, format, arguments));
        Assert.AreEqual(51, stream.Length);
        Assert.AreEqual(51, CurlPrintf.MPrintf(format, arguments));
        Assert.AreEqual(51, CurlPrintf.MSNPrintf(buffer, format, arguments));
        Assert.AreEqual(51, CurlPrintf.MSPrintf(buffer, format, arguments));
        Assert.AreEqual("testing a string and -443 is super fun and 9876543\n", CurlPrintf.MAPrintf(format, arguments));
    }

    private static (int Count, string Text) Format(int size, string format, params object?[] arguments)
    {
        var buffer = new byte[size];
        int count = CurlPrintf.MSNPrintf(buffer, format, arguments);
        int end = Array.IndexOf(buffer, (byte)0);
        return (count, Encoding.UTF8.GetString(buffer, 0, end < 0 ? size : end));
    }
}
