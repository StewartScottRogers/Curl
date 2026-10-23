using System.IO.Compression;
using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/multienc</c>: the head, the framing and the decompressed content.</summary>
[TestClass]
public sealed class MultiEncodingRouteTests
{
    [TestMethod]
    public async Task MultiEncodingRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var (head, body) = await RespondKeptAsync(new MultiEncodingRoute(), "/multienc");

        Assert.AreEqual(OkHead("Content-Encoding: deflate, gzip\r\n", body.Length), head);
        CollectionAssert.AreEqual(PythonGzipHeader, body[..10]);
        var zlib = Decompress(body, s => new GZipStream(s, CompressionMode.Decompress));
        CollectionAssert.AreEqual(new byte[] { 0x78, 0x9c }, zlib[..2]);
        Assert.AreEqual("layered\n", Text(Decompress(zlib, s => new ZLibStream(s, CompressionMode.Decompress))));
    }
}
