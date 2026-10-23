using System.IO.Compression;
using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/gziptrail</c>: the head, the framing and the decompressed content.</summary>
[TestClass]
public sealed class GzipTrailRouteTests
{
    [TestMethod]
    public async Task GzipTrailRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var (head, body) = await RespondKeptAsync(new GzipTrailRoute(), "/gziptrail");

        Assert.AreEqual(OkHead("Content-Encoding: gzip\r\n", body.Length), head);
        Assert.EndsWith("TRAILING GARBAGE", Text(body));
        CollectionAssert.AreEqual(PythonGzipHeader, body[..10]);
        Assert.AreEqual("good\n", Text(Decompress(body[..^16], s => new GZipStream(s, CompressionMode.Decompress))));
    }
}
