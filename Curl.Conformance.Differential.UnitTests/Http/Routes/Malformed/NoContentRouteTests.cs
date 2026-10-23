using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/nocontent</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class NoContentRouteTests
{
    [TestMethod]
    public async Task NoContentRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 204 No Content\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\n\r\n" + "HTTP/1.1 204 No Content\r\nDate: Thu, 01 Jan 2026 00:00:00 GMT\r\n\r\n", await MalformedRouteResponses.RespondTwiceAsync(new NoContentRoute(), "/nocontent"));
    }
}
