using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="ContentTypeRoute"/>.</summary>
[TestClass]
public sealed class ContentTypeRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersJsonToEachAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Type: application/json\r\nContent-Length: 7\r\n\r\n{{\"a\":1}}", 2)),
            await RespondAsync(new ContentTypeRoute(), "GET /ctype HTTP/1.1\r\n\r\nGET /ctype HTTP/1.1\r\n\r\n"));
    }
}
