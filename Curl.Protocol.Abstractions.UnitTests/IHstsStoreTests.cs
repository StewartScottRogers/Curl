using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the <see cref="IHstsStore" /> seam: <see cref="HttpRequestOptions.HstsStore" /> is unset by
/// default and carries the store it is given (ADR-0409).
/// </summary>
[TestClass]
public sealed class IHstsStoreTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void HstsStore_NothingSet_IsNull()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("options", "new HttpRequestOptions()");

        IHstsStore? store = new HttpRequestOptions().HstsStore;

        diagnostics.Act("hsts store", store);
        diagnostics.Assert("hsts store", null, store);
        Assert.IsNull(new HttpRequestOptions().HstsStore);
    }

    [TestMethod]
    public void HstsStore_Set_ReadsBackTheStore()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        RefusingHstsStore store = new();
        diagnostics.Arrange("store", store.GetType().Name);
        diagnostics.Arrange("header value", "max-age=abc");

        HttpRequestOptions options = new() { HstsStore = store };

        bool stored = options.HstsStore.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "max-age=abc", DateTimeOffset.UnixEpoch);
        diagnostics.Act("stored", stored);
        diagnostics.Assert("hsts store", store, options.HstsStore);
        diagnostics.Assert("stored", false, stored);
        Assert.AreSame(store, options.HstsStore);
        Assert.IsFalse(options.HstsStore.StoreFromResponse(CurlUrl.Parse("https://h.test/"), "max-age=abc", DateTimeOffset.UnixEpoch));
    }

    private sealed class RefusingHstsStore : IHstsStore
    {
        public bool StoreFromResponse(CurlUrl origin, string headerValue, DateTimeOffset now) => false;
    }
}
