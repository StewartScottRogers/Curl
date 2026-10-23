using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes and the delay of <see cref="SlowRoute"/>.</summary>
[TestClass]
public sealed class SlowRouteTests
{
    [TestMethod]
    public async Task RespondAsync_OneRequest_WaitsThreeSecondsBeforeAnsweringAndKeepsTheConnection()
    {
        var (written, delays) = await RespondWithDelaysAsync(new SlowRoute(), "GET /slow HTTP/1.1\r\n\r\nGET /slow HTTP/1.1\r\n\r\n");

        Assert.AreEqual(string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Length: 5\r\n\r\nslow\n", 2)), written);
        var answerLength = written.Length / 2;
        CollectionAssert.AreEqual(new[] { (TimeSpan.FromSeconds(3), 0), (TimeSpan.FromSeconds(3), answerLength) }, delays.ToArray());
    }
}
