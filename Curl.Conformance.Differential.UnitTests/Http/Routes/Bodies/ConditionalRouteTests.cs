using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="ConditionalRoute"/>.</summary>
[TestClass]
public sealed class ConditionalRouteTests
{
    [TestMethod]
    public async Task RespondAsync_MatchingAndOtherEntityTags_Answers304ThenFreshBodyAndKeepsTheConnection()
    {
        Assert.AreEqual(
            $"HTTP/1.1 304 Not Modified\r\n{Date}ETag: \"v1\"\r\n\r\n" + string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}ETag: \"v1\"\r\nContent-Length: 6\r\n\r\nfresh\n", 2)),
            await RespondAsync(new ConditionalRoute(), "GET /cond HTTP/1.1\r\nIf-None-Match: \"v1\"\r\n\r\nGET /cond HTTP/1.1\r\nIf-None-Match: \"v2\"\r\n\r\nGET /cond HTTP/1.1\r\n\r\n"));
    }
}
