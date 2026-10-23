using System.IO.Compression;
using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/deflate</c>: the head, the framing and the decompressed content.</summary>
[TestClass]
public sealed class DeflateRouteTests
{
    [TestMethod]
    public async Task DeflateRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var (head, body) = await RespondKeptAsync(new DeflateRoute(), "/deflate");

        Assert.AreEqual(OkHead("Content-Encoding: deflate\r\n", body.Length), head);
        CollectionAssert.AreEqual(new byte[] { 0x78, 0x9c }, body[..2]);
        Assert.AreEqual(Repeated("deflated body\n", 50), Text(Decompress(body, s => new ZLibStream(s, CompressionMode.Decompress))));
    }
}
