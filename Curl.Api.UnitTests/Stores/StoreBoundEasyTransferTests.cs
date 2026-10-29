using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;

namespace Curl.Api.Stores;

[TestClass]
public sealed class StoreBoundEasyTransferTests
{
    private const string Cookie = "example.com\tFALSE\t/\tFALSE\t0\tid\t42";

    [TestMethod]
    public async Task PerformAsyncThenDisposeAsync_StartsFromTheStoresAndLeavesTheTransfersStateInThem()
    {
        StoreEngine engine = new();
        using CurlStores stores = new(engine);
        HstsEntry preloaded = new("old.example", false, null);
        HstsEntry learned = new("new.example", true, "20271231 00:00:00");
        stores.Hsts.Add(preloaded);
        StoreEasyTransfer inner = new();
        await using (IEasyTransfer easy = stores.Bind(inner))
        {
            easy.Options = easy.Options with { Http = easy.Options.Http with { UserAgent = "agent/1" } };

            Assert.AreEqual(CurlCode.Ok, await easy.PerformAsync(TestContext.CancellationToken));

            inner.Info = new() { CookieList = [Cookie] };
            inner.HstsCacheAtCleanup.AddRange([preloaded, learned]);
        }

        Assert.AreEqual("agent/1", inner.PerformedOptions!.Http.UserAgent);
        Assert.AreSame(engine.CreatedShares.Single(), inner.PerformedOptions.Behaviour.Share);
        CollectionAssert.AreEqual(new[] { preloaded }, inner.PreloadedHsts);
        CollectionAssert.AreEqual(new[] { Cookie }, stores.Cookies.GetLines().ToArray());
        CollectionAssert.AreEqual(new[] { preloaded, learned }, stores.Hsts.GetEntries().ToArray());
        Assert.AreEqual(1, inner.DisposeCount);
    }

    [TestMethod]
    public async Task DisposeAsync_WithoutPerform_LeavesTheCookiesAlone()
    {
        using CurlStores stores = new(new StoreEngine());
        stores.Cookies.Add(Cookie);
        StoreEasyTransfer inner = new() { Info = new() { CookieList = [] } };

        await stores.Bind(inner).DisposeAsync();

        CollectionAssert.AreEqual(new[] { Cookie }, stores.Cookies.GetLines().ToArray());
        Assert.AreEqual(1, inner.DisposeCount);
    }

    [TestMethod]
    public async Task OtherMembers_ForwardToTheWrappedHandle()
    {
        using CurlStores stores = new(new StoreEngine());
        StoreEasyTransfer inner = new() { Info = new() { EffectiveUrl = "https://example.com/" } };
        IEasyTransfer easy = stores.Bind(inner);

        Assert.AreSame(inner.Info, easy.Info);
        Assert.AreEqual("error text", easy.ErrorText);
        Assert.AreEqual("headers", Assert.ThrowsExactly<InvalidOperationException>(() => easy.Headers).Message);
        Assert.AreEqual("websocket", Assert.ThrowsExactly<InvalidOperationException>(() => easy.WebSocket).Message);
        Assert.AreEqual("connection", Assert.ThrowsExactly<InvalidOperationException>(() => easy.Connection).Message);
        easy.Reset();
        Assert.AreEqual(1, inner.ResetCount);
        Assert.AreEqual(CurlCode.Ok, easy.Pause(CurlPause.All));
        Assert.AreEqual(CurlPause.All, inner.PausedWith);
        Assert.AreEqual(CurlCode.Again, await easy.UpkeepAsync(TestContext.CancellationToken));
        Assert.AreEqual(1, inner.UpkeepCount);
        Assert.AreSame(inner, easy.PerformingTransfer);
    }

    [TestMethod]
    public async Task Duplicate_IsBoundToTheSameStores()
    {
        StoreEngine engine = new();
        using CurlStores stores = new(engine);
        StoreEasyTransfer original = new();
        IEasyTransfer easy = stores.Bind(original);

        IEasyTransfer duplicate = easy.Duplicate();
        await duplicate.PerformAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<StoreBoundEasyTransfer>(duplicate);
        StoreEasyTransfer inner = original.Duplicated!;
        Assert.AreSame(engine.CreatedShares.Single(), inner.PerformedOptions!.Behaviour.Share);
    }

    public TestContext TestContext { get; set; } = null!;
}
