using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/badstatus</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class BadStatusRouteTests
{
    [TestMethod]
    public async Task BadStatusRoute_TwoRequests_WritesItsBytesAndCloses()
    {
        Assert.AreEqual("garbage\r\n\r\n", await MalformedRouteResponses.RespondTwiceAsync(new BadStatusRoute(), "/badstatus"));
    }
}
