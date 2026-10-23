using Curl.Conformance.Differential.Http.Routes.Encodings;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings.EncodingRouteResponses;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Encodings;

/// <summary>Pins the response bytes of server.py's <c>/identity</c>.</summary>
[TestClass]
public sealed class IdentityRouteTests
{
    [TestMethod]
    public async Task IdentityRoute_TwoRequests_AnswersEachRequestAndKeepsTheConnection()
    {
        var answer = $"HTTP/1.1 200 OK\r\n{Date}Content-Encoding: identity\r\nContent-Length: 6\r\n\r\nident\n";

        Assert.AreEqual(answer + answer, await RespondTwiceAsync(new IdentityRoute(), "/identity"));
    }
}
