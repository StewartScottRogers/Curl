using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/status-nocode</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class StatusNoCodeRouteTests
{
    [TestMethod]
    public async Task StatusNoCodeRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.1 \r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new StatusNoCodeRoute(), "/status-nocode"));
    }
}
