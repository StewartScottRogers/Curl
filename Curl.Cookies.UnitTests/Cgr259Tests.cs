using Curl.Cookies.Fakes;

namespace Curl.Cookies;

/// <summary>
/// Pins CGR-259: a <c>CURLOPT_COOKIELIST</c> item <c>Set-Cookie: k=v; path=/</c> with no domain stores nothing in
/// curl 8.22.0, so <c>CURLINFO_COOKIELIST</c> stays empty, while a <c>Set-Cookie:</c> line with a <c>Domain</c> is kept.
/// </summary>
[TestClass]
public sealed class Cgr259Tests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_790_460_351);

    [TestMethod]
    public void AddCookieListLine_SetCookieWithoutDomain_StoresNothing()
    {
        CookieStore store = new();

        store.AddCookieListLine("Set-Cookie: k=v; path=/", Now, new RecordingTransferEvents());

        Assert.AreEqual(0, store.Cookies.Count);
    }

    [TestMethod]
    public void AddCookieListLine_SetCookieWithDomain_StoresTheCookie()
    {
        CookieStore store = new();

        store.AddCookieListLine("Set-Cookie: k=v; domain=example.test; path=/", Now, new RecordingTransferEvents());

        Assert.AreEqual(1, store.Cookies.Count);
    }

    [TestMethod]
    public void AddCookieListLine_NetscapeLine_StoresTheCookie()
    {
        CookieStore store = new();

        store.AddCookieListLine("example.test\tFALSE\t/\tFALSE\t0\tk\tv", Now, new RecordingTransferEvents());

        Assert.AreEqual(1, store.Cookies.Count);
    }
}
