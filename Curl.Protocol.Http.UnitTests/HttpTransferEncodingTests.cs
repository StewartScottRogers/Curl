using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins <see cref="HttpTransferEncoding" /> against curl 8.21.0 (mingw, Schannel), measured
/// against a loopback server (BL-171 Notes).
/// </summary>
[TestClass]
public sealed class HttpTransferEncodingTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
        Diagnostics.Arrange("Transfer-Encoding", value);

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpTransferEncoding.IsChunked([new HttpResponseHeader("Transfer-Encoding", value)]));

        Diagnostics.Act("exception type", thrown.GetType().Name);
        Diagnostics.Act("exit code", thrown.ExitCode);
        Diagnostics.Act("message", thrown.Message);
        Diagnostics.Assert("exit code", CurlExitCode.BadContentEncoding, thrown.ExitCode);
        Diagnostics.Assert("message", message, thrown.Message);
        Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode);
        Assert.AreEqual(message, thrown.Message);
    }

    [TestMethod]
    [DataRow("chunked", "foo", "Unsolicited Transfer-Encoding (foo) found", DisplayName = "foo in a later header")]
    [DataRow("chunked", "gzip", "Unsolicited Transfer-Encoding (gzip) found", DisplayName = "gzip in a later header")]
    [DataRow("foo", "chunked", "Unsolicited Transfer-Encoding (foo) found", DisplayName = "foo in an earlier header")]
    public void IsChunked_CodingInAnotherHeader_ThrowsUnsolicited(string first, string second, string message)
    {
        Diagnostics.Arrange("first Transfer-Encoding", first);
        Diagnostics.Arrange("second Transfer-Encoding", second);

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpTransferEncoding.IsChunked(
                [new HttpResponseHeader("Transfer-Encoding", first), new HttpResponseHeader("transfer-encoding", second)]));

        Diagnostics.Act("exception type", thrown.GetType().Name);
        Diagnostics.Act("exit code", thrown.ExitCode);
        Diagnostics.Act("message", thrown.Message);
        Diagnostics.Assert("message", message, thrown.Message);
        Assert.AreEqual(message, thrown.Message);
    }

    [TestMethod]
    public void IsChunked_InvalidContentLengthBeforeARejectedCoding_ThrowsExit8()
    {
        Diagnostics.Arrange("headers", "Content-Length: abc, Transfer-Encoding: foo");

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpTransferEncoding.IsChunked(
                [new HttpResponseHeader("Content-Length", "abc"), new HttpResponseHeader("Transfer-Encoding", "foo")]));

        Diagnostics.Act("exception type", thrown.GetType().Name);
        Diagnostics.Act("exit code", thrown.ExitCode);
        Diagnostics.Act("message", thrown.Message);
        Diagnostics.Assert("exit code", CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Diagnostics.Assert("message", "Invalid Content-Length: value", thrown.Message);
        Assert.AreEqual(CurlExitCode.WeirdServerReply, thrown.ExitCode);
        Assert.AreEqual("Invalid Content-Length: value", thrown.Message);
    }

    [TestMethod]
    public void IsChunked_RejectedCodingBeforeAnInvalidContentLength_ThrowsExit61()
    {
        Diagnostics.Arrange("headers", "Transfer-Encoding: foo, Content-Length: abc");

        HttpTransferException thrown = Assert.ThrowsExactly<HttpTransferException>(
            () => HttpTransferEncoding.IsChunked(
                [new HttpResponseHeader("Transfer-Encoding", "foo"), new HttpResponseHeader("Content-Length", "abc")]));

        Diagnostics.Act("exception type", thrown.GetType().Name);
        Diagnostics.Act("exit code", thrown.ExitCode);
        Diagnostics.Act("message", thrown.Message);
        Diagnostics.Assert("exit code", CurlExitCode.BadContentEncoding, thrown.ExitCode);
        Assert.AreEqual(CurlExitCode.BadContentEncoding, thrown.ExitCode);
    }

    [TestMethod]
    public void IsChunked_NoTransferEncoding_IsFalse()
    {
        Diagnostics.Arrange("headers", "Content-Length: 5");

        bool isChunked = HttpTransferEncoding.IsChunked([new HttpResponseHeader("Content-Length", "5")]);

        Diagnostics.Act("is chunked", isChunked);
        Diagnostics.Assert("is chunked", false, isChunked);
        Assert.IsFalse(isChunked);
    }

    [TestMethod]
    [DataRow("chunked", true, DisplayName = "chunked")]
    [DataRow("gzip, chunked", true, DisplayName = "gzip before chunked is not refused")]
    [DataRow(" foo ,CHUNKED", true, DisplayName = "Any case, with blanks")]
    [DataRow("gzip", false, DisplayName = "gzip alone")]
    [DataRow("chunkedx", false, DisplayName = "chunkedx")]
    public void ListsChunked_Value_TellsWhetherChunkedIsListedRefusingNothing(string value, bool expected)
    {
        Diagnostics.Arrange("Transfer-Encoding", value);
        HttpResponseHeader[] headers = [new("Content-Length", "5"), new("transfer-encoding", value)];

        bool listsChunked = HttpTransferEncoding.ListsChunked(headers);

        Diagnostics.Act("lists chunked", listsChunked);
        Diagnostics.Assert("lists chunked", expected, listsChunked);
        Assert.AreEqual(expected, listsChunked);
    }
}
