using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/cl-dup-same</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class ContentLengthDuplicateSameRouteTests
{
    [TestMethod]
    public async Task ContentLengthDuplicateSameRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 1\r\nContent-Length: 1\r\n\r\nx" + "HTTP/1.1 200 OK\r\nContent-Length: 1\r\nContent-Length: 1\r\n\r\nx", await MalformedRouteResponses.RespondTwiceAsync(new ContentLengthDuplicateSameRoute(), "/cl-dup-same"));
    }
}
