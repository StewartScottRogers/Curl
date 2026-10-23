using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Core;

[TestClass]
public sealed class DifferentialHttpResponseWriterTests
{
    [TestMethod]
    public void Format_KnownStatus_WritesReasonDateHeadersAndContentLength()
    {
        var bytes = DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(404, [("A", "1"), ("B", "é")], "hi"u8.ToArray()));

        Assert.AreEqual(
            "HTTP/1.1 404 Not Found\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nA: 1\r\nB: Ã©\r\nContent-Length: 2\r\n\r\nhi",
            Encoding.Latin1.GetString(bytes));
    }

    [TestMethod]
    public void Format_UnknownStatusVersionAndClose_UsesStatusReasonAndAddsConnectionClose()
    {
        var bytes = DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(299, [], [], "HTTP/1.0", AddsConnectionClose: true));

        Assert.AreEqual(
            "HTTP/1.0 299 Status\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nContent-Length: 0\r\nConnection: close\r\n\r\n",
            Encoding.Latin1.GetString(bytes));
    }

    [TestMethod]
    [DataRow(204)]
    [DataRow(304)]
    [DataRow(101)]
    public void Format_StatusWithoutBody_AddsNoContentLength(int status)
    {
        var text = Encoding.Latin1.GetString(DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(status, [], [])));

        Assert.IsFalse(text.Contains("Content-Length", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Format_ContentLengthGivenOrOff_AddsNoOtherContentLength()
    {
        var given = Encoding.Latin1.GetString(DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(200, [("content-LENGTH", "9")], [])));
        var off = Encoding.Latin1.GetString(DifferentialHttpResponseWriter.Format(new DifferentialHttpResponse(200, [], [], AddsContentLength: false)));

        Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\ncontent-LENGTH: 9\r\n\r\n", given);
        Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\n\r\n", off);
    }

    [TestMethod]
    public async Task WriteRawAsync_WritesBytesUnchanged()
    {
        using var output = new MemoryStream();

        await DifferentialHttpResponseWriter.WriteRawAsync(output, "raw\0"u8.ToArray(), CancellationToken.None);

        CollectionAssert.AreEqual("raw\0"u8.ToArray(), output.ToArray());
    }
}
