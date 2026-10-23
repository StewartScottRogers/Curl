using System.Text;
using Curl.Conformance.Differential.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Core;

[TestClass]
public sealed class DifferentialHttpResponderTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";

    [TestMethod]
    public async Task RespondAsync_KeepAliveRequests_AnswersEachAndLogsEach()
    {
        var state = new DifferentialResponderState();

        var output = await RespondAsync(DifferentialRoutes.All, state, "GET /echo HTTP/1.1\r\n\r\nGET /other HTTP/1.1\r\n\r\n");

        Assert.AreEqual(
            $"HTTP/1.1 200 OK\r\n{Date}Content-Type: text/plain\r\nContent-Length: 3\r\n\r\nok\n"
            + $"HTTP/1.1 200 OK\r\n{Date}Content-Type: text/plain\r\nContent-Length: 8\r\n\r\ndefault\n",
            output);
        Assert.AreEqual("GET /echo HTTP/1.1\r\n\r\n\0SEP\0GET /other HTTP/1.1\r\n\r\n", Encoding.Latin1.GetString(state.TakeRequestLog()));
    }

    [TestMethod]
    public async Task RespondAsync_RouteClosesConnection_ReadsNoFurtherRequest()
    {
        var state = new DifferentialResponderState();

        var output = await RespondAsync(DifferentialRoutes.All, state, "CONNECT h:1 HTTP/1.1\r\n\r\nGET / HTTP/1.1\r\n\r\n");

        Assert.AreEqual($"HTTP/1.1 200 OK\r\n{Date}Content-Length: 0\r\n\r\n", output);
        Assert.AreEqual("CONNECT h:1 HTTP/1.1\r\n\r\n", Encoding.Latin1.GetString(state.TakeRequestLog()));
    }

    [TestMethod]
    public async Task RespondAsync_UnparsableRequestOrNoRoute_ClosesAfterLogging()
    {
        var state = new DifferentialResponderState();

        Assert.AreEqual(string.Empty, await RespondAsync(DifferentialRoutes.All, state, "BAD\r\n\r\nGET / HTTP/1.1\r\n\r\n"));
        Assert.AreEqual(string.Empty, await RespondAsync([], state, "GET / HTTP/1.1\r\n\r\nGET / HTTP/1.1\r\n\r\n"));
        Assert.AreEqual("BAD\r\n\r\n\0SEP\0GET / HTTP/1.1\r\n\r\n", Encoding.Latin1.GetString(state.TakeRequestLog()));
    }

    [TestMethod]
    public async Task RespondAsync_ReadFails_ReturnsQuietly()
    {
        using var connection = new DuplexMemoryStream([], failsReads: true);

        await Responder(DifferentialRoutes.All, new DifferentialResponderState()).RespondAsync(connection, CancellationToken.None);

        Assert.AreEqual(0, connection.Written.Length);
    }

    [TestMethod]
    public void All_OrdersSpecificRoutesBeforeConnectAndDefault()
    {
        var ranks = DifferentialRoutes.All.Select(route => route.Rank).ToList();

        CollectionAssert.AreEqual(ranks.Order().ToList(), ranks);
        Assert.IsInstanceOfType<DefaultRoute>(DifferentialRoutes.All[^1]);
        Assert.IsInstanceOfType<ConnectRoute>(DifferentialRoutes.All[^2]);
    }

    internal static async Task<string> RespondAsync(IReadOnlyList<IDifferentialRoute> routes, DifferentialResponderState state, string requests)
    {
        using var connection = new DuplexMemoryStream(Encoding.Latin1.GetBytes(requests));
        await Responder(routes, state).RespondAsync(connection, CancellationToken.None);
        return Encoding.Latin1.GetString(connection.Written);
    }

    private static DifferentialHttpResponder Responder(IReadOnlyList<IDifferentialRoute> routes, DifferentialResponderState state) =>
        new(routes, state, new DifferentialResponderOptions(18080, TimeProvider.System));
}
