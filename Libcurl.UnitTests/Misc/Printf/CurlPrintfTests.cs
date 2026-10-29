using System.Runtime.CompilerServices;
using System.Text;
using Libcurl.Misc.Printf;

namespace Libcurl.UnitTests.Misc.Printf;

// Each conversion family of curl 8.22.0's lib/mprintf.c, pinned on both platforms' C type widths.
[TestClass]
public sealed partial class CurlPrintfTests
{
    private static readonly PrintfPlatform Windows = new(IsWindows: true);
    private static readonly PrintfPlatform Posix = new(IsWindows: false);

    [TestMethod]
    [DataRow("%d", int.MinValue, "-2147483648")]
    [DataRow("%i", 42, "42")]
    [DataRow("%4d", 1, "   1")]
    [DataRow("%u", -1, "4294967295")]
    [DataRow("%d", 4294967297L, "1")]
    [DataRow("%hd", 65535, "65535")]
    [DataRow("%+d", 5, "+5")]
    [DataRow("% d", 5, " 5")]
    [DataRow("%+u", 1234, "+1234")]
    [DataRow("%05d", -42, "-0042")]
    [DataRow("%-05d|", 42, "42   |")]
    [DataRow("%.0d", 0, "")]
    [DataRow("%.5d", 42, "00042")]
    [DataRow("%8.5d", -42, "  -00042")]
    [DataRow("%#o", 8, "010")]
    [DataRow("%#.5o", 8, "00010")]
    [DataRow("%#x", 255, "0xff")]
    [DataRow("%#X", 255, "0XFF")]
    [DataRow("%#08x", 255, "0x0000ff")]
    [DataRow("%3c|%-3c", 'a', "  a|a  ")]
    public void Render_Integer_FormatsAsMprintf(string format, object value, string expected)
    {
        object[] arguments = format.Contains("%-3c", StringComparison.Ordinal) ? [value, value] : [value];

        Assert.AreEqual(expected, Render(Posix, format, arguments));
    }

    [TestMethod]
    [DataRow("%lld", long.MinValue, "-9223372036854775808")]
    [DataRow("%llu", -1L, "18446744073709551615")]
    [DataRow("%zd", 1L << 40, "1099511627776")]
    [DataRow("%Od", 1L << 40, "1099511627776")]
    [DataRow("%qd", 1L << 40, "1099511627776")]
    [DataRow("%llx", ulong.MaxValue, "ffffffffffffffff")]
    public void Render_LongLong_Is64BitsOnEveryPlatform(string format, object value, string expected)
    {
        Assert.AreEqual(expected, Render(Windows, format, value));
        Assert.AreEqual(expected, Render(Posix, format, value));
    }

    [TestMethod]
    [DataRow("%ld", 4294967297L, "1", "4294967297")]
    [DataRow("%lu", -1L, "4294967295", "18446744073709551615")]
    [DataRow("%lo %lx %lX %+lo", 0xFABCDEF123456780UL, "", "")]
    public void Render_Long_Is32BitsOnWindowsAnd64Elsewhere(string format, object value, string windows, string posix)
    {
        if (format.StartsWith("%lo", StringComparison.Ordinal))
        {
            // lib557's test_oct_hex_formatting, for each SIZEOF_LONG.
            object[] windowsArguments = [0xFABC1230UL, 0xFABC1230UL, 0xFABC1230UL, 1234UL];
            object[] posixArguments = [value, value, value, 1234UL];
            Assert.AreEqual("37257011060 fabc1230 FABC1230 +2322", Render(Windows, format, windowsArguments));
            Assert.AreEqual("1752746757044321263600 fabcdef123456780 FABCDEF123456780 +2322", Render(Posix, format, posixArguments));
            return;
        }

        Assert.AreEqual(windows, Render(Windows, format, value));
        Assert.AreEqual(posix, Render(Posix, format, value));
    }

    [TestMethod]
    [DataRow("%I64d", "4294967301")]
    [DataRow("%I32d", "5")]
    [DataRow("%Id", "4294967301")]
    public void Render_WindowsLengthModifier_IsUnderstoodOnlyOnWindows(string format, string windows)
    {
        Assert.AreEqual(windows, Render(Windows, format, 4294967301L));
        Assert.AreEqual(format, Render(Posix, format, 4294967301L));
    }

    [TestMethod]
    public void Render_IntegerArgumentTypes_AreReadAsTheirBits()
    {
        Assert.AreEqual("255 65 7 8 -1", Render(Posix, "%u %d %d %d %lld", (byte)255, 'A', (nint)7, (nuint)8, ulong.MaxValue));
    }

    [TestMethod]
    [DataRow("%S", "x", "\"x\"")]
    [DataRow("%5S", "x", "\"    x\"")]
    [DataRow("%-3s|", "x", "x  |")]
    [DataRow("%10.5s", "foo", "     foo")]
    [DataRow("%.-2s", "abc", "abc")]
    [DataRow("%s", "å", "å")]
    public void Render_String_FormatsAsOutString(string format, string value, string expected)
    {
        Assert.AreEqual(expected, Render(Posix, format, value));
    }

    [TestMethod]
    [DataRow("%s", "(nil)")]
    [DataRow("%S", "(nil)")]
    [DataRow("%.5s", "(nil)")]
    [DataRow("%.4s|", "|")]
    [DataRow("%.-2s|", "|")]
    public void Render_NullString_IsNilWhenThePrecisionAllows(string format, string expected)
    {
        Assert.AreEqual(expected, Render(Posix, format, [null]));
    }

    [TestMethod]
    public void Render_ByteArrayString_StopsAtItsFirstNul()
    {
        Assert.AreEqual("ab|cd", Render(Posix, "%s|%s", new byte[] { (byte)'a', (byte)'b', 0, (byte)'c' }, "cd"u8.ToArray()));
    }

    [TestMethod]
    [DataRow("%p", null, "(nil)")]
    [DataRow("%7p|", null, "(nil)  |")]
    [DataRow("%-7p|", null, "  (nil)|")]
    [DataRow("%p", 0x1234, "0x1234")]
    public void Render_Pointer_FormatsAsOutPointer(string format, object? value, string expected)
    {
        Assert.AreEqual(expected, Render(Posix, format, [value]));
    }

    [TestMethod]
    [DataRow("%e", 12345.678, "1.234568e+04")]
    [DataRow("%E", 12345.678, "1.234568E+04")]
    [DataRow("%.0e", 5.0, "5e+00")]
    [DataRow("%#.0e", 5.0, "5.e+00")]
    [DataRow("%e", 0.0, "0.000000e+00")]
    [DataRow("%e", 5e-324, "4.940656e-324")]
    [DataRow("%e", 9.9999999, "1.000000e+01")]
    [DataRow("%g", 0.0001, "0.0001")]
    [DataRow("%g", 0.00001, "1e-05")]
    [DataRow("%g", 100000.0, "100000")]
    [DataRow("%g", 1e6, "1e+06")]
    [DataRow("%g", 0.0, "0")]
    [DataRow("%.0g", 25.0, "2e+01")]
    [DataRow("%.3g", 9999.5, "1e+04")]
    [DataRow("%#g", 1.0, "1.00000")]
    [DataRow("%G", 1e-10, "1E-10")]
    [DataRow("%#.0f", 3.0, "3.")]
    [DataRow("%.0f", 0.5, "0")]
    [DataRow("%.0f", 1.5, "2")]
    [DataRow("%.0f", 2.5, "2")]
    [DataRow("%.0f", 0.6, "1")]
    [DataRow("%.2f", 1.005, "1.00")]
    [DataRow("%.1f", 0.04, "0.0")]
    [DataRow("%.1f", 0.001, "0.0")]
    [DataRow("%.1f", 9.96, "10.0")]
    [DataRow("%f", 0.0, "0.000000")]
    [DataRow("%f", -0.0, "-0.000000")]
    [DataRow("%+f", 1.0, "+1.000000")]
    [DataRow("% f", 1.0, " 1.000000")]
    [DataRow("%010f", 9.1, "  9.100000")]
    [DataRow("%lf %Lf", 1.0, "1.000000 1.000000")]
    [DataRow("%f", double.PositiveInfinity, "inf")]
    [DataRow("%F", double.NegativeInfinity, "-INF")]
    [DataRow("%f", double.NaN, "nan")]
    [DataRow("%E", double.NaN, "NAN")]
    public void Render_Double_FormatsAsCPrintf(string format, double value, string expected)
    {
        object[] arguments = format.Contains(' ', StringComparison.Ordinal) && format != "% f" ? [value, value] : [value];

        Assert.AreEqual(expected, Render(Posix, format, arguments));
    }

    [TestMethod]
    public void Render_Float_IsReadAsADouble()
    {
        Assert.AreEqual("1.500000", Render(Posix, "%f", 1.5f));
    }

    [TestMethod]
    public void Render_PositionalWidthAndPrecision_ReadTheirArguments()
    {
        Assert.AreEqual("  5", Render(Posix, "%1$*2$d", 5, 3));
        Assert.AreEqual("005", Render(Posix, "%2$.*1$d", 3, 5));
        Assert.AreEqual("42   |", Render(Posix, "%*d|", -5, 42));
        Assert.AreEqual("42", Render(Posix, "%.*d", -3, 42));
        Assert.AreEqual("a%b", Render(Posix, "%1$s%%%2$s", "a", "b"));
    }

    [TestMethod]
    [DataRow("%1$*d")]
    [DataRow("%1$.*d")]
    [DataRow("%1$d%2$*1$d")]
    [DataRow("%1$d%2$.*1$d")]
    [DataRow("%.*.2d")]
    [DataRow("%.d")]
    [DataRow("%99999999999d")]
    [DataRow("%.99999999999d")]
    [DataRow("%2$d")]
    public void Render_FormatMprintfRefuses_PrintsNothing(string format)
    {
        Assert.AreEqual(string.Empty, Render(Posix, format, 1, 2, 3));
    }

    [TestMethod]
    [DataRow("abc%", "abc%")]
    [DataRow("%200$d", "%200$d")]
    [DataRow("%0$d", "%0$d")]
    public void Render_UnknownConversion_StaysAsText(string format, string expected)
    {
        Assert.AreEqual(expected, Render(Posix, format, 1));
    }

    [TestMethod]
    public void Render_MoreThanMaxSegments_PrintsNothing()
    {
        string conversions = string.Concat(Enumerable.Repeat("%1$d", 128));

        Assert.AreEqual(new string('1', 128), Render(Posix, conversions, 1));
        Assert.AreEqual(string.Empty, Render(Posix, conversions + "%1$d", 1));
        Assert.AreEqual(string.Empty, Render(Posix, conversions + "x", 1));
        Assert.AreEqual(string.Empty, Render(Posix, conversions + "x%%", 1));
    }

    [TestMethod]
    public void Render_Count_StoresTheBytesWrittenSoFar()
    {
        var count = new StrongBox<int>();
        var longCount = new StrongBox<long>();
        var shortCount = new StrongBox<short>();

        string text = Render(Posix, "ab%ncd%lln%65537d%hn", count, longCount, 1, shortCount);

        Assert.AreEqual(2, count.Value);
        Assert.AreEqual(4L, longCount.Value);
        Assert.AreEqual((short)5, shortCount.Value);
        Assert.AreEqual(65541, text.Length);
    }

    [TestMethod]
    [DataRow("%n", "x")]
    [DataRow("%d", "x")]
    [DataRow("%d", null)]
    [DataRow("%d", true)]
    [DataRow("%d", 1.5)]
    [DataRow("%f", 1)]
    [DataRow("%s", 1)]
    public void Render_ArgumentOfTheWrongType_Throws(string format, object? value)
    {
        Assert.ThrowsExactly<ArgumentException>(() => Render(Posix, format, [value]));
    }

    [TestMethod]
    public void Render_TooFewArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => Render(Posix, "%d %d", 1));
    }

    [TestMethod]
    public void Render_NulInFormat_EndsIt()
    {
        Assert.AreEqual("ab", Render(Posix, "ab\0%d"));
    }

    [TestMethod]
    public void MSNPrintf_TruncatedMidConversion_StopsWriting()
    {
        var buffer = new byte[3];

        Assert.AreEqual(2, CurlPrintf.MSNPrintf(buffer, "%S", "abc"));
        Assert.AreEqual(2, CurlPrintf.MSNPrintf(buffer, "%300d", 1));
        Assert.AreEqual(0, buffer[2]);
    }

    [TestMethod]
    public void MSNPrintf_EmptyBuffer_WritesNothing()
    {
        Assert.AreEqual(0, CurlPrintf.MSNPrintf(Span<byte>.Empty, "abc"));
    }

    [TestMethod]
    public void MSPrintf_Text_IsNulTerminated()
    {
        var buffer = new byte[] { 9, 9, 9, 9, 9 };

        int count = CurlPrintf.MSPrintf(buffer, "%d", 123);

        Assert.AreEqual(3, count);
        CollectionAssert.AreEqual(new byte[] { (byte)'1', (byte)'2', (byte)'3', 0, 9 }, buffer);
    }

    [TestMethod]
    public void MSPrintf_BufferTooSmall_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CurlPrintf.MSPrintf(new byte[3], "%d", 123));
    }

    [TestMethod]
    public void MAPrintf_TextReachingTheLimit_AnswersNull()
    {
        Assert.IsNull(CurlPrintf.MAPrintf("%8000000d", 1));
        Assert.AreEqual(7_999_999, CurlPrintf.MAPrintf("%7999999d", 1)!.Length);
    }

    [TestMethod]
    public void MAPrintf_RefusedFormat_AnswersEmpty()
    {
        Assert.AreEqual(string.Empty, CurlPrintf.MAPrintf("%2$d", 1, 2));
    }

    [TestMethod]
    public void MFPrintf_NullStream_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPrintf.MFPrintf(null!, "x"));
    }

    [TestMethod]
    public void Render_NullFormatOrArguments_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPrintf.MAPrintf(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlPrintf.MAPrintf("x", null!));
    }

    private static string Render(PrintfPlatform platform, string format, params object?[] arguments)
    {
        var output = new PrintfMemoryOutput(int.MaxValue);
        CurlPrintf.Render(platform, output, format, arguments);
        return Encoding.UTF8.GetString(output.Written);
    }
}
