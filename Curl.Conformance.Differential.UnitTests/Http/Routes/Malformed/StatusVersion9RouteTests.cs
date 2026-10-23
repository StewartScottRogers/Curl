using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/status-ver9</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class StatusVersion9RouteTests
{
    [TestMethod]
    public async Task StatusVersion9Route_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.9 200 OK\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new StatusVersion9Route(), "/status-ver9"));
    }
}
