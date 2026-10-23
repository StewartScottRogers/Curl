using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/hdr-space-before-colon</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class HeaderSpaceBeforeColonRouteTests
{
    [TestMethod]
    public async Task HeaderSpaceBeforeColonRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nX-Sp : v\r\nContent-Length: 1\r\n\r\nx" + "HTTP/1.1 200 OK\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nX-Sp : v\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new HeaderSpaceBeforeColonRoute(), "/hdr-space-before-colon"));
    }
}
