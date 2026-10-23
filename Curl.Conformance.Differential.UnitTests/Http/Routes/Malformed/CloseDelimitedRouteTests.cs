using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/closedelim</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class CloseDelimitedRouteTests
{
    [TestMethod]
    public async Task CloseDelimitedRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nuntil close\n", await MalformedRouteResponses.RespondTwiceAsync(new CloseDelimitedRoute(), "/closedelim"));
    }
}
