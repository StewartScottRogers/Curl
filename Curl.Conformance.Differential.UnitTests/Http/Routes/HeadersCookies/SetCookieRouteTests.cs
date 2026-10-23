using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/setcookie</c>.</summary>
[TestClass]
public sealed class SetCookieRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersEightSetCookieHeadersAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Set-Cookie: a=1; Path=/\r\nSet-Cookie: b=2; Path=/echo; HttpOnly\r\nSet-Cookie: c=3; Max-Age=3600\r\n"
            + "Set-Cookie: d=4; Domain=127.0.0.1; Path=/\r\nSet-Cookie: e=5; Expires=Thu, 01 Jan 2099 00:00:00 GMT; Path=/\r\n"
            + "Set-Cookie: gone=1; Max-Age=0\r\nSet-Cookie: bad=1; Domain=evil.example\r\nSet-Cookie: s=1; Secure; Path=/\r\n"
            + "Content-Length: 8\r\n\r\ncookies\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new SetCookieRoute(), "/setcookie", "/setcookie", "/setcookiex"));
    }
}
