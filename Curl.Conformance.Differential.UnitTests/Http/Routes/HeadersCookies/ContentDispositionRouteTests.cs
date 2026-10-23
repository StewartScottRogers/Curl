using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cd</c>.</summary>
[TestClass]
public sealed class ContentDispositionRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersNamedAttachmentWithLastModifiedAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: attachment; filename=\"named file.txt\"\r\nLast-Modified: Wed, 21 Oct 2015 07:28:00 GMT\r\n"
            + "Content-Length: 8\r\n\r\ncd body\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionRoute(), "/cd", "/cd", "/cdx"));
    }
}
