using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/304-body</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class NotModifiedWithLengthRouteTests
{
    [TestMethod]
    public async Task NotModifiedWithLengthRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 304 Not Modified\r\nContent-Length: 5\r\n\r\n" + "HTTP/1.1 304 Not Modified\r\nContent-Length: 5\r\n\r\n", await MalformedRouteResponses.RespondTwiceAsync(new NotModifiedWithLengthRoute(), "/304-body"));
    }
}
