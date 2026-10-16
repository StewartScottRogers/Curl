using System.Text;
using Libcurl.Misc.Strings;

namespace Libcurl.UnitTests.Misc.Strings;

// The deprecated curl_escape and curl_unescape forms are what some of these tests pin.
#pragma warning disable CS0618

[TestClass]
public sealed class CurlStringsTests
{
    // Upstream's answers, as curl 8.22.0's lib/escape.c gives them.
    [TestMethod]
    [DataRow("hello world", "hello%20world")]
    [DataRow("AZaz09-._~", "AZaz09-._~")]
    [DataRow("a/b?c=d&e+f", "a%2Fb%3Fc%3Dd%26e%2Bf")]
    [DataRow("%", "%25")]
    [DataRow("å", "%C3%A5")]
    [DataRow("\n\t", "%0A%09")]
    public void Escape_Text_AnswersUppercasePercentEncoding(string text, string expected)
    {
        Assert.AreEqual(expected, CurlStrings.Escape(null, text, 0));
    }

    [TestMethod]
    public void Escape_ZeroLengthInput_AnswersEmptyNotNull()
    {
        Assert.AreEqual(string.Empty, CurlStrings.Escape(null, string.Empty, 0));
        Assert.AreEqual(string.Empty, CurlStrings.Escape(ReadOnlySpan<byte>.Empty));
    }

    [TestMethod]
    public void Escape_Length_EncodesOnlyThatManyBytes()
    {
        Assert.AreEqual("a%20", CurlStrings.Escape(null, "a b c", 2));
        Assert.AreEqual("a%20b%20c", CurlStrings.Escape(null, "a b c", 5));
    }

    [TestMethod]
    [DataRow(null, 0)]
    [DataRow("abc", -1)]
    [DataRow("abc", 4)]
    public void Escape_NullTextOrBadLength_AnswersNull(string? text, int length)
    {
        Assert.IsNull(CurlStrings.Escape(null, text, length));
    }

    [TestMethod]
    public void Escape_Bytes_EncodesEveryByteButTheUnreserved()
    {
        Assert.AreEqual("%00%FFa%7F", CurlStrings.Escape([0x00, 0xFF, (byte)'a', 0x7F]));
    }

    [TestMethod]
    public void Escape_DeprecatedForm_AnswersAsTheHandleForm()
    {
        Assert.AreEqual("a%2Fb", CurlStrings.Escape("a/b", 0));
        Assert.IsNull(CurlStrings.Escape(null, -1));
    }

    [TestMethod]
    [DataRow("hello%20world", "hello world")]
    [DataRow("%2f%2F", "//")]
    [DataRow("a+b", "a+b")]
    [DataRow("100%", "100%")]
    [DataRow("%4", "%4")]
    [DataRow("%zz%4g", "%zz%4g")]
    [DataRow("%C3%A5", "å")]
    public void Unescape_Text_DecodesEachPercentAndTwoHexDigits(string text, string expected)
    {
        byte[]? decoded = CurlStrings.Unescape(null, text, 0, out int outputLength);

        Assert.IsNotNull(decoded);
        Assert.AreEqual(expected, Encoding.UTF8.GetString(decoded));
        Assert.AreEqual(decoded.Length, outputLength);
    }

    [TestMethod]
    public void Unescape_PercentZeroZero_DecodesANulByteAndCountsIt()
    {
        byte[]? decoded = CurlStrings.Unescape(null, "a%00b", 0, out int outputLength);

        CollectionAssert.AreEqual(new byte[] { (byte)'a', 0x00, (byte)'b' }, decoded);
        Assert.AreEqual(3, outputLength);
    }

    [TestMethod]
    public void Unescape_ZeroLengthInput_AnswersEmptyNotNull()
    {
        byte[]? decoded = CurlStrings.Unescape(null, string.Empty, 0, out int outputLength);

        Assert.IsNotNull(decoded);
        Assert.IsEmpty(decoded);
        Assert.AreEqual(0, outputLength);
        Assert.IsEmpty(CurlStrings.Unescape(ReadOnlySpan<byte>.Empty));
    }

    [TestMethod]
    public void Unescape_Length_DecodesOnlyThatManyBytesSoACutEscapeStaysLiteral()
    {
        byte[]? decoded = CurlStrings.Unescape(null, "a%41b", 3, out int outputLength);

        CollectionAssert.AreEqual("a%4"u8.ToArray(), decoded);
        Assert.AreEqual(3, outputLength);
    }

    [TestMethod]
    [DataRow(null, 0)]
    [DataRow("abc", -1)]
    [DataRow("abc", 4)]
    public void Unescape_NullTextOrBadLength_AnswersNullAndZeroLength(string? text, int length)
    {
        Assert.IsNull(CurlStrings.Unescape(null, text, length, out int outputLength));
        Assert.AreEqual(0, outputLength);
    }

    [TestMethod]
    public void Unescape_Bytes_DecodesHighBytes()
    {
        CollectionAssert.AreEqual(new byte[] { 0xFF, 0x80 }, CurlStrings.Unescape("%ff%80"u8));
    }

    [TestMethod]
    public void Unescape_DeprecatedForm_AnswersAsTheHandleForm()
    {
        CollectionAssert.AreEqual("a b"u8.ToArray(), CurlStrings.Unescape("a%20b", 0));
        Assert.IsNull(CurlStrings.Unescape(null, 0));
    }

    [TestMethod]
    public void EscapeThenUnescape_RoundTripsEveryByte()
    {
        byte[] everyByte = [.. Enumerable.Range(0, 256).Select(value => (byte)value)];

        CollectionAssert.AreEqual(everyByte, CurlStrings.Unescape(Encoding.ASCII.GetBytes(CurlStrings.Escape(everyByte))));
    }

    [TestMethod]
    public void GetEnv_SetValue_AnswersIt()
    {
        Assert.AreEqual("value", CurlStrings.GetEnv("NAME", name => name == "NAME" ? "value" : null));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void GetEnv_UnsetOrEmpty_AnswersNull(string? value)
    {
        Assert.IsNull(CurlStrings.GetEnv("NAME", _ => value));
    }

    [TestMethod]
    public void GetEnv_ProcessEnvironment_AnswersNullForAnUnsetVariable()
    {
        Assert.IsNull(CurlStrings.GetEnv("LIBCURL_UNIT_TESTS_UNSET_" + Guid.NewGuid().ToString("N")));
    }

    [TestMethod]
    public void GetEnv_NullName_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CurlStrings.GetEnv(null!));
    }

    // curl_strequal's answers, as lib/strequal.c gives them: ASCII case only.
    [TestMethod]
    [DataRow("iii", "III", true)]
    [DataRow("iiia", "III", false)]
    [DataRow("iii", "IIIa", false)]
    [DataRow("iiiA", "IIIa", true)]
    [DataRow("", "", true)]
    [DataRow("", "a", false)]
    [DataRow("a\0b", "A\0c", true)]
    [DataRow("@", "`", false)]
    [DataRow("å", "Å", false)]
    [DataRow(null, null, true)]
    [DataRow(null, "a", false)]
    [DataRow("a", null, false)]
    public void StrEqual_AnswersAsCurlStrequal(string? first, string? second, bool expected)
    {
        Assert.AreEqual(expected, CurlStrings.StrEqual(first, second));
    }

    [TestMethod]
    [DataRow("iii", "III", 3u, true)]
    [DataRow("iiiA", "IIIa", 3u, true)]
    [DataRow("iiiA", "IIIb", 3u, true)]
    [DataRow("iiiA", "IIIb", 4u, false)]
    [DataRow("iii", "IIIa", 4u, false)]
    [DataRow("iii", "III", 10u, true)]
    [DataRow("abc", "xyz", 0u, true)]
    [DataRow("", "", 5u, true)]
    [DataRow(null, null, 1u, true)]
    [DataRow(null, null, 0u, false)]
    [DataRow(null, "a", 1u, false)]
    [DataRow("a", null, 1u, false)]
    public void StrNEqual_AnswersAsCurlStrnequal(string? first, string? second, uint maximum, bool expected)
    {
        Assert.AreEqual(expected, CurlStrings.StrNEqual(first, second, maximum));
    }
}
