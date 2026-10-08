using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpRangeHeader" /> to the <c>Range</c> values curl 8.21.0 sends for <c>-r</c>
/// and <c>-C</c>, and the <c>Content-Range</c> values it sends for <c>-r</c> with a body (measured,
/// BL-178, BL-306 and BL-386 Notes).
/// </summary>
[TestClass]
public sealed class HttpRangeHeaderTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void ValueFor_NoRangeOrResume_IsNull() =>
        Assert.IsNull(Checked(null, ValueFor(null, null, sendsBody: false)));

    [TestMethod]
    public void ValueFor_RequestWithABody_IsNull() =>
        Assert.IsNull(Checked(null, ValueFor(100, "0-99", sendsBody: true)));

    [TestMethod]
    public void ValueFor_Resume_IsFromTheOffset() =>
        Assert.AreEqual("bytes=100-", Checked("bytes=100-", ValueFor(100, "0-99", sendsBody: false)));

    [TestMethod]
    public void ValueFor_ResumeFromZero_SendsTheRange() =>
        Assert.AreEqual("bytes=0-99", Checked("bytes=0-99", ValueFor(0, "0-99", sendsBody: false)));

    [TestMethod]
    [DataRow("0-99", "bytes=0-99", DisplayName = "-r 0-99")]
    [DataRow("100-", "bytes=100-", DisplayName = "-r 100-")]
    [DataRow("-500", "bytes=-500", DisplayName = "-r -500")]
    [DataRow("0-9,20-29", "bytes=0-9,20-29", DisplayName = "-r 0-9,20-29")]
    [DataRow("1-2abc", "bytes=1-2abc", DisplayName = "-r 1-2abc")]
    [DataRow("abc", "bytes=abc", DisplayName = "-r abc")]
    [DataRow("-0", "bytes=-0", DisplayName = "-r -0")]
    public void ValueFor_RangeText_SendsItAsTyped(string rangeText, string expected) =>
        Assert.AreEqual(expected, Checked(expected, ValueFor(null, rangeText, sendsBody: false)));

    [TestMethod]
    [DataRow("0-9", 1L, "bytes 0-9/1", DisplayName = "-d x -r 0-9")]
    [DataRow("100-", 1L, "bytes 100-/1", DisplayName = "-d x -r 100-")]
    [DataRow("-500", 1L, "bytes -500/1", DisplayName = "-d x -r -500")]
    [DataRow("0-9,20-29", 1L, "bytes 0-9,20-29/1", DisplayName = "-d x -r 0-9,20-29")]
    [DataRow("abc", 1L, "bytes abc/1", DisplayName = "-d x -r abc")]
    [DataRow("0-9", null, "bytes 0-9/-1", DisplayName = "-T - -r 0-9, length unknown")]
    public void ContentRangeFor_RangeText_SendsItAsTyped(string rangeText, long? bodyLength, string expected)
    {
        Diagnostics.Arrange("range text, body length", $"{rangeText}, {(bodyLength is null ? "(unknown)" : bodyLength)}");

        string actual = HttpRangeHeader.ContentRangeFor(rangeText, bodyLength);

        Diagnostics.Act("Content-Range", actual);
        Diagnostics.Assert("Content-Range", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    private string? ValueFor(long? resumeFrom, string? rangeText, bool sendsBody)
    {
        Diagnostics.Arrange("resume from, range text, sends body", $"{(resumeFrom is null ? "(none)" : resumeFrom)}, {rangeText ?? "(none)"}, {sendsBody}");
        string? value = HttpRangeHeader.ValueFor(RangeContext(resumeFrom, rangeText), sendsBody);
        Diagnostics.Act("Range", value ?? "(null)");
        return value;
    }

    private string? Checked(string? expected, string? actual)
    {
        Diagnostics.Assert("Range", expected ?? "(null)", actual ?? "(null)");
        return actual;
    }

    private static TransferContext RangeContext(long? resumeFrom = null, string? rangeText = null) =>
        new() { Url = CurlUrl.Parse("http://127.0.0.1/f"), Output = Stream.Null, ResumeFrom = resumeFrom, RangeText = rangeText };
}
