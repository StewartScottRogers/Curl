using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cookies2</c>.</summary>
[TestClass]
public sealed class Cookies2RouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersSeventeenSetCookieHeadersWithTheNonAsciiOneInUtf8AndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Set-Cookie: q=\"quoted val\"; Path=/\r\nSet-Cookie: sp=a b c; Path=/\r\nSet-Cookie: __Secure-x=1; Path=/\r\n"
            + "Set-Cookie: __Host-y=1; Path=/; Secure\r\nSet-Cookie: tld=1; Domain=.com\r\nSet-Cookie: ip=1; Domain=127.0.0.1\r\n"
            + "Set-Cookie: ss=1; SameSite=Strict; Path=/\r\nSet-Cookie: noval; Path=/\r\nSet-Cookie: =emptyname; Path=/\r\n"
            + "Set-Cookie: big=" + new string('v', 5000) + "; Path=/\r\n"
            + "Set-Cookie: exp=1; Expires=Wed, 09 Jun 2021 10:18:14 GMT; Path=/\r\n"
            + "Set-Cookie: future=1; Expires=Fri, 31 Dec 9999 23:59:59 GMT; Path=/\r\n"
            + "Set-Cookie: maxage=1; Max-Age=60; Expires=Wed, 09 Jun 2021 10:18:14 GMT; Path=/\r\n"
            + "Set-Cookie: dup=1; Path=/\r\nSet-Cookie: dup=2; Path=/\r\nSet-Cookie: pathx=1; Path=/other\r\n"
            + "Set-Cookie: uni=Ã©; Path=/\r\n"
            + "Content-Length: 3\r\n\r\nc2\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new Cookies2Route(), "/cookies2", "/cookies2", "/cookies2x"));
    }
}
