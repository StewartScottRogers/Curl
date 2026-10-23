using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/chunk-trunc</c>.</summary>
[TestClass]
public sealed class ChunkTruncRouteTests
{
    [TestMethod]
    public async Task ChunkTruncRoute_TwoRequests_AnswersOnceAndCloses()
    {
        var answer = "HTTP/1.1 200 OK\r\nTransfer-Encoding: chunked\r\n\r\n5\r\nab";

        Assert.AreEqual(answer, await RespondTwiceAsync(new ChunkTruncRoute(), "/chunk-trunc"));
    }
}
