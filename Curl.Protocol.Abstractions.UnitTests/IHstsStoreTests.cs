namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the <see cref="IHstsStore" /> seam: <see cref="HttpRequestOptions.HstsStore" /> is unset by
/// default and carries the store it is given (ADR-0409).
/// </summary>
[TestClass]
public sealed class IHstsStoreTests
{
    [TestMethod]
    public void HstsStore_NothingSet_IsNull() =>
        Assert.IsNull(new HttpRequestOptions().HstsStore);

    [TestMethod]
    public void HstsStore_Set_ReadsBackTheStore()
    {
        RefusingHstsStore store = new();

        HttpRequestOptions options = new() { HstsStore = store };

        Assert.AreSame(store, options.HstsStore);
        Assert.IsFalse(options.HstsStore.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "max-age=abc", DateTimeOffset.UnixEpoch));
    }

    private sealed class RefusingHstsStore : IHstsStore
    {
        public bool StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now) => false;
    }
}
