using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/status-lower</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class StatusLowercaseRouteTests
{
    [TestMethod]
    public async Task StatusLowercaseRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("http/1.1 200 OK\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new StatusLowercaseRoute(), "/status-lower"));
    }
}
