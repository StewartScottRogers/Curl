using System.IO.Compression;
using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/gzip</c>: the head, the framing and the decompressed content.</summary>
[TestClass]
public sealed class GzipRouteTests
{
    [TestMethod]
    public async Task GzipRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var (head, body) = await RespondKeptAsync(new GzipRoute(), "/gzip");

        Assert.AreEqual(OkHead("Content-Encoding: gzip\r\nContent-Type: text/plain\r\n", body.Length), head);
        CollectionAssert.AreEqual(PythonGzipHeader, body[..10]);
        Assert.AreEqual(Repeated("gzipped body\n", 50), Text(Decompress(body, s => new GZipStream(s, CompressionMode.Decompress))));
    }
}
