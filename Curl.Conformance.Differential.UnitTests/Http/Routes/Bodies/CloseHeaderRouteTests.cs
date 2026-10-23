using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="CloseHeaderRoute"/>.</summary>
[TestClass]
public sealed class CloseHeaderRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersOnceWithItsOwnConnectionCloseAndCloses()
    {
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}Connection: close\r\nContent-Length: 8\r\n\r\nclosing\n",
            await RespondAsync(new CloseHeaderRoute(), "GET /close-hdr HTTP/1.1\r\n\r\nGET /close-hdr HTTP/1.1\r\n\r\n"));
    }
}
