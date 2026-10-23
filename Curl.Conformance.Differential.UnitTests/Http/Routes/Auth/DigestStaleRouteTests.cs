using Curl.Conformance.Differential.Http.Core;
using Curl.Conformance.Differential.Http.Routes.Auth;
using Curl.Conformance.Differential.UnitTests.Http.Core;

namespace Curl.Conformance.Differential.UnitTests.Http.Routes.Auth;

/// <summary>Pins the response bytes of server.py's <c>/digeststale</c> and its per-run counter.</summary>
[TestClass]
public sealed class DigestStaleRouteTests
{
    private const string Date = "Date: Thu, 01 Jan 2026 00:00:00 GMT\r\n";
    private const string Digest = "GET /digeststale HTTP/1.1\r\nAuthorization: Digest response=\"x\"\r\n\r\n";

    [TestMethod]
    public async Task RespondAsync_ThreeDigestAnswers_ChallengesThenStaleThenOk()
    {
        Assert.AreEqual(
            Challenge(1, string.Empty) + Challenge(2, ", stale=true") + $"HTTP/1.1 200 OK\r\n{Date}Content-Length: 9\r\n\r\nstale ok\n",
            await DifferentialHttpResponderTests.RespondAsync([new DigestStaleRoute()], new DifferentialResponderState(), Digest + Digest + Digest));
    }

    [TestMethod]
    public async Task RespondAsync_ThirdRequestWithoutDigest_ChallengesAgain()
    {
        Assert.AreEqual(
            Challenge(1, string.Empty) + Challenge(2, ", stale=true") + Challenge(3, ", stale=true"),
            await DifferentialHttpResponderTests.RespondAsync(
                [new DigestStaleRoute()],
                new DifferentialResponderState(),
                Digest + Digest + "GET /digeststale HTTP/1.1\r\n\r\n"));
    }

    [TestMethod]
    public async Task RespondAsync_AfterReset_CountsFromOne()
    {
        var state = new DifferentialResponderState();
        await DifferentialHttpResponderTests.RespondAsync([new DigestStaleRoute()], state, Digest + Digest);
        state.Reset();
        Assert.AreEqual(
            Challenge(1, string.Empty),
            await DifferentialHttpResponderTests.RespondAsync([new DigestStaleRoute()], state, Digest));
    }

    private static string Challenge(int count, string stale) =>
        $"HTTP/1.1 401 Unauthorized\r\n{Date}WWW-Authenticate: Digest realm=\"r\", nonce=\"n{count}\", qop=\"auth\"{stale}\r\nContent-Length: 2\r\n\r\nd\n";
}
