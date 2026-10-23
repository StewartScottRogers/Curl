using Curl.Conformance.Differential.Http.Routes.Malformed;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Malformed;

/// <summary>Pins the response bytes of server.py's <c>/nothing</c> and whether the connection closes after it.</summary>
[TestClass]
public sealed class NothingRouteTests
{
    [TestMethod]
    public async Task NothingRoute_TwoRequests_WritesNothingAndCloses()
    {
        Assert.AreEqual(string.Empty, await MalformedRouteResponses.RespondTwiceAsync(new NothingRoute(), "/nothing"));
    }
}
