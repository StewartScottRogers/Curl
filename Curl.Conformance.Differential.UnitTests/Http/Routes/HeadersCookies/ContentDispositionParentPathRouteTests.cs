using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/cdbad</c>.</summary>
[TestClass]
public sealed class ContentDispositionParentPathRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersAttachmentNamedWithParentPathAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + "Content-Disposition: attachment; filename=\"../../evil.txt\"\r\n"
            + "Content-Length: 5\r\n\r\nevil\n";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ContentDispositionParentPathRoute(), "/cdbad", "/cdbad", "/cdbadx"));
    }
}
