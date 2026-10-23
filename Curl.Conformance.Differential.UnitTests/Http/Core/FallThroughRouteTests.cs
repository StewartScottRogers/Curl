using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Core;

/// <summary>Pins the response bytes of server.py's fall-through routes: <c>/status/&lt;n&gt;</c>, <c>/echo</c>, <c>/</c>, <c>CONNECT</c> and the default.</summary>
[TestClass]
public sealed class FallThroughRouteTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    [TestMethod]
    public async Task StatusCodeRoute_Number_AnswersThatStatusAndKeepsTheConnection()
    {
        Assert.AreEqual(
            $"HTTP/1.1 503 Service Unavailable\r\n{Date}Content-Type: text/plain\r\nContent-Length: 11\r\n\r\nstatus 503\n"
            + $"HTTP/1.1 299 Status\r\n{Date}Content-Type: text/plain\r\nContent-Length: 11\r\n\r\nstatus 299\n",
            await RespondAsync([new StatusCodeRoute()], "GET /status/503 HTTP/1.1\r\n\r\nGET /status/299/x HTTP/1.1\r\n\r\n"));
    }

    [TestMethod]
    public async Task StatusCodeRoute_NotANumber_ClosesUnanswered()
    {
        Assert.AreEqual(string.Empty, await RespondAsync([new StatusCodeRoute()], "GET /status/x HTTP/1.1\r\n\r\nGET /status/200 HTTP/1.1\r\n\r\n"));
    }

    [TestMethod]
    public async Task EchoRoute_GetAndHead_AnswersOkOrItsHeadOnly()
    {
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}Content-Type: text/plain\r\nContent-Length: 3\r\n\r\nok\n"
            + $"HTTP/1.1 200 OK\r\n{Date}Content-Type: text/plain\r\nContent-Length: 3\r\n\r\n",
            await RespondAsync([new EchoRoute()], "GET / HTTP/1.1\r\n\r\nHEAD /echo HTTP/1.1\r\n\r\nGET /other HTTP/1.1\r\n\r\n"));
    }

    [TestMethod]
    public async Task ConnectRoute_AnyTarget_AnswersEmptyOkAndCloses()
    {
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 0\r\n\r\n",
            await RespondAsync([new ConnectRoute()], "CONNECT h:443 HTTP/1.1\r\n\r\nCONNECT h:443 HTTP/1.1\r\n\r\n"));
    }

    [TestMethod]
    public async Task DefaultRoute_AnyRequest_AnswersDefaultAndKeepsTheConnection()
    {
        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}Content-Type: text/plain\r\nContent-Length: 8\r\n\r\ndefault\n"
            + $"HTTP/1.1 200 OK\r\n{Date}Content-Type: text/plain\r\nContent-Length: 8\r\n\r\ndefault\n",
            await RespondAsync([new DefaultRoute()], "POST /x HTTP/1.1\r\n\r\nPUT /y HTTP/1.1\r\n\r\n"));
    }

    private static Task<string> RespondAsync(IReadOnlyList<IDifferentialRoute> routes, string requests) =>
        DifferentialHttpResponderTests.RespondAsync(routes, new DifferentialResponderState(), requests);
}
