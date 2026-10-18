using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.Share;
using Curl.Api.Errors;
using Curl.Api.Options;
using Curl.Api.Options.Http;

namespace Curl.Api.Stores;

[TestClass]
public sealed class CurlStoresTests
{
    private const string Cookie = "example.com\tFALSE\t/\tFALSE\t0\tid\t42";

    [TestMethod]
    public void Constructor_SharesCookiesAndHstsOnANewShareHandle()
    {
        StoreEngine engine = new();

        using CurlStores stores = new(engine);

        StoreShareHandle share = engine.CreatedShares.Single();
        CollectionAssert.AreEqual(new[] { CurlLockData.Cookie, CurlLockData.Hsts }, share.Shared);
        Assert.AreEqual(0, share.CleanupCount);
    }

    [TestMethod]
    [DataRow(CurlLockData.Cookie)]
    [DataRow(CurlLockData.Hsts)]
    public void Constructor_ShareRefused_CleansUpAndThrows(CurlLockData refused)
    {
        StoreEngine engine = new();
        engine.ShareAnswers[refused] = CurlShareCode.BadOption;

        CurlException exception = Assert.ThrowsExactly<CurlException>(() => new CurlStores(engine));

        Assert.AreEqual(CurlCode.FailedInit, exception.Code);
        Assert.AreEqual("curl_share_setopt failed: BadOption", exception.Message);
        Assert.AreEqual(1, engine.CreatedShares.Single().CleanupCount);
        Assert.AreEqual(refused, engine.CreatedShares.Single().Shared.Last());
    }

    [TestMethod]
    public void Constructor_NullEngine_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => new CurlStores(null!));
    }

    [TestMethod]
    public void Dispose_CleansUpTheShareHandleOnce()
    {
        StoreEngine engine = new();
        CurlStores stores = new(engine);

        stores.Dispose();
        stores.Dispose();

        Assert.AreEqual(1, engine.CreatedShares.Single().CleanupCount);
    }

    [TestMethod]
    public void ApplyTo_SetsTheShareCookiesHstsAndAltSvc()
    {
        StoreEngine engine = new();
        using CurlStores stores = new(engine);
        stores.Cookies.Add(Cookie);
        stores.AltSvc.Use("altsvc.txt", AltSvcControl.H3);

        EasyOptions options = stores.ApplyTo(new EasyOptions());

        Assert.AreSame(engine.CreatedShares.Single(), options.Behaviour.Share);
        CollectionAssert.AreEqual(new[] { "ALL", Cookie }, options.Http.CookieList.ToArray());
        Assert.AreEqual(HstsControl.Enable, options.Http.HstsControl);
        Assert.AreEqual("altsvc.txt", options.Http.AltSvc);
        Assert.IsNotNull(options.Callbacks.HstsRead);
        Assert.IsNotNull(options.Callbacks.HstsWrite);
    }

    [TestMethod]
    public void ApplyTo_AfterDispose_Throws()
    {
        CurlStores stores = new(new StoreEngine());
        stores.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => stores.ApplyTo(new EasyOptions()));
    }

    public TestContext TestContext { get; set; } = null!;
}
