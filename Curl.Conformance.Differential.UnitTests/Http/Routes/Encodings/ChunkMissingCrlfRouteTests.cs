using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/chunk-missing-crlf</c>.</summary>
[TestClass]
public sealed class ChunkMissingCrlfRouteTests
{
    [TestMethod]
    public async Task ChunkMissingCrlfRoute_TwoRequests_AnswersOnceAndCloses()
    {
        var answer = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n3\r\nabcXX0\r\n\r\n";

        Assert.AreEqual(answer, await RespondTwiceAsync(new ChunkMissingCrlfRoute(), "/chunk-missing-crlf"));
    }
}
