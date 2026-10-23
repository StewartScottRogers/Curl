using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/status-ver2</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class StatusVersion2RouteTests
{
    [TestMethod]
    public async Task StatusVersion2Route_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/2.0 200 OK\r\nContent-Length: 1\r\n\r\nx" + "HTTP/2.0 200 OK\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new StatusVersion2Route(), "/status-ver2"));
    }
}
