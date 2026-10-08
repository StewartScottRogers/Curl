using System.Text;
using Curl.Http2;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="Http2ResponseHead" />: the head and trailer bytes curl's HTTP/2 layer writes,
/// and which <c>:status</c> values are valid (BL-658 Notes).
/// </summary>
[TestClass]
public sealed class Http2ResponseHeadTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("200", 200, DisplayName = "200")]
    [DataRow("999", 999, DisplayName = "999")]
    [DataRow("100", 100, DisplayName = "100")]
    public void StatusOf_ThreeDigits_GivesTheCode(string status, int expected)
    {
        Diagnostics.Arrange(":status", status);

        int? code = Http2ResponseHead.StatusOf([new(":status", status)]);

        Diagnostics.Act("status code", code?.ToString() ?? "null");
        Diagnostics.Assert("status code", expected, code);
        Assert.AreEqual(expected, code);
    }

    [TestMethod]
    [DataRow("20", DisplayName = "two digits")]
    [DataRow("2000", DisplayName = "four digits")]
    [DataRow("099", DisplayName = "below 100")]
    [DataRow("2a0", DisplayName = "not digits")]
    public void StatusOf_OtherValue_GivesNull(string status)
    {
        Diagnostics.Arrange(":status", status);

        int? code = Http2ResponseHead.StatusOf([new(":status", status)]);

        Diagnostics.Act("status code", code?.ToString() ?? "null");
        Diagnostics.Assert("status code", "null", code?.ToString() ?? "null");
        Assert.IsNull(code);
    }

    [TestMethod]
    public void StatusOf_NoStatus_GivesNull()
    {
        Diagnostics.Arrange("headers", "content-type: text/plain, no :status");

        int? code = Http2ResponseHead.StatusOf([new("content-type", "text/plain")]);

        Diagnostics.Act("status code", code?.ToString() ?? "null");
        Diagnostics.Assert("status code", "null", code?.ToString() ?? "null");
        Assert.IsNull(code);
    }

    [TestMethod]
    public void Format_Block_WritesTheStatusLineWithATrailingBlankAndSkipsPseudoHeaders()
    {
        Diagnostics.Arrange("header block", ":status: 200, content-type: text/plain, x-a: b");

        byte[] head = Http2ResponseHead.Format("HTTP/2", 200, [new(":status", "200"), new("content-type", "text/plain"), new("x-a", "b")]);

        Diagnostics.Bytes("head", head);
        Diagnostics.Act("head length", head.Length);
        Diagnostics.Diff("head", "HTTP/2 200 \r\ncontent-type: text/plain\r\nx-a: b\r\n\r\n", Encoding.Latin1.GetString(head));

        Assert.AreEqual("HTTP/2 200 \r\ncontent-type: text/plain\r\nx-a: b\r\n\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void FormatTrailers_Block_WritesTheLinesWithNoEmptyLine()
    {
        Diagnostics.Arrange("trailer block", "x-checksum: abc, x-second: two");

        byte[] trailers = Http2ResponseHead.FormatTrailers([new("x-checksum", "abc"), new("x-second", "two")]);

        Diagnostics.Bytes("trailers", trailers);
        Diagnostics.Act("trailers length", trailers.Length);
        Diagnostics.Diff("trailers", "x-checksum: abc\r\nx-second: two\r\n", Encoding.Latin1.GetString(trailers));

        Assert.AreEqual("x-checksum: abc\r\nx-second: two\r\n", Encoding.Latin1.GetString(trailers));
    }
}
