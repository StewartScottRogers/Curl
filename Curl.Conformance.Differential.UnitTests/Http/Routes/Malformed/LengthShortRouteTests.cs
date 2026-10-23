using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/lenshort</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class LengthShortRouteTests
{
    [TestMethod]
    public async Task LengthShortRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nContent-Length: 100\r\n\r\nshort", await MalformedRouteResponses.RespondTwiceAsync(new LengthShortRoute(), "/lenshort"));
    }
}
