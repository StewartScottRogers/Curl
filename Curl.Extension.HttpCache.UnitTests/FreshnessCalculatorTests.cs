namespace Curl.Extension.HttpCache.UnitTests;

[TestClass]
public sealed class FreshnessCalculatorTests
{
    private static readonly DateTimeOffset RequestTime = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset ResponseTime = RequestTime.AddSeconds(2);

    private static StoredResponse Response(int statusCode, params string[] namesAndValues) =>
        new($"HTTP/1.1 {statusCode} X", statusCode, [.. namesAndValues.Chunk(2).Select(pair => new KeyValuePair<string, string>(pair[0], pair[1]))], [], RequestTime, ResponseTime);

    private static FreshnessCalculator Calculator(bool isSharedCache = false, double secondsAfterResponse = 0) =>
        new(new FixedTimeProvider(ResponseTime.AddSeconds(secondsAfterResponse)), isSharedCache);

    [TestMethod]
    public void Section4_2_1_MaxAge_IsTheLifetime() =>
        Assert.AreEqual(TimeSpan.FromSeconds(60), Calculator().FreshnessLifetime(Response(200, "Cache-Control", "max-age=60, s-maxage=600")));

    [TestMethod]
    public void Section4_2_1_SMaxAgeInSharedCache_WinsOverMaxAge() =>
        Assert.AreEqual(TimeSpan.FromSeconds(600), Calculator(isSharedCache: true).FreshnessLifetime(Response(200, "Cache-Control", "max-age=60, s-maxage=\"600\"")));

    [TestMethod]
    public void Section4_2_1_SharedCacheWithoutSMaxAge_UsesMaxAge() =>
        Assert.AreEqual(TimeSpan.FromSeconds(60), Calculator(isSharedCache: true).FreshnessLifetime(Response(200, "Cache-Control", "max-age = 60")));

    [TestMethod]
    public void Section4_2_1_ExpiresMinusDate_IsTheLifetime() =>
        Assert.AreEqual(
            TimeSpan.FromSeconds(90),
            Calculator().FreshnessLifetime(Response(200, "Date", "Thu, 29 Oct 2026 12:00:00 GMT", "Expires", "Thu, 29 Oct 2026 12:01:30 GMT")));

    [TestMethod]
    public void Section4_2_1_ExpiresWithoutDate_CountsFromTheResponseTime() =>
        Assert.AreEqual(TimeSpan.FromSeconds(8), Calculator().FreshnessLifetime(Response(200, "Expires", "Thursday, 29-Oct-26 12:00:10 GMT")));

    [TestMethod]
    public void Section4_2_1_ExpiresBeforeDate_IsZero() =>
        Assert.AreEqual(
            TimeSpan.Zero,
            Calculator().FreshnessLifetime(Response(200, "Date", "Thu, 29 Oct 2026 12:00:00 GMT", "Expires", "Thu Oct 29 11:00:00 2026")));

    [TestMethod]
    public void Section4_2_1_InvalidExpires_IsZero() =>
        Assert.AreEqual(TimeSpan.Zero, Calculator().FreshnessLifetime(Response(200, "Expires", "0", "Last-Modified", "Thu, 01 Oct 2026 12:00:00 GMT")));

    [TestMethod]
    public void Section4_2_1_InvalidMaxAge_FallsBackToExpires() =>
        Assert.AreEqual(
            TimeSpan.FromSeconds(30),
            Calculator().FreshnessLifetime(Response(200, "Cache-Control", "max-age=-1", "Date", "Thu, 29 Oct 2026 12:00:00 GMT", "Expires", "Thu, 29 Oct 2026 12:00:30 GMT")));

    [TestMethod]
    public void Section4_2_2_HeuristicIsATenthOfTheTimeSinceLastModified() =>
        Assert.AreEqual(
            TimeSpan.FromHours(1),
            Calculator().FreshnessLifetime(Response(200, "Date", "Thu, 29 Oct 2026 12:00:00 GMT", "Last-Modified", "Thu, 29 Oct 2026 02:00:00 GMT")));

    [TestMethod]
    public void Section4_2_2_HeuristicWithPublic_AppliesToAnyStatus() =>
        Assert.AreEqual(
            TimeSpan.FromHours(1),
            Calculator().FreshnessLifetime(Response(302, "Cache-Control", "public", "Date", "Thu, 29 Oct 2026 12:00:00 GMT", "Last-Modified", "Thu, 29 Oct 2026 02:00:00 GMT")));

    [TestMethod]
    public void Section4_2_2_HeuristicForStatusNotHeuristicallyCacheable_IsZero() =>
        Assert.AreEqual(TimeSpan.Zero, Calculator().FreshnessLifetime(Response(302, "Last-Modified", "Thu, 29 Oct 2026 02:00:00 GMT")));

    [TestMethod]
    public void Section4_2_2_WithoutLastModified_IsZero() =>
        Assert.AreEqual(TimeSpan.Zero, Calculator().FreshnessLifetime(Response(200)));

    [TestMethod]
    public void Section4_2_2_LastModifiedInTheFuture_IsZero() =>
        Assert.AreEqual(TimeSpan.Zero, Calculator().FreshnessLifetime(Response(200, "Date", "Thu, 29 Oct 2026 12:00:00 GMT", "Last-Modified", "Fri, 30 Oct 2026 12:00:00 GMT")));

    [TestMethod]
    public void Section4_2_3_AgeAddsTheResponseDelayAndResidentTime() =>
        Assert.AreEqual(TimeSpan.FromSeconds(100 + 2 + 30), Calculator(secondsAfterResponse: 30).CurrentAge(Response(200, "Age", "100")));

    [TestMethod]
    public void Section4_2_3_ApparentAgeFromDate_WinsWhenLarger() =>
        Assert.AreEqual(
            TimeSpan.FromSeconds(62 + 5),
            Calculator(secondsAfterResponse: 5).CurrentAge(Response(200, "Date", "Thu, 29 Oct 2026 11:59:00 GMT", "Age", "1")));

    [TestMethod]
    public void Section4_2_3_InvalidAgeAndDateInTheFuture_CountOnlyDelayAndResidentTime() =>
        Assert.AreEqual(
            TimeSpan.FromSeconds(2 + 1),
            Calculator(secondsAfterResponse: 1).CurrentAge(Response(200, "Date", "Thu, 29 Oct 2026 13:00:00 GMT", "Age", "1x")));

    [TestMethod]
    public void Section4_2_FreshResponse_IsFresh() =>
        Assert.AreEqual(CacheEntryUsability.Fresh, Calculator(secondsAfterResponse: 10).Evaluate(Response(200, "Cache-Control", "max-age=60")));

    [TestMethod]
    public void Section4_2_StaleResponse_IsStale() =>
        Assert.AreEqual(CacheEntryUsability.Stale, Calculator(secondsAfterResponse: 60).Evaluate(Response(200, "Cache-Control", "max-age=60")));

    [TestMethod]
    public void Section5_2_2_4_NoCache_MustValidateEvenWhenFresh() =>
        Assert.AreEqual(CacheEntryUsability.MustValidate, Calculator().Evaluate(Response(200, "Cache-Control", "max-age=60, no-cache=\"Set-Cookie\"")));

    [TestMethod]
    public void Section5_2_2_2_MustRevalidate_MustValidateWhenStale() =>
        Assert.AreEqual(CacheEntryUsability.MustValidate, Calculator(secondsAfterResponse: 60).Evaluate(Response(200, "Cache-Control", "max-age=60, must-revalidate")));

    [TestMethod]
    public void Section5_2_2_8_ProxyRevalidateInSharedCache_MustValidateWhenStale() =>
        Assert.AreEqual(CacheEntryUsability.MustValidate, Calculator(isSharedCache: true, secondsAfterResponse: 60).Evaluate(Response(200, "Cache-Control", "max-age=60, proxy-revalidate")));

    [TestMethod]
    public void Section5_2_2_8_ProxyRevalidateInPrivateCache_IsStaleWhenStale() =>
        Assert.AreEqual(CacheEntryUsability.Stale, Calculator(secondsAfterResponse: 60).Evaluate(Response(200, "Cache-Control", "max-age=60, proxy-revalidate")));

    [TestMethod]
    public void Section1_2_2_DeltaSecondsTooLarge_ReadsAsTheGreatestValue()
    {
        Assert.IsTrue(DeltaSeconds.TryParse("99999999999999999999999", out var seconds));
        Assert.AreEqual(TimeSpan.FromSeconds(DeltaSeconds.Greatest), seconds);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    public void Section1_2_2_DeltaSecondsMissing_DoesNotParse(string? value) =>
        Assert.IsFalse(DeltaSeconds.TryParse(value, out _));
}
