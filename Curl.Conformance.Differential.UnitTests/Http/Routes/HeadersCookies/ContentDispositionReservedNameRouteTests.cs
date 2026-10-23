using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cd-reserved</c>.</summary>
[TestClass]
public sealed class ContentDispositionReservedNameRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersAttachmentNamedConAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: attachment; filename=\"CON\"\r\n"
            + "Content-Length: 4\r\n\r\ncon\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionReservedNameRoute(), "/cd-reserved", "/cd-reserved", "/cd-reservedx"));
    }
}
