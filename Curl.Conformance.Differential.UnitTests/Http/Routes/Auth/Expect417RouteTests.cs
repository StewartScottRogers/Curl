using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/expect417</c>.</summary>
[TestClass]
public sealed class Expect417RouteTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    [TestMethod]
    public async Task RespondAsync_WithThenWithoutExpect_ContinuesAndAnswers417ThenOkAndKeepsTheConnection()
    {
        Assert.AreEqual(
            "HTTP/1.1 100 Continue\r\n\r\n"
            + $"HTTP/1.1 417 Status\r\n{Date}Content-Length: 10\r\n\r\nno expect\n"
            + $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 23\r\n\r\nretried without expect\n",
            await DifferentialHttpResponderTests.RespondAsync(
                [new Expect417Route()],
                new DifferentialResponderState(),
                "GET /expect417 HTTP/1.1\r\nExpect: 100-continue\r\n\r\nGET /expect417 HTTP/1.1\r\n\r\n"));
    }
}
