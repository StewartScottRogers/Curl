using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/ct-cd-star</c>.</summary>
[TestClass]
public sealed class ContentDispositionExtendedFilenameRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersAttachmentNamedByExtendedFilenameAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: attachment; filename*=UTF-8''na%C3%AFve%20file.txt\r\n"
            + "Content-Length: 5\r\n\r\nstar\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionExtendedFilenameRoute(), "/ct-cd-star", "/ct-cd-star", "/ct-cd-starx"));
    }
}
