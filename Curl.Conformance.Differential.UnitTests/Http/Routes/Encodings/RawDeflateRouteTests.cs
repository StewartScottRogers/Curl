using System.IO.Compression;
using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/rawdeflate</c>: the head, the framing and the decompressed content.</summary>
[TestClass]
public sealed class RawDeflateRouteTests
{
    [TestMethod]
    public async Task RawDeflateRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var (head, body) = await RespondKeptAsync(new RawDeflateRoute(), "/rawdeflate");

        Assert.AreEqual(OkHead("Content-Encoding: deflate\r\n", body.Length), head);
        Assert.AreNotEqual(0x78, body[0]);
        Assert.AreEqual("raw deflated\n", Text(Decompress(body, s => new DeflateStream(s, CompressionMode.Decompress))));
    }
}
