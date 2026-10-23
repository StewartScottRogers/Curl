using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/cl-neg</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class ContentLengthNegativeRouteTests
{
    [TestMethod]
    public async Task ContentLengthNegativeRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: -5\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new ContentLengthNegativeRoute(), "/cl-neg"));
    }
}
