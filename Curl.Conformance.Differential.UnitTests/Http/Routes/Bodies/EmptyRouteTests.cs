using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="EmptyRoute"/>.</summary>
[TestClass]
public sealed class EmptyRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersItsOwnZeroLengthOnceEachAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Length: 0\r\n\r\n", 2)),
            await RespondAsync(new EmptyRoute(), "GET /empty HTTP/1.1\r\n\r\nGET /empty HTTP/1.1\r\n\r\n"));
    }
}
