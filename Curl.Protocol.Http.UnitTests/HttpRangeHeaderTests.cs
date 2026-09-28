using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRangeHeader" /> to the <c>Range</c> values curl 8.21.0 sends for <c>-r</c>
/// and <c>-C</c>, and the <c>Content-Range</c> values it sends for <c>-r</c> with a body (measured,
/// BL-178, BL-306 and BL-386 Notes).
/// </summary>
[TestClass]
public sealed class HttpRangeHeaderTests
{
    [TestMethod]
    public void ValueFor_NoRangeOrResume_IsNull() =>
        Assert.IsNull(HttpRangeHeader.ValueFor(RangeContext(), sendsBody: false));

    [TestMethod]
    public void ValueFor_RequestWithABody_IsNull() =>
        Assert.IsNull(HttpRangeHeader.ValueFor(RangeContext(resumeFrom: 100, rangeText: "0-99"), sendsBody: true));

    [TestMethod]
    public void ValueFor_Resume_IsFromTheOffset() =>
        Assert.AreEqual("bytes=100-", HttpRangeHeader.ValueFor(RangeContext(resumeFrom: 100, rangeText: "0-99"), sendsBody: false));

    [TestMethod]
    public void ValueFor_ResumeFromZero_SendsTheRange() =>
        Assert.AreEqual("bytes=0-99", HttpRangeHeader.ValueFor(RangeContext(resumeFrom: 0, rangeText: "0-99"), sendsBody: false));

    [TestMethod]
    [DataRow("0-99", "bytes=0-99", DisplayName = "-r 0-99")]
    [DataRow("100-", "bytes=100-", DisplayName = "-r 100-")]
    [DataRow("-500", "bytes=-500", DisplayName = "-r -500")]
    [DataRow("0-9,20-29", "bytes=0-9,20-29", DisplayName = "-r 0-9,20-29")]
    [DataRow("1-2abc", "bytes=1-2abc", DisplayName = "-r 1-2abc")]
    [DataRow("abc", "bytes=abc", DisplayName = "-r abc")]
    [DataRow("-0", "bytes=-0", DisplayName = "-r -0")]
    public void ValueFor_RangeText_SendsItAsTyped(string rangeText, string expected) =>
        Assert.AreEqual(expected, HttpRangeHeader.ValueFor(RangeContext(rangeText: rangeText), sendsBody: false));

    [TestMethod]
    [DataRow("0-9", 1L, "bytes 0-9/1", DisplayName = "-d x -r 0-9")]
    [DataRow("100-", 1L, "bytes 100-/1", DisplayName = "-d x -r 100-")]
    [DataRow("-500", 1L, "bytes -500/1", DisplayName = "-d x -r -500")]
    [DataRow("0-9,20-29", 1L, "bytes 0-9,20-29/1", DisplayName = "-d x -r 0-9,20-29")]
    [DataRow("abc", 1L, "bytes abc/1", DisplayName = "-d x -r abc")]
    [DataRow("0-9", null, "bytes 0-9/-1", DisplayName = "-T - -r 0-9, length unknown")]
    public void ContentRangeFor_RangeText_SendsItAsTyped(string rangeText, long? bodyLength, string expected) =>
        Assert.AreEqual(expected, HttpRangeHeader.ContentRangeFor(rangeText, bodyLength));

    private static TransferContext RangeContext(long? resumeFrom = null, string? rangeText = null) =>
        new() { Url = CurlUrl.Parse("http://127.0.0.1/f"), Output = Stream.Null, ResumeFrom = resumeFrom, RangeText = rangeText };
}
