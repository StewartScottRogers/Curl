namespace Curl.Cookies;

/// <summary>
/// The part of <c>CookieStoreTests</c> that pins <see cref="CookieStore.RemoveAll"/> and
/// <see cref="CookieStore.RemoveSessionCookies"/>, libcurl's <c>CURLOPT_COOKIELIST</c> <c>ALL</c> and
/// <c>SESS</c> (BL-2520).
/// </summary>
public sealed partial class CookieStoreTests
{
    private const string SessionAndLasting =
        "127.0.0.1\tFALSE\t/\tFALSE\t0\tsession\t1\n127.0.0.1\tFALSE\t/\tFALSE\t4102444800\tlasting\t2\n";

    [TestMethod]
    public void RemoveAll_StoredCookiesAndACookieString_RemovesTheCookiesAndKeepsTheString()
    {
        CookieStore store = new();
        store.LoadCookieFile(new StringReader(SessionAndLasting), discardSessionCookies: false, Now);
        store.AddCookieString("given=1");

        store.RemoveAll();

        Assert.IsEmpty(store.Cookies);
        Assert.AreEqual("given=1", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    [TestMethod]
    public void RemoveSessionCookies_SessionAndLastingCookies_KeepsOnlyTheLastingOne()
    {
        CookieStore store = new();
        store.LoadCookieFile(new StringReader(SessionAndLasting), discardSessionCookies: false, Now);

        store.RemoveSessionCookies();

        Assert.AreEqual("lasting", store.Cookies.Single().Name);
        Assert.AreEqual("lasting=2", store.GetCookieHeader(Loopback, secure: false, Now));
    }

    [TestMethod]
    public void RemoveAll_ThenACookieIsStored_StoresItAgain()
    {
        CookieStore store = new();
        store.LoadCookieFile(new StringReader(SessionAndLasting), discardSessionCookies: false, Now);

        store.RemoveAll();
        store.LoadCookieFile(new StringReader("127.0.0.1\tFALSE\t/\tFALSE\t0\tnew\t3\n"), discardSessionCookies: false, Now);

        Assert.AreEqual("new=3", store.GetCookieHeader(Loopback, secure: false, Now));
    }
}
