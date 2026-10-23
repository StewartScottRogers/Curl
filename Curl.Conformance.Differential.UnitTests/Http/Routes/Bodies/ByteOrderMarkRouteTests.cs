using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="ByteOrderMarkRoute"/>.</summary>
[TestClass]
public sealed class ByteOrderMarkRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersBodyStartingWithByteOrderMarkAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Length: 13\r\n\r\nï»¿bom start\n", 2)),
            await RespondAsync(new ByteOrderMarkRoute(), "GET /bom HTTP/1.1\r\n\r\nGET /bom HTTP/1.1\r\n\r\n"));
    }
}
