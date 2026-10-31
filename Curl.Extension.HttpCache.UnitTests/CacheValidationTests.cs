namespace Curl.Extension.HttpCache.UnitTests;

[TestClass]
public sealed class CacheValidationTests
{
    private static readonly DateTimeOffset Time = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private static KeyValuePair<string, string>[] Headers(params string[] namesAndValues) =>
        [.. namesAndValues.Chunk(2).Select(pair => new KeyValuePair<string, string>(pair[0], pair[1]))];

    private static StoredResponse Stored(params string[] namesAndValues) =>
        new("HTTP/1.1 200 OK", 200, Headers(namesAndValues), [1, 2], Time, Time);

    [TestMethod]
    public void Section4_3_1_BothValidators_BuildIfNoneMatchAndIfModifiedSince() =>
        CollectionAssert.AreEqual(
            Headers("If-None-Match", "\"v1\"", "If-Modified-Since", "Thu, 29 Oct 2026 02:00:00 GMT"),
            CacheValidation.BuildConditionalHeaders(Stored("etag", "\"v1\"", "Last-Modified", "Thu, 29 Oct 2026 02:00:00 GMT")).ToArray());

    [TestMethod]
    public void Section4_3_1_NoValidator_BuildsNoHeader() =>
        Assert.AreEqual(0, CacheValidation.BuildConditionalHeaders(Stored("Content-Type", "text/plain")).Count);

    [TestMethod]
    [DataRow("\"v1\"")]
    [DataRow("W/\"v1\"")]
    public void Section4_3_4_MatchingEntityTag_SelectsTheStoredResponse(string entityTag) =>
        Assert.IsTrue(CacheValidation.SelectsStoredResponse(Stored("ETag", "W/\"v1\""), Headers("ETag", entityTag)));

    [TestMethod]
    public void Section4_3_4_DifferentEntityTag_DoesNotSelect() =>
        Assert.IsFalse(CacheValidation.SelectsStoredResponse(Stored("ETag", "\"v1\""), Headers("ETag", "\"v2\"")));

    [TestMethod]
    public void Section4_3_4_EntityTagWhenStoredHasNone_DoesNotSelect() =>
        Assert.IsFalse(CacheValidation.SelectsStoredResponse(Stored(), Headers("ETag", "\"v1\"")));

    [TestMethod]
    public void Section4_3_4_SameLastModified_Selects() =>
        Assert.IsTrue(CacheValidation.SelectsStoredResponse(Stored("Last-Modified", "Thu, 29 Oct 2026 02:00:00 GMT"), Headers("Last-Modified", "Thu, 29 Oct 2026 02:00:00 GMT")));

    [TestMethod]
    public void Section4_3_4_DifferentLastModified_DoesNotSelect() =>
        Assert.IsFalse(CacheValidation.SelectsStoredResponse(Stored("Last-Modified", "Thu, 29 Oct 2026 02:00:00 GMT"), Headers("Last-Modified", "Thu, 29 Oct 2026 03:00:00 GMT")));

    [TestMethod]
    public void Section4_3_4_NoValidatorAnywhere_Selects() =>
        Assert.IsTrue(CacheValidation.SelectsStoredResponse(Stored(), Headers("Date", "x")));

    [TestMethod]
    [DataRow("ETag", "\"v1\"")]
    [DataRow("Last-Modified", "Thu, 29 Oct 2026 02:00:00 GMT")]
    public void Section4_3_4_NoValidatorInTheResponseButOneStored_DoesNotSelect(string name, string value) =>
        Assert.IsFalse(CacheValidation.SelectsStoredResponse(Stored(name, value), Headers()));

    [TestMethod]
    public void Section4_3_4_NotModified_ReplacesStoredFieldsAndKeepsTheRest()
    {
        var stored = Stored("Content-Type", "text/plain", "Cache-Control", "max-age=1", "cache-control", "public", "Content-Length", "2", "X-Old", "a");
        var updated = CacheValidation.ApplyNotModified(
            stored,
            Headers("Cache-Control", "max-age=60", "Content-Length", "0", "Connection", "close, X-Hop", "X-Hop", "h", "Keep-Alive", "timeout=5", "ETag", "\"v2\""),
            Time.AddHours(1),
            Time.AddHours(2));

        CollectionAssert.AreEqual(
            Headers("Content-Type", "text/plain", "Content-Length", "2", "X-Old", "a", "Cache-Control", "max-age=60", "ETag", "\"v2\""),
            updated.Headers.ToArray());
        Assert.AreEqual(Time.AddHours(1), updated.RequestTime);
        Assert.AreEqual(Time.AddHours(2), updated.ResponseTime);
        Assert.AreSame(stored.Body, updated.Body);
    }

    [TestMethod]
    public void Section4_3_4_NotModifiedWithoutConnection_UpdatesEveryOtherField() =>
        CollectionAssert.AreEqual(
            Headers("Date", "new"),
            CacheValidation.ApplyNotModified(Stored("Date", "old"), Headers("Date", "new"), Time, Time).Headers.ToArray());
}
