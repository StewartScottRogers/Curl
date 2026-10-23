using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.HeadersCookies;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.HeadersCookies;

/// <summary>Pins the response bytes of server.py's prefix route for <c>/name:</c>, <c>/q?</c>, <c>/CON</c> and <c>/trail.</c>.</summary>
[TestClass]
public sealed class NamedPathRouteTests
{
    [TestMethod]
    public async Task RespondAsync_PathsWithEachPrefix_AnswerNamedAndKeepTheConnection()
    {
        const string Response = "HTTP/1.1 200 OK\r\n" + HeadersCookiesRouteExchange.Date + "Content-Length: 6\r\n\r\nnamed\n";
        Assert.AreEqual(
            Response + Response + Response,
            await HeadersCookiesRouteExchange.RespondAsync(new NamedPathRoute(), "/name:a", "/CONx", "/trail.txt", "/other", "/name:b"));
    }

    [TestMethod]
    public void Matches_PathStartingQuestionMarkPrefix_Matches()
    {
        Assert.IsTrue(new NamedPathRoute().Matches(new DifferentialHttpRequest("GET", "/q?x", string.Empty, new Dictionary<string, string>(), [])));
    }

    [TestMethod]
    public async Task RespondAsync_QueryAfterQ_IsNotAnswered()
    {
        Assert.AreEqual(string.Empty, await HeadersCookiesRouteExchange.RespondAsync(new NamedPathRoute(), "/q?x"));
    }
}
