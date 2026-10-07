using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins the Public Suffix List check (BL-223). Measured on curl 8.21.0 (libpsl 0.21.5) on 2026-09-27:
/// <c>Record-CurlExchange.ps1</c> served <c>Set-Cookie: &lt;name&gt;=v; Path=/; &lt;Domain&gt;</c> to
/// <c>curl -s -v --resolve &lt;host&gt;:&lt;port&gt;:127.0.0.1 -c - http://&lt;host&gt;:&lt;port&gt;/</c>, and the
/// jar curl wrote to standard output held the cookie or was empty. BL-223's Notes list every run.
/// </summary>
public sealed partial class CookieStoreTests
{
    [TestMethod]
    [DataRow("www.example.co.uk", "Domain=co.uk", DisplayName = "An ICANN suffix")]
    [DataRow("www.example.co.uk", "Domain=.co.uk", DisplayName = "An ICANN suffix with a leading dot")]
    [DataRow("www.example.co.uk", "Domain=CO.UK", DisplayName = "An ICANN suffix in upper case")]
    [DataRow("www.example.com", "Domain=com", DisplayName = "A top-level domain")]
    [DataRow("a.foo.ck", "Domain=foo.ck", DisplayName = "A child of a wildcard rule")]
    [DataRow("a.github.io", "Domain=github.io", DisplayName = "A private-section suffix")]
    public void StoreFromResponse_DomainIsAPublicSuffix_DropsTheCookie(string host, string domainAttribute)
    {
        CookieStore store = new();
        Diagnostics.ArrangeSetCookies(CurlUrl.Parse($"http://{host}/"), [$"n=v; Path=/; {domainAttribute}"], Now);

        store.StoreFromResponse(CurlUrl.Parse($"http://{host}/"), [$"n=v; Path=/; {domainAttribute}"], Now, NoTransferEvents.Instance);
        Diagnostics.ActCookies("stored cookies", store.Cookies);

        Diagnostics.Assert("stored cookie count", 0, store.Cookies.Count);
        Assert.IsEmpty(store.Cookies);
    }

    [TestMethod]
    [DataRow("www.example.co.uk", "Domain=example.co.uk", "example.co.uk", DisplayName = "A registrable domain under an ICANN suffix")]
    [DataRow("www.example.com", "Domain=example.com", "example.com", DisplayName = "A registrable domain under a top-level domain")]
    [DataRow("co.uk", "Domain=co.uk", "co.uk", DisplayName = "A public suffix set by itself")]
    [DataRow("co.uk", "", "co.uk", DisplayName = "A host-only cookie from a public suffix")]
    [DataRow("a.www.ck", "Domain=www.ck", "www.ck", DisplayName = "An exception to a wildcard rule")]
    [DataRow("a.b.zzqq", "Domain=b.zzqq", "b.zzqq", DisplayName = "Under a top-level domain the list does not name")]
    public void StoreFromResponse_DomainIsNotAPublicSuffix_KeepsTheCookie(string host, string domainAttribute, string storedDomain)
    {
        CookieStore store = new();
        Diagnostics.ArrangeSetCookies(CurlUrl.Parse($"http://{host}/"), [$"n=v; Path=/; {domainAttribute}"], Now);

        store.StoreFromResponse(CurlUrl.Parse($"http://{host}/"), [$"n=v; Path=/; {domainAttribute}"], Now, NoTransferEvents.Instance);
        Diagnostics.ActCookies("stored cookies", store.Cookies);

        Diagnostics.AssertText("stored domain", storedDomain, store.Cookies.Single().Domain);
        Assert.AreEqual(storedDomain, store.Cookies.Single().Domain);
    }

    /// <summary>A 255-character host kept its host-only cookie; a 256-character one was dropped.</summary>
    [TestMethod]
    [DataRow("example.com", 1, DisplayName = "255 characters")]
    [DataRow("examplex.com", 0, DisplayName = "256 characters")]
    public void StoreFromResponse_HostLongerThan255Characters_DropsEveryCookie(string lastLabels, int storedCount)
    {
        string label = new('a', 60);
        CurlUrl url = CurlUrl.Parse($"http://{label}.{label}.{label}.{label}.{lastLabels}/");
        CookieStore store = new();
        Diagnostics.Arrange("host length", url.Host.Length);
        Diagnostics.ArrangeSetCookies(url, ["n=v; Path=/"], Now);

        store.StoreFromResponse(url, ["n=v; Path=/"], Now, NoTransferEvents.Instance);
        Diagnostics.ActCookies("stored cookies", store.Cookies);

        Diagnostics.Assert("stored cookie count", storedCount, store.Cookies.Count);
        Assert.HasCount(storedCount, store.Cookies);
    }

    /// <summary>
    /// A rule written with a non-ASCII label also refuses the label's punycode form, the form curl's IDN
    /// conversion gives the host: <c>公司.cn</c> is <c>xn--55qx5d.cn</c>.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_DomainIsThePunycodeFormOfANonAsciiSuffix_DropsTheCookie()
    {
        CookieStore store = new();
        Diagnostics.ArrangeSetCookies(CurlUrl.Parse("http://www.example.xn--55qx5d.cn/"), ["n=v; Path=/; Domain=xn--55qx5d.cn"], Now);

        store.StoreFromResponse(CurlUrl.Parse("http://www.example.xn--55qx5d.cn/"), ["n=v; Path=/; Domain=xn--55qx5d.cn"], Now, NoTransferEvents.Instance);
        Diagnostics.ActCookies("stored cookies", store.Cookies);

        Diagnostics.Assert("stored cookie count", 0, store.Cookies.Count);
        Assert.IsEmpty(store.Cookies);
    }
}
