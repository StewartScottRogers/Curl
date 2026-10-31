using Curl.Extension.OAuth2.Cache;
using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;
using Curl.Protocol.Abstractions;

namespace Curl.Extension.OAuth2.Flows;

/// <summary>Pins the order a token is got in (ADR-1045): the cache first, a refresh through it, and only then the flow.</summary>
[TestClass]
public sealed class OAuth2CachedTokenSourceTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly OAuth2FlowRequest Request = new(
        new Uri("https://server.example.com/token"), new OAuth2Client("s6BhdRkqt3", null, OAuth2ClientAuthentication.Basic), "read");

    private static readonly OAuth2TokenResponse Issued = new("2YotnFZFEjr1zCsicMWpAA", "Bearer", 3600, "tGzv3JOkF0XG5Qx2TlKWIA", "read");

    [TestMethod]
    public async Task GetTokenAsync_NothingCached_RunsTheFlowAndCachesItsToken()
    {
        using var directory = new CacheTestDirectory();
        var source = new OAuth2CachedTokenSource(NewCache(directory, new RecordedConnection(null), new SetTimeProvider(Start)));
        var flow = new CountingFlow(OAuth2FlowResult.Issued(Issued));

        var first = await source.GetTokenAsync(Request, flow, CancellationToken.None);
        var second = await source.GetTokenAsync(Request, flow, CancellationToken.None);

        Assert.AreSame(Issued, first.Token);
        Assert.AreEqual(new OAuth2TokenResponse("2YotnFZFEjr1zCsicMWpAA", "Bearer", null, "tGzv3JOkF0XG5Qx2TlKWIA", "read"), second.Token);
        Assert.AreEqual(1, flow.Runs);
    }

    [TestMethod]
    public async Task GetTokenAsync_ExpiredTokenRefreshes_DoesNotRunTheFlow()
    {
        using var directory = new CacheTestDirectory();
        var time = new SetTimeProvider(Start);
        var connection = new RecordedConnection(FlowResponses.Token("refreshed"));
        var cache = NewCache(directory, connection, time);
        await cache.StoreAsync(Request.CacheKey, Issued, CancellationToken.None);
        time.Now = Start.AddHours(2);
        var flow = new CountingFlow(OAuth2FlowResult.Issued(Issued));

        var result = await new OAuth2CachedTokenSource(cache).GetTokenAsync(Request, flow, CancellationToken.None);

        Assert.AreEqual("refreshed", result.Token?.AccessToken);
        StringAssert.Contains(connection.Written, "grant_type=refresh_token&refresh_token=tGzv3JOkF0XG5Qx2TlKWIA");
        Assert.AreEqual(0, flow.Runs);
    }

    [TestMethod]
    public async Task GetTokenAsync_FlowFails_GivesTheFailureAndCachesNothing()
    {
        using var directory = new CacheTestDirectory();
        var source = new OAuth2CachedTokenSource(NewCache(directory, new RecordedConnection(null), new SetTimeProvider(Start)));
        var flow = new CountingFlow(OAuth2FlowResult.Failed(CurlExitCode.LoginDenied, "denied"));

        var first = await source.GetTokenAsync(Request, flow, CancellationToken.None);
        await source.GetTokenAsync(Request, flow, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.LoginDenied, first.ExitCode);
        Assert.AreEqual(2, flow.Runs);
    }

    [TestMethod]
    public async Task GetTokenAsync_NullArguments_Throw()
    {
        using var directory = new CacheTestDirectory();
        var source = new OAuth2CachedTokenSource(NewCache(directory, new RecordedConnection(null), new SetTimeProvider(Start)));
        var flow = new CountingFlow(OAuth2FlowResult.Issued(Issued));

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => source.GetTokenAsync(null!, flow, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => source.GetTokenAsync(Request, null!, CancellationToken.None));
    }

    private static OAuth2TokenCache NewCache(CacheTestDirectory directory, RecordedConnection connection, TimeProvider time) =>
        new(new OAuth2TokenCacheFile(directory.Path), new OAuth2HttpClient(new RecordedConnector(connection)), time);
}
