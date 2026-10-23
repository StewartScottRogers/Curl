using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/notmod</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class NotModifiedRouteTests
{
    [TestMethod]
    public async Task NotModifiedRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 304 Not Modified\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nETag: \"abc\"\r\n\r\n" + "HTTP/1.1 304 Not Modified\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\nETag: \"abc\"\r\n\r\n", await MalformedRouteResponses.RespondTwiceAsync(new NotModifiedRoute(), "/notmod"));
    }
}
