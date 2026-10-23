using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/retry500</c> and its per-run counter.</summary>
[TestClass]
public sealed class Retry500RouteTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";
    private const string Request = "GET /retry500 HTTP/1.1\r\n\r\n";

    [TestMethod]
    public async Task RespondAsync_ThreeRequests_OneServerErrorThenOkAndKeepsTheConnection()
    {
        const string Ok = $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 13\r\n\r\nok after 500\n";
        Assert.AreEqual(
            $"HTTP/1.1 500 Internal Server Error\r\n{Date}Content-Length: 4\r\n\r\nerr\n" + Ok + Ok,
            await DifferentialHttpResponderTests.RespondAsync([new Retry500Route()], new DifferentialResponderState(), Request + Request + Request));
    }
}
