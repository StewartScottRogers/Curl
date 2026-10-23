using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/altsvc</c>.</summary>
[TestClass]
public sealed class AltSvcRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersHttp3AltSvcAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Alt-Svc: h3=\":443\"; ma=3600\r\n"
            + "Content-Length: 1\r\n\r\nx";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new AltSvcRoute(), "/altsvc", "/altsvc", "/altsvcx"));
    }
}
