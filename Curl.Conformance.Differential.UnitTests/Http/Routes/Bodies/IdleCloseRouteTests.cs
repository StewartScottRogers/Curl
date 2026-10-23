using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes and the delay of <see cref="IdleCloseRoute"/>.</summary>
[TestClass]
public sealed class IdleCloseRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersKeepAliveWaitsAThirdOfASecondAndCloses()
    {
        var (written, delays) = await RespondWithDelaysAsync(new IdleCloseRoute(), "GET /idle-close HTTP/1.1\r\n\r\nGET /idle-close HTTP/1.1\r\n\r\n");

        Assert.AreEqual($"HTTP/1.1 200 OK\r\n{Date}Connection: keep-alive\r\nKeep-Alive: timeout=1\r\nContent-Length: 5\r\n\r\nidle\n", written);
        CollectionAssert.AreEqual(new[] { (TimeSpan.FromMilliseconds(300), written.Length) }, delays.ToArray());
    }
}
