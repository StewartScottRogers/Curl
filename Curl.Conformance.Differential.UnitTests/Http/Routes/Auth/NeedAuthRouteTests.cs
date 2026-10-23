using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/needauth</c>.</summary>
[TestClass]
public sealed class NeedAuthRouteTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    [TestMethod]
    public async Task RespondAsync_BasicUP_AnswersAuthedAndKeepsTheConnection()
    {
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 7\r\n\r\nauthed\n"
            + $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 7\r\n\r\nauthed\n",
            await DifferentialHttpResponderTests.RespondAsync(
                [new NeedAuthRoute()],
                new DifferentialResponderState(),
                "GET /needauth HTTP/1.1\r\nAuthorization: Basic dTpw\r\n\r\nGET /needauth HTTP/1.1\r\nAuthorization: Basic dTpw\r\n\r\n"));
    }

    [TestMethod]
    public async Task RespondAsync_NoOrWrongCredentials_AnswersBasicChallenge()
    {
        const string Denied = $"HTTP/1.1 401 Unauthorized\r\n{Date}WWW-Authenticate: Basic realm=\"r\"\r\nContent-Length: 7\r\n\r\ndenied\n";
        Assert.AreEqual(
            Denied + Denied,
            await DifferentialHttpResponderTests.RespondAsync(
                [new NeedAuthRoute()],
                new DifferentialResponderState(),
                "GET /needauth HTTP/1.1\r\n\r\nGET /needauth HTTP/1.1\r\nAuthorization: Basic dTpx\r\n\r\n"));
    }
}
