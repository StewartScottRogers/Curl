using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cd-backslash</c>.</summary>
[TestClass]
public sealed class ContentDispositionBackslashRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersAttachmentNamedWithTwoBackslashesAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: attachment; filename=\"..\\\\evil.txt\"\r\n"
            + "Content-Length: 3\r\n\r\nbs\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionBackslashRoute(), "/cd-backslash", "/cd-backslash", "/cd-backslashx"));
    }
}
