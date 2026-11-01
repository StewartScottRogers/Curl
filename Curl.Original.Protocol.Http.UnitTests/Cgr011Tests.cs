using System.Text;
using Curl.Http2;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-011 on <see cref="Http2ResponseHead" />: a response header field whose value holds
/// a CR, LF or NUL is malformed (RFC 9113 section 8.2.1), and curl's nghttp2 build ignores it
/// without a message, so no injected line reaches the HTTP/1 header parser, <c>-D</c> or the
/// cookie jar.
/// </summary>
[TestClass]
public sealed class Cgr011Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("a\r\nset-cookie: evil=1; Path=/", DisplayName = "CRLF and an injected Set-Cookie")]
    [DataRow("a\r\nb", DisplayName = "CRLF")]
    [DataRow("a\nb", DisplayName = "LF")]
    [DataRow("a\rb", DisplayName = "CR")]
    [DataRow("a\0b", DisplayName = "NUL")]
    public void Format_ValueWithCrLfOrNul_LeavesTheFieldOut(string value)
    {
        Diagnostics.Arrange("x-inj", value);

        byte[] head = Http2ResponseHead.Format("HTTP/2", 200, [new(":status", "200"), new("x-inj", value), new("content-length", "3")]);

        string text = Encoding.Latin1.GetString(head);
        Diagnostics.Act("head", text);
        Diagnostics.Assert("head", "HTTP/2 200 \\r\\ncontent-length: 3\\r\\n\\r\\n", text);
        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 3\r\n\r\n", text);
    }

    [TestMethod]
    public void Format_LocationWithCrLf_LeavesTheLocationOut()
    {
        Diagnostics.Arrange("location", "/next\\r\\nset-cookie: evil=1");

        byte[] head = Http2ResponseHead.Format("HTTP/2", 302, [new(":status", "302"), new("location", "/next\r\nset-cookie: evil=1")]);

        string text = Encoding.Latin1.GetString(head);
        Diagnostics.Act("head", text);
        Diagnostics.Assert("head", "HTTP/2 302 \\r\\n\\r\\n", text);
        Assert.AreEqual("HTTP/2 302 \r\n\r\n", text);
    }

    [TestMethod]
    public void FormatTrailers_ValueWithLf_LeavesTheFieldOut()
    {
        Diagnostics.Arrange("trailers", "x-bad: a\\nb, x-good: c");

        byte[] trailers = Http2ResponseHead.FormatTrailers([new("x-bad", "a\nb"), new("x-good", "c")]);

        string text = Encoding.Latin1.GetString(trailers);
        Diagnostics.Act("trailers", text);
        Diagnostics.Assert("trailers", "x-good: c\\r\\n", text);
        Assert.AreEqual("x-good: c\r\n", text);
    }
}
