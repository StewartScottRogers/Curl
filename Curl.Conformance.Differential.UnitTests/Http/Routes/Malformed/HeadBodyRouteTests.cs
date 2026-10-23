using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/head-body</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class HeadBodyRouteTests
{
    [TestMethod]
    public async Task HeadBodyRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 5\r\n\r\nhello", await MalformedRouteResponses.RespondTwiceAsync(new HeadBodyRoute(), "/head-body"));
    }
}
