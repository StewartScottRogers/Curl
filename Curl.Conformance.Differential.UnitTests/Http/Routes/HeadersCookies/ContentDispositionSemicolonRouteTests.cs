using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cd-semicolon</c>.</summary>
[TestClass]
public sealed class ContentDispositionSemicolonRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersAttachmentNamedWithSemicolonAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: attachment; filename=\"a;b.txt\"\r\n"
            + "Content-Length: 5\r\n\r\nsemi\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionSemicolonRoute(), "/cd-semicolon", "/cd-semicolon", "/cd-semicolonx"));
    }
}
