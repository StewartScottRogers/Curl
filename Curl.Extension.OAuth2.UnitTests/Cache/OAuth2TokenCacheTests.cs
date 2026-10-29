using System.Text;
using Curl.Extension.OAuth2.Http;
using Curl.Extension.OAuth2.Wire;

namespace Curl.Extension.OAuth2.Cache;

/// <summary>
/// Pins the token cache (ADR-1034): storing and reading, expiry by <c>expires_in</c> (RFC 6749 §5.1) less the safety
/// margin by a set <see cref="TimeProvider"/>, and refresh (§6) with its failures.
/// </summary>
[TestClass]
public sealed class OAuth2TokenCacheTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static readonly OAuth2TokenCacheKey Key = new(new Uri("https://server.example.com/token"), "s6BhdRkqt3", "read");

    private static readonly OAuth2Client Client = new("s6BhdRkqt3", null, OAuth2ClientAuthentication.Basic);

    private static readonly OAuth2TokenResponse Issued = new("2YotnFZFEjr1zCsicMWpAA", "Bearer", 3600, "tGzv3JOkF0XG5Qx2TlKWIA", null);

    [TestMethod]
    public async Task StoreAsync_ThenFindAsync_GivesBackTheUnexpiredTokenWithoutARequest()
    {
        using var directory = new CacheTestDirectory();
        var connection = new RecordedConnection(null);
        var cache = NewCache(directory, connection, new SetTimeProvider(Start));

        Assert.IsTrue(await cache.StoreAsync(Key, Issued, CancellationToken.None));
        var found = await cache.FindAsync(Key, Client, CancellationToken.None);

        Assert.AreEqual(new OAuth2CachedToken("2YotnFZFEjr1zCsicMWpAA", "Bearer", Start.AddHours(1), "tGzv3JOkF0XG5Qx2TlKWIA", null), found);
        Assert.AreEqual(string.Empty, connection.Written);
    }

    [TestMethod]
    public async Task FindAsync_OtherScopeOrClient_FindsNothing()
    {
        using var directory = new CacheTestDirectory();
        var cache = NewCache(directory, new RecordedConnection(null), new SetTimeProvider(Start));
        await cache.StoreAsync(Key, Issued, CancellationToken.None);

        Assert.IsNull(await cache.FindAsync(Key with { Scope = "write" }, Client, CancellationToken.None));
        Assert.IsNull(await cache.FindAsync(Key with { ClientId = "other" }, Client, CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_JustInsideTheSafetyMargin_IsStillUsable()
    {
        using var directory = new CacheTestDirectory();
        var time = new SetTimeProvider(Start);
        var cache = NewCache(directory, new RecordedConnection(null), time);
        await cache.StoreAsync(Key, Issued, CancellationToken.None);

        time.Now = Start.AddHours(1) - OAuth2TokenCache.ExpirySafetyMargin - TimeSpan.FromSeconds(1);

        Assert.AreEqual("2YotnFZFEjr1zCsicMWpAA", (await cache.FindAsync(Key, Client, CancellationToken.None))?.AccessToken);
    }

    [TestMethod]
    public async Task FindAsync_NoExpiresIn_NeverExpires()
    {
        using var directory = new CacheTestDirectory();
        var time = new SetTimeProvider(Start);
        var cache = NewCache(directory, new RecordedConnection(null), time);
        await cache.StoreAsync(Key, Issued with { ExpiresIn = null }, CancellationToken.None);

        time.Now = Start.AddYears(5);

        Assert.IsNull((await cache.FindAsync(Key, Client, CancellationToken.None))?.ExpiresAt);
    }

    [TestMethod]
    public async Task FindAsync_ExpiredWithRefreshToken_RefreshesAndKeepsTheNewRefreshToken()
    {
        using var directory = new CacheTestDirectory();
        var time = new SetTimeProvider(Start);
        var connection = Respond(200, """{"access_token":"new-access","token_type":"Bearer","expires_in":600,"refresh_token":"new-refresh"}""");
        var cache = NewCache(directory, connection, time);
        await cache.StoreAsync(Key, Issued, CancellationToken.None);
        time.Now = Start.AddHours(1) - OAuth2TokenCache.ExpirySafetyMargin;

        var found = await cache.FindAsync(Key, Client, CancellationToken.None);

        var expected = new OAuth2CachedToken("new-access", "Bearer", time.Now.AddSeconds(600), "new-refresh", null);
        Assert.AreEqual(expected, found);
        Assert.EndsWith("\r\n\r\ngrant_type=refresh_token&refresh_token=tGzv3JOkF0XG5Qx2TlKWIA&scope=read&client_id=s6BhdRkqt3", connection.Written);
        Assert.AreEqual(expected, (await new OAuth2TokenCacheFile(directory.Path).ReadAsync(CancellationToken.None))[Key]);
    }

    [TestMethod]
    public async Task FindAsync_RefreshResponseWithoutRefreshToken_KeepsTheOldOne()
    {
        using var directory = new CacheTestDirectory();
        var time = new SetTimeProvider(Start);
        var cache = NewCache(directory, Respond(200, """{"access_token":"new-access","token_type":"Bearer"}"""), time);
        await cache.StoreAsync(Key, Issued, CancellationToken.None);
        time.Now = Start.AddDays(1);

        var found = await cache.FindAsync(Key, Client, CancellationToken.None);

        Assert.AreEqual(new OAuth2CachedToken("new-access", "Bearer", null, "tGzv3JOkF0XG5Qx2TlKWIA", null), found);
    }

    [TestMethod]
    public async Task FindAsync_RefreshFailsWithInvalidGrant_DropsTheEntry()
    {
        using var directory = new CacheTestDirectory();
        var time = new SetTimeProvider(Start);
        var cache = NewCache(directory, Respond(400, """{"error":"invalid_grant"}"""), time);
        await cache.StoreAsync(Key, Issued, CancellationToken.None);
        time.Now = Start.AddDays(1);

        Assert.IsNull(await cache.FindAsync(Key, Client, CancellationToken.None));
        Assert.IsEmpty(await new OAuth2TokenCacheFile(directory.Path).ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    [DataRow(400, """{"error":"invalid_client"}""")]
    [DataRow(200, "not json")]
    [DataRow(503, "")]
    [DataRow(null, null)]
    public async Task FindAsync_RefreshFailsOtherwise_FindsNothingAndKeepsTheEntry(int? status, string? body)
    {
        using var directory = new CacheTestDirectory();
        var time = new SetTimeProvider(Start);
        var cache = NewCache(directory, status is { } code ? Respond(code, body!) : new RecordedConnection(null), time);
        await cache.StoreAsync(Key, Issued, CancellationToken.None);
        time.Now = Start.AddDays(1);

        Assert.IsNull(await cache.FindAsync(Key, Client, CancellationToken.None));
        Assert.HasCount(1, await new OAuth2TokenCacheFile(directory.Path).ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task FindAsync_ExpiredWithoutRefreshToken_DropsTheEntryWithoutARequest()
    {
        using var directory = new CacheTestDirectory();
        var time = new SetTimeProvider(Start);
        var connection = new RecordedConnection(null);
        var cache = NewCache(directory, connection, time);
        await cache.StoreAsync(Key, Issued with { RefreshToken = null, ExpiresIn = -5 }, CancellationToken.None);

        Assert.IsNull(await cache.FindAsync(Key, Client, CancellationToken.None));
        Assert.IsEmpty(await new OAuth2TokenCacheFile(directory.Path).ReadAsync(CancellationToken.None));
        Assert.AreEqual(string.Empty, connection.Written);
    }

    [TestMethod]
    public async Task StoreAsync_AbsurdExpiresIn_IsCappedRatherThanOverflowing()
    {
        using var directory = new CacheTestDirectory();
        var cache = NewCache(directory, new RecordedConnection(null), new SetTimeProvider(Start));

        await cache.StoreAsync(Key, Issued with { ExpiresIn = long.MaxValue }, CancellationToken.None);

        Assert.IsGreaterThan(Start.AddYears(99), (await cache.FindAsync(Key, Client, CancellationToken.None))!.ExpiresAt!.Value);
    }

    [TestMethod]
    public async Task RemoveAsync_DropsTheEntryOnlyWhenThere()
    {
        using var directory = new CacheTestDirectory();
        var cache = NewCache(directory, new RecordedConnection(null), new SetTimeProvider(Start));
        await cache.StoreAsync(Key, Issued, CancellationToken.None);

        Assert.IsTrue(await cache.RemoveAsync(Key, CancellationToken.None));
        Assert.IsFalse(await cache.RemoveAsync(Key, CancellationToken.None));
        Assert.IsNull(await cache.FindAsync(Key, Client, CancellationToken.None));
    }

    private static OAuth2TokenCache NewCache(CacheTestDirectory directory, RecordedConnection connection, TimeProvider time) =>
        new(new OAuth2TokenCacheFile(directory.Path), new OAuth2HttpClient(new RecordedConnector(connection)), time);

    private static RecordedConnection Respond(int status, string body) =>
        new($"HTTP/1.1 {status} Status\r\nContent-Type: application/json\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\n\r\n{body}");
}
