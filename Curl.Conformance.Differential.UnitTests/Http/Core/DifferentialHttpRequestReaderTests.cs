using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Core;

[TestClass]
public sealed class DifferentialHttpRequestReaderTests
{
    [TestMethod]
    public async Task ReadAsync_EmptyStream_ReturnsNull()
    {
        Assert.IsNull(await ReadAsync(string.Empty));
    }

    [TestMethod]
    public async Task ReadAsync_HeadersAndQuery_LowerCasesNamesLastWinsAndSplitsQuery()
    {
        var read = await ReadAsync("GET http://h:1/p?a=1?b HTTP/1.1\r\nX-A: 1\r\nx-a:  2 \r\nNoColon\r\n\r\n");

        var request = read!.Request!;
        Assert.AreEqual("GET", request.Method);
        Assert.AreEqual("/p", request.Path);
        Assert.AreEqual("a=1?b", request.Query);
        Assert.AreEqual("2", request.Headers["x-a"]);
        Assert.AreEqual(string.Empty, request.Headers["nocolon"]);
        Assert.AreEqual("GET http://h:1/p?a=1?b HTTP/1.1\r\nX-A: 1\r\nx-a:  2 \r\nNoColon\r\n\r\n", Encoding.Latin1.GetString(read.LoggedBytes));
    }

    [TestMethod]
    public async Task ReadAsync_ContentLengthAndExpect_ReadsBodyAndWritesContinue()
    {
        using var response = new MemoryStream();
        var read = await ReadAsync("POST /x HTTP/1.1\nExpect: 100-continue\nContent-Length: 3\n\nabcdef", response);

        Assert.AreEqual("abc", Encoding.Latin1.GetString(read!.Request!.Body));
        Assert.AreEqual("HTTP/1.1 100 Continue\r\n\r\n", Encoding.Latin1.GetString(response.ToArray()));
    }

    [TestMethod]
    public async Task ReadAsync_ChunkedBody_KeepsFramingAndTrailer()
    {
        var read = await ReadAsync("POST / HTTP/1.1\r\nTransfer-Encoding: Chunked\r\nContent-Length: 1\r\n\r\n3;ext=1\r\nabc\r\n\r\n0\r\nT: 1\r\n\r\nNEXT");

        Assert.AreEqual("3;ext=1\r\nabc\r\n\r\n0\r\nT: 1\r\n\r\n", Encoding.Latin1.GetString(read!.Request!.Body));
    }

    [TestMethod]
    public async Task ReadAsync_ChunkedBodyEndingEarly_StopsAtTheEnd()
    {
        var read = await ReadAsync("POST / HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nab");

        Assert.AreEqual("2\r\nab", Encoding.Latin1.GetString(read!.Request!.Body));
    }

    [TestMethod]
    [DataRow("FFFFFFFFFFFFFFFFF")]
    [DataRow("FFFFFFFFFFFFFFFF")]
    public async Task ReadAsync_ChunkSizeTooLargeForLong_ReadsToTheEnd(string size)
    {
        var read = await ReadAsync($"POST / HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\n{size}\r\nxyz");

        Assert.AreEqual($"{size}\r\nxyz", Encoding.Latin1.GetString(read!.Request!.Body));
    }

    [TestMethod]
    public async Task ReadAsync_ChunkSizeNotHex_ReturnsNull()
    {
        Assert.IsNull(await ReadAsync("POST / HTTP/1.1\r\nTransfer-Encoding: chunked\r\n\r\nzz\r\n"));
    }

    [TestMethod]
    public async Task ReadAsync_ContentLengthNotANumber_ReturnsNull()
    {
        Assert.IsNull(await ReadAsync("POST / HTTP/1.1\r\nContent-Length: x\r\n\r\n"));
    }

    [TestMethod]
    public async Task ReadAsync_NegativeContentLength_ReadsToTheEnd()
    {
        var read = await ReadAsync("POST / HTTP/1.1\r\nContent-Length: -5\r\n\r\nall of it");

        Assert.AreEqual("all of it", Encoding.Latin1.GetString(read!.Request!.Body));
    }

    [TestMethod]
    public async Task ReadAsync_RequestLineWithoutTarget_LogsButHasNoRequest()
    {
        var read = await ReadAsync("GARBAGE\r\n\r\n");

        Assert.IsNull(read!.Request);
        Assert.AreEqual("GARBAGE\r\n\r\n", Encoding.Latin1.GetString(read.LoggedBytes));
    }

    [TestMethod]
    public async Task ReadAsync_LineLongerThanTheBuffer_ReadsItWhole()
    {
        var target = "/" + new string('a', 10000);
        var read = await ReadAsync($"GET {target} HTTP/1.1\r\n\r\n");

        Assert.AreEqual(target, read!.Request!.Path);
    }

    [TestMethod]
    [DataRow("http://h:1/x", "/x")]
    [DataRow("https://h", "/")]
    [DataRow("http://", "http://")]
    [DataRow("http:///x", "http:///x")]
    [DataRow("/p", "/p")]
    [DataRow("", "/")]
    public void ReduceTargetToPath_RemovesSchemeAndAuthority(string target, string expected)
    {
        Assert.AreEqual(expected, DifferentialHttpRequestReader.ReduceTargetToPath(target));
    }

    private static async Task<DifferentialHttpRequestRead?> ReadAsync(string request, Stream? response = null)
    {
        using var input = new MemoryStream(Encoding.Latin1.GetBytes(request));
        return await DifferentialHttpRequestReader.ReadAsync(
            new DifferentialHttpLineReader(input), response ?? Stream.Null, CancellationToken.None);
    }
}
