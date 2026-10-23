using System.IO.Compression;
using System.Text;
using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/gziptrunc</c>: the head, the framing and the decompressed content.</summary>
[TestClass]
public sealed class GzipTruncRouteTests
{
    [TestMethod]
    public async Task GzipTruncRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var (head, body) = await RespondKeptAsync(new GzipTruncRoute(), "/gziptrunc");

        Assert.AreEqual(OkHead("Content-Encoding: gzip\r\n", body.Length), head);
        CollectionAssert.AreEqual(PythonGzipHeader, body[..10]);
        Assert.AreEqual(Repeated("good data here\n", 20), Text(Decompress(body[10..], s => new DeflateStream(s, CompressionMode.Decompress))));
        CollectionAssert.AreEqual(ServerPyCompression.Gzip(Encoding.ASCII.GetBytes(Repeated("good data here\n", 20)))[..^8], body);
    }
}
