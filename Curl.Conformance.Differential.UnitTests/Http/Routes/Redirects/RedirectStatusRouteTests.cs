using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Redirects;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Redirects;

/// <summary>Pins the response bytes of server.py's <c>/r301</c> to <c>/r308</c> (<c>^/r30[1-8]$</c>).</summary>
[TestClass]
public sealed class RedirectStatusRouteTests
{
    [TestMethod]
    [DataRow("/r301", "301 Moved Permanently")]
    [DataRow("/r302", "302 Found")]
    [DataRow("/r303", "303 See Other")]
    [DataRow("/r304", "304 Not Modified")]
    [DataRow("/r305", "305 Status")]
    [DataRow("/r306", "306 Status")]
    [DataRow("/r307", "307 Temporary Redirect")]
    [DataRow("/r308", "308 Permanent Redirect")]
    public async Task RespondAsync_R301ToR308_AnswersThatStatusToEchoAndKeepsTheConnection(string path, string statusAndReason)
    {
        var response = $"HTTP/1.1 {statusAndReason}\r\n{RedirectRouteRequests.Date}Location: /echo\r\nContent-Length: 0\r\n\r\n";
        Assert.AreEqual(response + response, await RedirectRouteRequests.RespondTwiceThenToOtherPathAsync(new RedirectStatusRoute(), path));
    }

    [TestMethod]
    [DataRow("/r300")]
    [DataRow("/r309")]
    [DataRow("/r3011")]
    [DataRow("/x3011")]
    [DataRow("/x301")]
    public void Matches_PathOutsideR301ToR308_IsFalse(string path)
    {
        var request = new DifferentialHttpRequest("GET", path, string.Empty, new Dictionary<string, string>(), []);

        Assert.IsFalse(new RedirectStatusRoute().Matches(request));
    }
}
