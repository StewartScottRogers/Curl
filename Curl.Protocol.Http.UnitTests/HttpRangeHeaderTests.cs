using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRangeHeader" /> to the <c>Range</c> values curl 8.21.0 sends for <c>-r</c>
/// and <c>-C</c>, and the <c>Content-Range</c> values it sends for <c>-r</c> with a body (measured,
/// BL-178 and BL-306 Notes).
/// </summary>
[TestClass]
public sealed class HttpRangeHeaderTests
{
    [TestMethod]
    public void ValueFor_NoRangeOrResume_IsNull() =>
        Assert.IsNull(HttpRangeHeader.ValueFor(RangeContext(), sendsBody: false));

    [TestMethod]
    public void ValueFor_RequestWithABody_IsNull() =>
        Assert.IsNull(HttpRangeHeader.ValueFor(RangeContext(resumeFrom: 100, range: ByteRange.Bounded(0, 99)), sendsBody: true));

    [TestMethod]
    public void ValueFor_Resume_IsFromTheOffset() =>
        Assert.AreEqual("bytes=100-", HttpRangeHeader.ValueFor(RangeContext(resumeFrom: 100, range: ByteRange.Bounded(0, 99)), sendsBody: false));

    [TestMethod]
    public void ValueFor_ResumeFromZero_SendsTheRange() =>
        Assert.AreEqual("bytes=0-99", HttpRangeHeader.ValueFor(RangeContext(resumeFrom: 0, range: ByteRange.Bounded(0, 99)), sendsBody: false));

    [TestMethod]
    public void ValueFor_EachRangeForm_IsCurlsForm()
    {
        Assert.AreEqual("bytes=0-99", HttpRangeHeader.ValueFor(RangeContext(range: ByteRange.Bounded(0, 99)), sendsBody: false));
        Assert.AreEqual("bytes=100-", HttpRangeHeader.ValueFor(RangeContext(range: ByteRange.FromOffset(100)), sendsBody: false));
        Assert.AreEqual("bytes=-500", HttpRangeHeader.ValueFor(RangeContext(range: ByteRange.Suffix(500)), sendsBody: false));
    }

    [TestMethod]
    [DataRow("0-9", 1L, "bytes 0-9/1", DisplayName = "-d x -r 0-9")]
    [DataRow("100-", 1L, "bytes 100-/1", DisplayName = "-d x -r 100-")]
    [DataRow("-500", 1L, "bytes -500/1", DisplayName = "-d x -r -500")]
    [DataRow("0-9", null, "bytes 0-9/-1", DisplayName = "-T - -r 0-9, length unknown")]
    public void ContentRangeFor_EachRangeForm_IsCurlsForm(string form, long? bodyLength, string expected)
    {
        ByteRange range = form switch
        {
            "0-9" => ByteRange.Bounded(0, 9),
            "100-" => ByteRange.FromOffset(100),
            _ => ByteRange.Suffix(500),
        };

        Assert.AreEqual(expected, HttpRangeHeader.ContentRangeFor(range, bodyLength));
    }

    private static TransferContext RangeContext(long? resumeFrom = null, ByteRange? range = null) =>
        new() { Url = CurlUrl.Parse("http://127.0.0.1/f"), Output = Stream.Null, ResumeFrom = resumeFrom, Range = range };
}
