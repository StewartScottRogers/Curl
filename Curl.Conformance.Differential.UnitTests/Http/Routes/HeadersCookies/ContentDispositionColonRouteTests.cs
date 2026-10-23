using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cd-colon</c>.</summary>
[TestClass]
public sealed class ContentDispositionColonRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersAttachmentNamedWithColonQuestionMarkAndStarAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: attachment; filename=\"a:b?c*.txt\"\r\n"
            + "Content-Length: 6\r\n\r\ncolon\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionColonRoute(), "/cd-colon", "/cd-colon", "/cd-colonx"));
    }
}
