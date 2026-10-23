using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's <c>/manyhdrs</c>.</summary>
[TestClass]
public sealed class ManyHeadersRouteTests
{
    [TestMethod]
    public async Task RespondAsync_GetItsPath_AnswersThreeHundredHeadersAndKeepsTheConnection()
    {
        var response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date
            + string.Concat(Enumerable.Range(0, 300).Select(index => $"X-H{index}: {new string('v', 50)}\r\n"))
            + "Content-Length: 1\r\n\r\nx";
        Assert.AreEqual(response + response, await HeadersCookiesRouteExchange.RespondAsync(new ManyHeadersRoute(), "/manyhdrs", "/manyhdrs", "/manyhdrsx"));
    }
}
