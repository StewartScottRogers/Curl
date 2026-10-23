using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="EarlyHintsRoute"/>.</summary>
[TestClass]
public sealed class EarlyHintsRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_Sends103ThenOkAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 103 Early Hints\r\nLink: </s.css>; rel=preload\r\n\r\nHTTP/1.1 200 OK\r\n{Date}Content-Length: 10\r\n\r\nafter 103\n", 2)),
            await RespondAsync(new EarlyHintsRoute(), "GET /early HTTP/1.1\r\n\r\nGET /early HTTP/1.1\r\n\r\n"));
    }
}
