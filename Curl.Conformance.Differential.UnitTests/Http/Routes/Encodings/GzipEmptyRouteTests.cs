using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/gzipempty</c>.</summary>
[TestClass]
public sealed class GzipEmptyRouteTests
{
    [TestMethod]
    public async Task GzipEmptyRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var answer = $"HTTP/1.1 200 OK\r\n{Date}Content-Encoding: gzip\r\nContent-Length: 0\r\n\r\n";

        Assert.AreEqual(answer + answer, await RespondTwiceAsync(new GzipEmptyRoute(), "/gzipempty"));
    }
}
