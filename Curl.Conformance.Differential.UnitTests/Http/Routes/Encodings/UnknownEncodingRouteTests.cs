using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/unknownenc</c>.</summary>
[TestClass]
public sealed class UnknownEncodingRouteTests
{
    [TestMethod]
    public async Task UnknownEncodingRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var answer = $"HTTP/1.1 200 OK\r\n{Date}Content-Encoding: foo\r\nContent-Length: 4\r\n\r\ndata";

        Assert.AreEqual(answer + answer, await RespondTwiceAsync(new UnknownEncodingRoute(), "/unknownenc"));
    }
}
