using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/status-2digit</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class StatusTwoDigitRouteTests
{
    [TestMethod]
    public async Task StatusTwoDigitRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.1 20 OK\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new StatusTwoDigitRoute(), "/status-2digit"));
    }
}
