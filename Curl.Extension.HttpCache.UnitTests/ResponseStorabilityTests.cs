namespace Curl.Extension.HttpCache.UnitTests;

[TestClass]
public sealed class ResponseStorabilityTests
{
    private static readonly ResponseStorability PrivateCache = new(isSharedCache: false);
    private static readonly ResponseStorability SharedCache = new(isSharedCache: true);

    private static KeyValuePair<string, string>[] Headers(params string[] namesAndValues) =>
        [.. namesAndValues.Chunk(2).Select(pair => new KeyValuePair<string, string>(pair[0], pair[1]))];

    [TestMethod]
    public void Section3_GetWithHeuristicallyCacheableStatus_IsStorable() =>
        Assert.IsTrue(PrivateCache.IsStorable("GET", 200, Headers(), Headers()));

    [TestMethod]
    public void Section3_MethodOtherThanGet_IsNotStorable() =>
        Assert.IsFalse(PrivateCache.IsStorable("POST", 200, Headers(), Headers("Cache-Control", "max-age=60")));

    [TestMethod]
    [DataRow(101)]
    [DataRow(206)]
    [DataRow(304)]
    [DataRow(600)]
    public void Section3_StatusNotFinalOrNotUnderstood_IsNotStorable(int statusCode) =>
        Assert.IsFalse(PrivateCache.IsStorable("GET", statusCode, Headers(), Headers("Cache-Control", "max-age=60")));

    [TestMethod]
    public void Section3_NoStoreInRequest_IsNotStorable() =>
        Assert.IsFalse(PrivateCache.IsStorable("GET", 200, Headers("Cache-Control", "no-store"), Headers()));

    [TestMethod]
    public void Section3_NoStoreInResponse_IsNotStorable() =>
        Assert.IsFalse(PrivateCache.IsStorable("GET", 200, Headers(), Headers("cache-control", "max-age=5, NO-STORE")));

    [TestMethod]
    public void Section3_StatusNotHeuristicallyCacheableWithoutExplicitFreshness_IsNotStorable() =>
        Assert.IsFalse(PrivateCache.IsStorable("GET", 302, Headers(), Headers()));

    [TestMethod]
    [DataRow("Expires", "Thu, 01 Jan 2026 00:00:00 GMT")]
    [DataRow("Cache-Control", "max-age=60")]
    [DataRow("Cache-Control", "public")]
    public void Section3_StatusNotHeuristicallyCacheableWithExplicitFreshness_IsStorable(string name, string value) =>
        Assert.IsTrue(PrivateCache.IsStorable("GET", 302, Headers(), Headers(name, value)));

    [TestMethod]
    public void Section3_SMaxAgeInPrivateCache_DoesNotMakeAResponseStorable() =>
        Assert.IsFalse(PrivateCache.IsStorable("GET", 302, Headers(), Headers("Cache-Control", "s-maxage=60")));

    [TestMethod]
    public void Section3_SMaxAgeInSharedCache_MakesAResponseStorable() =>
        Assert.IsTrue(SharedCache.IsStorable("GET", 302, Headers(), Headers("Cache-Control", "s-maxage=60")));

    [TestMethod]
    public void Section3_PrivateInPrivateCache_IsStorable() =>
        Assert.IsTrue(PrivateCache.IsStorable("GET", 200, Headers(), Headers("Cache-Control", "private")));

    [TestMethod]
    public void Section3_PrivateInSharedCache_IsNotStorable() =>
        Assert.IsFalse(SharedCache.IsStorable("GET", 200, Headers(), Headers("Cache-Control", "private")));

    [TestMethod]
    public void Section3_SharedCacheWithoutAuthorization_IsStorable() =>
        Assert.IsTrue(SharedCache.IsStorable("GET", 200, Headers(), Headers()));

    [TestMethod]
    public void Section3_5_AuthorizationInPrivateCache_IsStorable() =>
        Assert.IsTrue(PrivateCache.IsStorable("GET", 200, Headers("Authorization", "Basic eDp5"), Headers()));

    [TestMethod]
    public void Section3_5_AuthorizationInSharedCacheWithoutPermission_IsNotStorable() =>
        Assert.IsFalse(SharedCache.IsStorable("GET", 200, Headers("Authorization", "Basic eDp5"), Headers("Cache-Control", "max-age=60")));

    [TestMethod]
    [DataRow("must-revalidate")]
    [DataRow("public")]
    [DataRow("s-maxage=60")]
    public void Section3_5_AuthorizationInSharedCacheWithPermission_IsStorable(string directive) =>
        Assert.IsTrue(SharedCache.IsStorable("GET", 200, Headers("Authorization", "Basic eDp5"), Headers("Cache-Control", directive)));
}
