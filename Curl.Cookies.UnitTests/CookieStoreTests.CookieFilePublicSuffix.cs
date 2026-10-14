using Curl.Cookies.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins the Public Suffix List check on cookies loaded from a <c>-b</c> file (upstream test409, BL-2165). Measured on
/// curl 8.22.0 (mingw, libpsl 0.21.5) on 2026-10-13: <c>Record-CurlExchange.ps1</c> ran
/// <c>curl -v -b cookies.txt -c jar.txt --resolve foo.co.uk:&lt;port&gt;:127.0.0.1 http://foo.co.uk:&lt;port&gt;/409</c>
/// (and the same against <c>http://localhost:&lt;port&gt;/409</c>); a dropped cookie was neither sent nor written to the
/// jar, and curl printed the expected <c>-v</c> line for it. BL-2165's Notes list every run.
/// </summary>
public sealed partial class CookieStoreTests
{
    [TestMethod]
    public void LoadCookieFile_TailmatchingPublicSuffixCookie_IsNotSentAndTheSiblingIs()
    {
        const string file = ".co.uk\tTRUE\t/\tFALSE\t0\tbad\tnot-allowed\nfoo.co.uk\tFALSE\t/\tFALSE\t0\tgood\tallowed\n";
        RecordingTransferEvents events = new();
        CookieStore store = new();
        Diagnostics.ArrangeText("cookie file", file);

        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now, events);
        string? header = store.GetCookieHeader(CurlUrl.Parse("http://foo.co.uk/409"), secure: false, Now);
        Diagnostics.Act("Cookie header", header);

        Diagnostics.Assert("Cookie header", "good=allowed", header);
        Assert.AreEqual("good=allowed", header);
        CollectionAssert.AreEqual(new[] { "cookie 'bad' dropped, domain '[file]' must not set cookies for 'co.uk'" }, events.Info);
    }

    [TestMethod]
    [DataRow("co.uk\tFALSE", "co.uk", DisplayName = "A host-only public suffix")]
    [DataRow("co.uk\tTRUE", "co.uk", DisplayName = "A tailmatching public suffix without a leading dot")]
    [DataRow(".uk\tTRUE", "uk", DisplayName = "A top-level domain")]
    [DataRow(".CO.UK\tTRUE", "CO.UK", DisplayName = "A public suffix in upper case")]
    [DataRow("github.io\tFALSE", "github.io", DisplayName = "A private-section suffix")]
    [DataRow("com.\tFALSE", "com.", DisplayName = "A top-level domain with a trailing dot")]
    [DataRow("[::1]\tFALSE", "[::1]", DisplayName = "A bracketed IPv6 address, a single label to libpsl")]
    public void LoadCookieFile_DomainIsAPublicSuffix_DropsTheCookieAndReportsIt(string domainFields, string reportedDomain)
    {
        string file = $"{domainFields}\t/\tFALSE\t0\tn\tv\n";
        RecordingTransferEvents events = new();
        CookieStore store = new();
        Diagnostics.ArrangeText("cookie file", file);

        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now, events);
        Diagnostics.ActCookies("stored cookies", store.Cookies);

        Diagnostics.Assert("stored cookie count", 0, store.Cookies.Count);
        Assert.IsEmpty(store.Cookies);
        CollectionAssert.AreEqual(new[] { $"cookie 'n' dropped, domain '[file]' must not set cookies for '{reportedDomain}'" }, events.Info);
    }

    [TestMethod]
    [DataRow("localhost\tFALSE", DisplayName = "localhost, exempt though a single label")]
    [DataRow(".LOCALHOST\tTRUE", DisplayName = "localhost in upper case")]
    [DataRow(".foo.co.uk\tTRUE", DisplayName = "A registrable domain under a public suffix")]
    [DataRow("example.com.\tFALSE", DisplayName = "A registrable domain with a trailing dot")]
    [DataRow("127.0.0.1\tTRUE", DisplayName = "An IPv4 address")]
    [DataRow("::1\tFALSE", DisplayName = "An unbracketed IPv6 address")]
    public void LoadCookieFile_DomainIsNotAPublicSuffix_StoresTheCookieSilently(string domainFields)
    {
        string file = $"{domainFields}\t/\tFALSE\t0\tn\tv\n";
        RecordingTransferEvents events = new();
        CookieStore store = new();
        Diagnostics.ArrangeText("cookie file", file);

        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now, events);
        Diagnostics.ActCookies("stored cookies", store.Cookies);

        Diagnostics.Assert("stored cookie count", 1, store.Cookies.Count);
        Assert.HasCount(1, store.Cookies);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    [DataRow("1.2", DisplayName = "A two-part number, not a dotted quad")]
    public void LoadCookieFile_NumericDomainThatIsNotADottedQuad_IsCheckedAgainstTheList(string domain)
    {
        string file = $"{domain}\tFALSE\t/\tFALSE\t0\tn\tv\n";
        RecordingTransferEvents events = new();
        CookieStore store = new();
        Diagnostics.ArrangeText("cookie file", file);

        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now, events);
        Diagnostics.ActCookies("stored cookies", store.Cookies);

        Diagnostics.Assert("stored cookie count", 1, store.Cookies.Count);
        Assert.HasCount(1, store.Cookies);
    }

    [TestMethod]
    public void LoadCookieFile_DomainLongerThan255Characters_DropsTheCookie()
    {
        string domain = string.Join('.', Enumerable.Repeat("abcdefghij", 24)) + ".example.com";
        string file = $"{domain}\tFALSE\t/\tFALSE\t0\tn\tv\n";
        CookieStore store = new();
        Diagnostics.ArrangeText("cookie file", file);

        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now, new RecordingTransferEvents());
        Diagnostics.ActCookies("stored cookies", store.Cookies);

        Diagnostics.Assert("stored cookie count", 0, store.Cookies.Count);
        Assert.IsEmpty(store.Cookies);
    }
}
