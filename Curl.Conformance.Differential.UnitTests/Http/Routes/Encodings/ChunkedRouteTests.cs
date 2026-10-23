using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/chunked</c>.</summary>
[TestClass]
public sealed class ChunkedRouteTests
{
    [TestMethod]
    public async Task ChunkedRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var answer = $"HTTP/1.1 200 OK\r\n{Date}Transfer-Encoding: chunked\r\nTrailer: X-T\r\n\r\n5\r\nhello\r\n6;ext=1\r\n world\r\n0\r\nX-T: trailer\r\n\r\n";

        Assert.AreEqual(answer + answer, await RespondTwiceAsync(new ChunkedRoute(), "/chunked"));
    }
}
