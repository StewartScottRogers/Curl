using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/http10len</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class Http10LengthRouteTests
{
    [TestMethod]
    public async Task Http10LengthRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.0 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nContent-Length: 5\r\n\r\nabcde", await MalformedRouteResponses.RespondTwiceAsync(new Http10LengthRoute(), "/http10len"));
    }
}
