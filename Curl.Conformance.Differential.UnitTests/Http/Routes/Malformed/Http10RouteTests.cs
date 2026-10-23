using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/http10</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class Http10RouteTests
{
    [TestMethod]
    public async Task Http10Route_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.0 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\n\r\nhttp10 body", await MalformedRouteResponses.RespondTwiceAsync(new Http10Route(), "/http10"));
    }
}
