using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/hdr-badline</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class HeaderBadLineRouteTests
{
    [TestMethod]
    public async Task HeaderBadLineRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nthis line has no colon\r\nContent-Length: 1\r\n\r\nx" + "HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nthis line has no colon\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new HeaderBadLineRoute(), "/hdr-badline"));
    }
}
