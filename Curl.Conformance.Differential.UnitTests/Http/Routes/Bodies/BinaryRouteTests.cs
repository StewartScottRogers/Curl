using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="BinaryRoute"/>.</summary>
[TestClass]
public sealed class BinaryRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoRequests_AnswersEveryByteValueAndKeepsTheConnection()
    {
        Assert.AreEqual(
            string.Concat(Enumerable.Repeat($"HTTP/1.1 200 OK\r\n{Date}Content-Length: 256\r\n\r\n" + new string([.. Enumerable.Range(0, 256).Select(value => (char)value)]), 2)),
            await RespondAsync(new BinaryRoute(), "GET /binary HTTP/1.1\r\n\r\nGET /binary HTTP/1.1\r\n\r\n"));
    }
}
