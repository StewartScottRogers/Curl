using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/chunk-huge-size</c>.</summary>
[TestClass]
public sealed class ChunkHugeSizeRouteTests
{
    [TestMethod]
    public async Task ChunkHugeSizeRoute_TwoRequests_AnswersOnceAndCloses()
    {
        var answer = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\nFFFFFFFFFFFFFFFFF\r\nx\r\n";

        Assert.AreEqual(answer, await RespondTwiceAsync(new ChunkHugeSizeRoute(), "/chunk-huge-size"));
    }
}
