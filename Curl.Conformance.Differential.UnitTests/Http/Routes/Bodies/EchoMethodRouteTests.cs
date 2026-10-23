using Curl.Conformance.Differential.Http.Routes.Bodies;
using static Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies.BodyRouteResponder;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Bodies;

/// <summary>Pins the response bytes of <see cref="EchoMethodRoute"/>.</summary>
[TestClass]
public sealed class EchoMethodRouteTests
{
    [TestMethod]
    public async Task RespondAsync_TwoMethods_AnswersEachMethodInHeaderAndKeepsTheConnection()
    {
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}X-Method: POST\r\nContent-Length: 0\r\n\r\nHTTP/1.1 200 OK\r\n{Date}X-Method: DELETE\r\nContent-Length: 0\r\n\r\n",
            await RespondAsync(new EchoMethodRoute(), "POST /echo-method HTTP/1.1\r\n\r\nDELETE /echo-method HTTP/1.1\r\n\r\n"));
    }
}
