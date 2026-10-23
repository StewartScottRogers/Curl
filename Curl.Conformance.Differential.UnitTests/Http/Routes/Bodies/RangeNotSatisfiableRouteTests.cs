using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="RangeNotSatisfiableRoute"/>.</summary>
[TestClass]
public sealed class RangeNotSatisfiableRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_Answers416WithStatusReasonAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 416 Status\r\n{Date}Content-Range: bytes */10\r\nContent-Length: 0\r\n\r\n", 2)),
            await RespondAsync(new RangeNotSatisfiableRoute(), "GET /range416 HTTP/1.1\r\nRange: bytes=0-1\r\n\r\nGET /range416 HTTP/1.1\r\n\r\n"));
    }
}
