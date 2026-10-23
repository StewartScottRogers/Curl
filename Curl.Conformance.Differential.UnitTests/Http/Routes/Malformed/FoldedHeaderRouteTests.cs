using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/folded</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class FoldedHeaderRouteTests
{
    [TestMethod]
    public async Task FoldedHeaderRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nX-F: one\r\n two\r\nContent-Length: 1\r\n\r\nx" + "HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nX-F: one\r\n two\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new FoldedHeaderRoute(), "/folded"));
    }
}
