using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/badgzip</c>.</summary>
[TestClass]
public sealed class BadGzipRouteTests
{
    [TestMethod]
    public async Task BadGzipRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var answer = $"HTTP/1.1 200 OK\r\n{Date}Content-Encoding: gzip\r\nContent-Length: 15\r\n\r\nnot gzip at all";

        Assert.AreEqual(answer + answer, await RespondTwiceAsync(new BadGzipRoute(), "/badgzip"));
    }
}
