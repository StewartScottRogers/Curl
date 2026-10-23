using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="NulBytesRoute"/>.</summary>
[TestClass]
public sealed class NulBytesRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersBodyWithNulBytesAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Length: 6\r\n\r\na\0b\0c\n", 2)),
            await RespondAsync(new NulBytesRoute(), "GET /nul HTTP/1.1\r\n\r\nGET /nul HTTP/1.1\r\n\r\n"));
    }
}
