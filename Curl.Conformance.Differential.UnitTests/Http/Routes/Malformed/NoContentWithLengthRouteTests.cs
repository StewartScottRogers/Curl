using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/204-cl</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class NoContentWithLengthRouteTests
{
    [TestMethod]
    public async Task NoContentWithLengthRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 204 No Content\r\nContent-Length: 5\r\n\r\n" + "HTTP/1.1 204 No Content\r\nContent-Length: 5\r\n\r\n", await MalformedRouteResponses.RespondTwiceAsync(new NoContentWithLengthRoute(), "/204-cl"));
    }
}
