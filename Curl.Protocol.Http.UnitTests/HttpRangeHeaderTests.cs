using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRangeHeader" /> to the <c>Range</c> values curl 8.21.0 sends for <c>-r</c>
/// and <c>-C</c> (measured, BL-178 Notes).
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

    private static TransferContext RangeContext(long? resumeFrom = null, ByteRange? range = null) =>
        new() { Url = CurlUrl.Parse("http://127.0.0.1/f"), Output = Stream.Null, ResumeFrom = resumeFrom, Range = range };
}
