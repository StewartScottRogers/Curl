using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="LastModifiedRoute"/>.</summary>
[TestClass]
public sealed class LastModifiedRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersLastModifiedAndEntityTagAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Last-Modified: Wed, 21 Oct 2015 07:28:00 GMT\r\nETag: \"v1\"\r\nContent-Length: 8\r\n\r\nlm body\n", 2)),
            await RespondAsync(new LastModifiedRoute(), "GET /lm HTTP/1.1\r\n\r\nGET /lm HTTP/1.1\r\n\r\n"));
    }
}
