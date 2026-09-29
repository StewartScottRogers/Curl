using System.Text;
using Curl.Http2;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="Http2ResponseHead" />: the head and trailer bytes curl's HTTP/2 layer writes,
/// and which <c>:status</c> values are valid (BL-658 Notes).
/// </summary>
[TestClass]
public sealed class Http2ResponseHeadTests
{
    [TestMethod]
    [DataRow("200", 200, DisplayName = "200")]
    [DataRow("999", 999, DisplayName = "999")]
    [DataRow("100", 100, DisplayName = "100")]
    public void StatusOf_ThreeDigits_GivesTheCode(string status, int expected) =>
        Assert.AreEqual(expected, Http2ResponseHead.StatusOf([new(":status", status)]));

    [TestMethod]
    [DataRow("20", DisplayName = "two digits")]
    [DataRow("2000", DisplayName = "four digits")]
    [DataRow("099", DisplayName = "below 100")]
    [DataRow("2a0", DisplayName = "not digits")]
    public void StatusOf_OtherValue_GivesNull(string status) =>
        Assert.IsNull(Http2ResponseHead.StatusOf([new(":status", status)]));

    [TestMethod]
    public void StatusOf_NoStatus_GivesNull() =>
        Assert.IsNull(Http2ResponseHead.StatusOf([new("content-type", "text/plain")]));

    [TestMethod]
    public void Format_Block_WritesTheStatusLineWithATrailingBlankAndSkipsPseudoHeaders()
    {
        byte[] head = Http2ResponseHead.Format("HTTP/2", 200, [new(":status", "200"), new("content-type", "text/plain"), new("x-a", "b")]);

        Assert.AreEqual("HTTP/2 200 \r\ncontent-type: text/plain\r\nx-a: b\r\n\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void FormatTrailers_Block_WritesTheLinesWithNoEmptyLine()
    {
        byte[] trailers = Http2ResponseHead.FormatTrailers([new("x-checksum", "abc"), new("x-second", "two")]);

        Assert.AreEqual("x-checksum: abc\r\nx-second: two\r\n", Encoding.Latin1.GetString(trailers));
    }
}
