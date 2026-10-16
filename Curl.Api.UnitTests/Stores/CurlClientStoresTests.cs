using Curl.Api.EngineContracts;

namespace Curl.Api.Stores;

[TestClass]
public sealed class CurlClientStoresTests
{
    private const string Cookie = "example.com\tFALSE\t/\tFALSE\t0\tid\t42";

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void CreateEasy_BeforeTheStoresAreUsed_HandsOutTheEnginesHandle()
    {
        StoreEngine engine = new();
        CurlClient client = new(engine);

        IEasyTransfer easy = client.CreateEasy();

        Assert.AreSame(engine.CreatedEasyHandles.Single(), easy);
        Assert.IsEmpty(engine.CreatedShares);
    }

    [TestMethod]
    public void Stores_NoneInOptions_MakesTheClientsOwnOnceOverItsEngine()
    {
        StoreEngine engine = new();
        CurlClient client = new(engine);

        CurlStores first = client.Stores;
        CurlStores second = client.Stores;

        Assert.AreSame(first, second);
        Assert.HasCount(1, engine.CreatedShares);
        Assert.AreSame(first.Cookies, client.Cookies);
        Assert.AreSame(first.Hsts, client.Hsts);
        Assert.AreSame(first.AltSvc, client.AltSvc);
    }

    [TestMethod]
    public void Stores_MadeWhileAnotherCallMakesThem_KeepsTheFirstAndDisposesTheOther()
    {
        StoreEngine engine = new();
        CurlClient client = new(engine);
        CurlStores? inner = null;
        engine.OnCreateShare = () => inner = client.Stores;

        CurlStores outer = client.Stores;

        Assert.AreSame(inner, outer);
        Assert.HasCount(2, engine.CreatedShares);
        Assert.AreEqual(1, engine.CreatedShares[0].CleanupCount);
        Assert.AreEqual(0, engine.CreatedShares[1].CleanupCount);
    }

    [TestMethod]
    public async Task CreateEasy_AfterTheStoresAreUsed_BindsTheHandleToThem()
    {
        StoreEngine engine = new();
        CurlClient client = new(engine);
        client.Cookies.Add(Cookie);

        await using IEasyTransfer easy = client.CreateEasy();
        await easy.PerformAsync(TestContext.CancellationToken);

        Assert.IsInstanceOfType<StoreBoundEasyTransfer>(easy);
        CollectionAssert.AreEqual(new[] { "ALL", Cookie }, engine.CreatedEasyHandles.Single().PerformedOptions!.Http.CookieList.ToArray());
    }

    [TestMethod]
    public async Task SendAsync_RequestFailsBeforeItsTransfer_DisposesTheBoundHandleAndKeepsTheCookies()
    {
        StoreEngine engine = new() { HandlesYieldOnDispose = true };
        await using CurlClient client = new(engine);
        client.Cookies.Add(Cookie);
        CurlRequest request = new(HttpMethod.Post, "https://example.com/") { Body = new CurlMultipartContent(CurlMimePart.Text("a", "1")) };

        await Assert.ThrowsExactlyAsync<NotSupportedException>(() => client.SendAsync(request, TestContext.CancellationToken));

        StoreEasyTransfer easy = engine.CreatedEasyHandles.Single();
        Assert.AreEqual(1, easy.DisposeCount);
        Assert.IsNull(easy.PerformedOptions);
        CollectionAssert.AreEqual(new[] { Cookie }, client.Cookies.GetLines().ToArray());
    }

    [TestMethod]
    public async Task TwoClients_NamingTheSameStores_ShareCookiesThroughOneShareHandle()
    {
        StoreEngine engine = new();
        using CurlStores stores = new(engine);
        CurlClient first = new(engine, new CurlClientOptions { Stores = stores }, ownsEngine: false);
        CurlClient second = new(engine, new CurlClientOptions { Stores = stores }, ownsEngine: false);

        await using (IEasyTransfer easy = first.CreateEasy())
        {
            await easy.PerformAsync(TestContext.CancellationToken);
            engine.CreatedEasyHandles[0].Info = new() { CookieList = [Cookie] };
        }

        await using IEasyTransfer next = second.CreateEasy();
        await next.PerformAsync(TestContext.CancellationToken);

        Assert.AreSame(stores, second.Stores);
        Assert.AreSame(first.Cookies, second.Cookies);
        Assert.HasCount(1, engine.CreatedShares);
        CollectionAssert.AreEqual(new[] { "ALL", Cookie }, engine.CreatedEasyHandles[1].PerformedOptions!.Http.CookieList.ToArray());
        Assert.AreSame(engine.CreatedShares[0], engine.CreatedEasyHandles[1].PerformedOptions!.Behaviour.Share);
    }

    [TestMethod]
    public async Task DisposeAsync_DisposesTheClientsOwnStoresButNotSharedOnes()
    {
        StoreEngine engine = new();
        CurlStores shared = new(engine);
        CurlClient owning = new(engine, new CurlClientOptions(), ownsEngine: false);
        CurlClient sharing = new(engine, new CurlClientOptions { Stores = shared }, ownsEngine: false);
        _ = owning.Stores;
        _ = sharing.Stores;

        await owning.DisposeAsync();
        await sharing.DisposeAsync();

        Assert.AreEqual(0, engine.CreatedShares[0].CleanupCount);
        Assert.AreEqual(1, engine.CreatedShares[1].CleanupCount);
        Assert.AreEqual(0, engine.DisposeCount);
        shared.Dispose();
    }

    [TestMethod]
    public async Task DisposeAsync_OwnedEngine_DisposesStoresAndEngineOnce()
    {
        StoreEngine engine = new();
        CurlClient client = new(engine);
        _ = client.Stores;

        await client.DisposeAsync();
        await client.DisposeAsync();

        Assert.AreEqual(1, engine.CreatedShares.Single().CleanupCount);
        Assert.AreEqual(1, engine.DisposeCount);
    }

    [TestMethod]
    public async Task Stores_AfterDispose_Throws()
    {
        CurlClient client = new(new StoreEngine());
        await client.DisposeAsync();

        Assert.ThrowsExactly<ObjectDisposedException>(() => client.Stores);
    }
}
