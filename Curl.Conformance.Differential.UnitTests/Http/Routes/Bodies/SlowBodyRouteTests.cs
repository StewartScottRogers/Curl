using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes and the delay of <see cref="SlowBodyRoute"/>.</summary>
[TestClass]
public sealed class SlowBodyRouteTests
{
    [TestMethod]
    public async Task RespondAsync_OneRequest_WaitsThreeSecondsBetweenBodyHalvesAndKeepsTheConnection()
    {
        var firstHalf = $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 20\r\n\r\n0123456789";

        var (written, delays) = await RespondWithDelaysAsync(new SlowBodyRoute(), "GET /slowbody HTTP/1.1\r\n\r\nGET /slowbody HTTP/1.1\r\n\r\n");

        Assert.AreEqual(string.Concat(Enumerable.Repeat(firstHalf + "0123456789", 2)), written);
        CollectionAssert.AreEqual(
            new[] { (TimeSpan.FromSeconds(3), firstHalf.Length), (TimeSpan.FromSeconds(3), (firstHalf.Length * 2) + 10) },
            delays.ToArray());
    }
}
