using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpTransferEncoding" /> against curl 8.21.0 (mingw, Schannel), measured
/// against a loopback server (BL-171 Notes).
/// </summary>
[TestClass]
public sealed class HttpTransferEncodingTests
{
    [TestMethod]
    [DataRow("chunked, gzip", "A Transfer-Encoding (gzip) was listed after chunked", DisplayName = "gzip after chunked")]
    [DataRow("chunked, identity", "A Transfer-Encoding (identity) was listed after chunked", DisplayName = "identity after chunked")]
    [DataRow("chunked, foo", "A Transfer-Encoding (foo) was listed after chunked", DisplayName = "foo after chunked")]
    [DataRow("gzip, chunked", "Unsolicited Transfer-Encoding (gzip) found", DisplayName = "gzip before chunked")]
    [DataRow("gzip", "Unsolicited Transfer-Encoding (gzip) found", DisplayName = "gzip alone")]
    [DataRow("foo", "Unsolicited Transfer-Encoding (foo) found", DisplayName = "foo alone")]
    [DataRow("chunked;q=1", "Unsolicited Transfer-Encoding (chunked;q=1) found", DisplayName = "chunked with a parameter")]
    [DataRow("chunkedx", "Unsolicited Transfer-Encoding (chunkedx) found", DisplayName = "chunkedx")]
    public void IsChunked_CodingCurlDoesNotDecode_ThrowsExit61(string value, string message)
    {
        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpTransferEncoding.IsChunked([new HttpResponseHeader("Transfer-Encoding", value)]));

        Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode);
        Assert.AreEqual(message, thrown.Message);
    }

    [TestMethod]
    [DataRow("chunked", "foo", "Unsolicited Transfer-Encoding (foo) found", DisplayName = "foo in a later header")]
    [DataRow("chunked", "gzip", "Unsolicited Transfer-Encoding (gzip) found", DisplayName = "gzip in a later header")]
    [DataRow("foo", "chunked", "Unsolicited Transfer-Encoding (foo) found", DisplayName = "foo in an earlier header")]
    public void IsChunked_CodingInAnotherHeader_ThrowsUnsolicited(string first, string second, string message)
    {
        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpTransferEncoding.IsChunked(
                [new HttpResponseHeader("Transfer-Encoding", first), new HttpResponseHeader("transfer-encoding", second)]));

        Assert.AreEqual(message, thrown.Message);
    }

    [TestMethod]
    public void IsChunked_InvalidContentLengthBeforeARejectedCoding_ThrowsExit8()
    {
        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpTransferEncoding.IsChunked(
                [new HttpResponseHeader("Content-Length", "abc"), new HttpResponseHeader("Transfer-Encoding", "foo")]));

        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", thrown.Message);
    }

    [TestMethod]
    public void IsChunked_RejectedCodingBeforeAnInvalidContentLength_ThrowsExit61()
    {
        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpTransferEncoding.IsChunked(
                [new HttpResponseHeader("Transfer-Encoding", "foo"), new HttpResponseHeader("Content-Length", "abc")]));

        Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode);
    }

    [TestMethod]
    public void IsChunked_NoTransferEncoding_IsFalse()
    {
        Assert.IsFalse(HttpTransferEncoding.IsChunked([new HttpResponseHeader("Content-Length", "5")]));
    }

    [TestMethod]
    [DataRow("chunked", true, DisplayName = "chunked")]
    [DataRow("gzip, chunked", true, DisplayName = "gzip before chunked is not refused")]
    [DataRow(" foo ,CHUNKED", true, DisplayName = "Any case, with blanks")]
    [DataRow("gzip", false, DisplayName = "gzip alone")]
    [DataRow("chunkedx", false, DisplayName = "chunkedx")]
    public void ListsChunked_Value_TellsWhetherChunkedIsListedRefusingNothing(string value, bool expected)
    {
        HttpResponseHeader[] headers = [new("Content-Length", "5"), new("transfer-encoding", value)];

        Assert.AreEqual(expected, HttpTransferEncoding.ListsChunked(headers));
    }
}
