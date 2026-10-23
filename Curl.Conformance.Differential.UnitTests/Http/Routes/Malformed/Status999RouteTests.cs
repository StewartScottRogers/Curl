using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/status-999</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class Status999RouteTests
{
    [TestMethod]
    public async Task Status999Route_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 999 Odd\r\nContent-Length: 1\r\n\r\nx" + "HTTP/1.1 999 Odd\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new Status999Route(), "/status-999"));
    }
}
