using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/cl-huge</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class ContentLengthHugeRouteTests
{
    [TestMethod]
    public async Task ContentLengthHugeRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 99999999999999999999\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new ContentLengthHugeRoute(), "/cl-huge"));
    }
}
