using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="CurlUrl.RequestPath" /> for CGR-016 part 3 (BL-3851, ADR-0921): on Linux an argument
/// byte that is not valid UTF-8 reaches the URL as the lone surrogate <c>U+DC00</c> plus the byte, and
/// curl 8.18.0 sends <c>http://h/a</c> and the byte <c>0xFF</c> as <c>GET /a%FF</c>, measured in WSL on
/// 2026-10-26; valid UTF-8 is still percent-encoded as its UTF-8.
/// </summary>
[TestClass]
public sealed class Cgr016UrlTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // A lone surrogate does not survive an attribute's UTF-8 blob, so each row names the escaped
    // bytes with '^' and a pair of hex digits, which the test turns into U+DC00 plus the byte.
    [TestMethod]
    [DataRow("/a^FF", "/a%FF", DisplayName = "invalid UTF-8 byte 0xFF")]
    [DataRow("/^80x^C3?q", "/%80x%C3", DisplayName = "invalid UTF-8 bytes 0x80 and 0xC3")]
    [DataRow("/é~", "/%C3%A9~", DisplayName = "valid UTF-8")]
    [DataRow("/a%ff", "/a%ff", DisplayName = "escape written in the URL")]
    public void RequestPath_PercentEncodesEachByteAboveTilde(string written, string expected)
    {
        string text = "http://127.0.0.1:20001" + WithEscapedBytes(written);
        Diagnostics.Arrange("url", text);

        string path = CurlUrl.Parse(text).RequestPath;

        Diagnostics.Act("request path", path);
        Diagnostics.Assert("request path", expected, path);
        Assert.AreEqual(expected, path);
    }

    [TestMethod]
    public void TryParse_HostHoldingAnInvalidUtf8Byte_IsRefused()
    {
        string text = WithEscapedBytes("http://a^FF.example/");
        Diagnostics.Arrange("url", text);

        bool parsed = CurlUrl.TryParse(text, pathAsIs: false, out CurlUrl? url);

        Diagnostics.Act("parsed", parsed);
        Diagnostics.Assert("parsed", false, parsed);
        Assert.IsFalse(parsed);
        Assert.IsNull(url);
    }

    [TestMethod]
    public void UrlByteText_ToBytes_QueryHoldingAnInvalidUtf8Byte_IsTheByte()
    {
        string query = CurlUrl.Parse(WithEscapedBytes("http://h/?q^FF")).Query!;
        Diagnostics.Arrange("query", query);

        byte[] bytes = UrlByteText.ToBytes(query);

        Diagnostics.Act("bytes", Convert.ToHexString(bytes));
        Diagnostics.Assert("bytes", "71FF", Convert.ToHexString(bytes));
        Assert.AreEqual("71FF", Convert.ToHexString(bytes));
    }

    private static string WithEscapedBytes(string written)
    {
        string[] pieces = written.Split('^');
        System.Text.StringBuilder text = new(pieces[0]);
        foreach (string piece in pieces.Skip(1))
        {
            text.Append((char)(0xDC00 + Convert.ToInt32(piece[..2], 16))).Append(piece[2..]);
        }

        return text.ToString();
    }
}
