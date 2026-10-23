using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cd-long</c>.</summary>
[TestClass]
public sealed class ContentDispositionLongFilenameRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersAttachmentWithThreeHundredCharacterNameAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: attachment; filename=\"" + new string('n', 300) + ".txt\"\r\n"
            + "Content-Length: 5\r\n\r\nlong\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionLongFilenameRoute(), "/cd-long", "/cd-long", "/cd-longx"));
    }
}
