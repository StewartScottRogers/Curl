using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="Utf8Route"/>.</summary>
[TestClass]
public sealed class Utf8RouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersUtf8TextToEachAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Type: text/plain; charset=utf-8\r\nContent-Length: 18\r\n\r\nhÃ©llo wÃ¶rld â\u009C\u0093\n", 2)),
            await RespondAsync(new Utf8Route(), "GET /utf8 HTTP/1.1\r\n\r\nGET /utf8 HTTP/1.1\r\n\r\n"));
    }
}
