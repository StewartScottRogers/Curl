using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="EarlyContinueRoute"/>.</summary>
[TestClass]
public sealed class EarlyContinueRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_SendsUnaskedContinueThenOkAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 100 Continue\r\nX-Junk: 1\r\n\r\nHTTP/1.1 200 OK\r\n{Date}Content-Length: 10\r\n\r\nafter 100\n", 2)),
            await RespondAsync(new EarlyContinueRoute(), "GET /early100 HTTP/1.1\r\n\r\nGET /early100 HTTP/1.1\r\n\r\n"));
    }
}
