using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="NoRangeRoute"/>.</summary>
[TestClass]
public sealed class NoRangeRouteTests
{
    [TestMethod]
    public async Task RespondAsync_RangeAsked_AnswersFullBodyAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Length: 25\r\n\r\nfull body ignoring range\n", 2)),
            await RespondAsync(new NoRangeRoute(), "GET /norange HTTP/1.1\r\nRange: bytes=0-1\r\n\r\nGET /norange HTTP/1.1\r\n\r\n"));
    }
}
