using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/cl-and-te</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class ContentLengthAndTransferEncodingRouteTests
{
    [TestMethod]
    public async Task ContentLengthAndTransferEncodingRoute_TwoRequests_WritesItsBytesAndKeepsTheConnection()
    {
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 3\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nhi\r\n0\r\n\r\n" + "HTTP/1.1 200 OK\r\nContent-Length: 3\r\nTransfer-Encoding: chunked\r\n\r\n2\r\nhi\r\n0\r\n\r\n", await MalformedRouteResponses.RespondTwiceAsync(new ContentLengthAndTransferEncodingRoute(), "/cl-and-te"));
    }
}
