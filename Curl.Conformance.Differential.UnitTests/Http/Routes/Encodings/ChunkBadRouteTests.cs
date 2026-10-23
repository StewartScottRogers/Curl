using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/chunkbad</c>.</summary>
[TestClass]
public sealed class ChunkBadRouteTests
{
    [TestMethod]
    public async Task ChunkBadRoute_TwoRequests_AnswersOnceAndCloses()
    {
        var answer = $"HTTP/1.1 200 OK\r\n{Date}Transfer-Encoding: chunked\r\n\r\nzz\r\nhello\r\n";

        Assert.AreEqual(answer, await RespondTwiceAsync(new ChunkBadRoute(), "/chunkbad"));
    }
}
