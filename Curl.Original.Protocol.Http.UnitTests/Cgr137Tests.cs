using System.Text;
using Curl.Http2;
using Curl.Testing;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-137 on <see cref="Http2ResponseHead" />: a response header field whose name is
/// empty or holds a character that is not a token character (a space or a colon) is
/// malformed, and curl's nghttp2 build ignores it without a message, so it is neither printed
/// nor counted in <c>%{size_header}</c> and <c>%{num_headers}</c> (measured with curl 8.18.0,
/// nghttp2 1.68.0).
/// </summary>
[TestClass]
public sealed class Cgr137Tests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("x bad", DisplayName = "a space")]
    [DataRow("x:y", DisplayName = "a colon")]
    [DataRow("", DisplayName = "empty")]
    public void Format_InvalidName_LeavesTheFieldOut(string name)
    {
        Diagnostics.Arrange("name", name);

        byte[] head = Http2ResponseHead.Format("HTTP/2", 200, [new(":status", "200"), new("content-length", "5"), new(name, "v"), new("x-good", "1")]);

        string text = Encoding.Latin1.GetString(head);
        Diagnostics.Act("head", text);
        Diagnostics.Assert("head", "HTTP/2 200 \\r\\ncontent-length: 5\\r\\nx-good: 1\\r\\n\\r\\n", text);
        Assert.AreEqual("HTTP/2 200 \r\ncontent-length: 5\r\nx-good: 1\r\n\r\n", text);
    }

    [TestMethod]
    public void Format_TokenName_KeepsTheField()
    {
        Diagnostics.Arrange("name", "x-ok!#$%&'*+.^_`|~09");

        byte[] head = Http2ResponseHead.Format("HTTP/2", 200, [new(":status", "200"), new("x-ok!#$%&'*+.^_`|~09", "v")]);

        string text = Encoding.Latin1.GetString(head);
        Diagnostics.Act("head", text);
        Diagnostics.Assert("head", "HTTP/2 200 \\r\\nx-ok!#$%&'*+.^_`|~09: v\\r\\n\\r\\n", text);
        Assert.AreEqual("HTTP/2 200 \r\nx-ok!#$%&'*+.^_`|~09: v\r\n\r\n", text);
    }

    [TestMethod]
    public void FormatTrailers_NameWithSpace_LeavesTheFieldOut()
    {
        Diagnostics.Arrange("trailers", "x bad: v, x-good: c");

        byte[] trailers = Http2ResponseHead.FormatTrailers([new("x bad", "v"), new("x-good", "c")]);

        string text = Encoding.Latin1.GetString(trailers);
        Diagnostics.Act("trailers", text);
        Diagnostics.Assert("trailers", "x-good: c\\r\\n", text);
        Assert.AreEqual("x-good: c\r\n", text);
    }
}
