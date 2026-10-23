using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="JsonishRoute"/>.</summary>
[TestClass]
public sealed class JsonishRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersJsonWithCharsetToEachAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Type: application/json; charset=utf-8\r\nContent-Length: 22\r\n\r\n{{\"k\":[1,2,{{\"z\":null}}]}}", 2)),
            await RespondAsync(new JsonishRoute(), "GET /jsonish HTTP/1.1\r\n\r\nGET /jsonish HTTP/1.1\r\n\r\n"));
    }
}
