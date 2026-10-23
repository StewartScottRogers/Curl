using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/retry</c> and its per-run counter.</summary>
[TestClass]
public sealed class RetryRouteTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";
    private const string Later = $"HTTP/1.1 503 Service Unavailable\r\n{Date}Retry-After: 1\r\nContent-Length: 6\r\n\r\nlater\n";
    private const string Finally = $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 8\r\n\r\nfinally\n";
    private const string Request = "GET /retry HTTP/1.1\r\n\r\n";

    [TestMethod]
    public async Task RespondAsync_FourRequests_TwoUnavailableThenOkAndKeepsTheConnection()
    {
        Assert.AreEqual(
            Later + Later + Finally + Finally,
            await DifferentialHttpResponderTests.RespondAsync([new RetryRoute()], new DifferentialResponderState(), Request + Request + Request + Request));
    }

    [TestMethod]
    public async Task RespondAsync_AfterReset_CountsFromOne()
    {
        var state = new DifferentialResponderState();
        await DifferentialHttpResponderTests.RespondAsync([new RetryRoute()], state, Request + Request + Request);
        state.Reset();
        Assert.AreEqual(Later, await DifferentialHttpResponderTests.RespondAsync([new RetryRoute()], state, Request));
    }
}
