using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/cl-dup-diff</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class ContentLengthDuplicateDifferentRouteTests
{
    [TestMethod]
    public async Task ContentLengthDuplicateDifferentRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 1\r\nContent-Length: 2\r\n\r\nxy", await MalformedRouteResponses.RespondTwiceAsync(new ContentLengthDuplicateDifferentRoute(), "/cl-dup-diff"));
    }
}
