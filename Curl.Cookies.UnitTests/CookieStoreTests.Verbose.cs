using Curl.Cookies.Fakes;
using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins the <c>-v</c> lines <see cref="CookieStore.StoreFromResponse"/> reports (BL-367). Measured on curl 8.21.0
/// (mingw, libpsl 0.21.5) on 2026-09-27: <c>Record-CurlExchange.ps1</c> served the <c>Set-Cookie</c> headers to
/// <c>curl -s -v --resolve &lt;host&gt;:&lt;port&gt;:127.0.0.1 -c - http://&lt;host&gt;:&lt;port&gt;/</c>, and each
/// expected line is the text curl wrote to standard error after <c>* </c>. BL-367's Notes list every run.
/// </summary>
public sealed partial class CookieStoreTests
{
    private static readonly CurlUrl CoUk = CurlUrl.Parse("http://www.example.co.uk/");

    [TestMethod]
    public void StoreFromResponse_CookieAdded_ReportsAddedCookie()
    {
        RecordingTransferEvents events = new();

        new CookieStore().StoreFromResponse(CoUk, ["n2=v; Path=/; Domain=example.co.uk"], Now, events);

        CollectionAssert.AreEqual(new[] { "Added cookie n2=\"v\" for domain example.co.uk, path /, expire 0" }, events.Info);
    }

    [TestMethod]
    [DataRow("Domain=co.uk", "co.uk", DisplayName = "As sent")]
    [DataRow("Domain=.co.uk", "co.uk", DisplayName = "Without its leading dot")]
    [DataRow("Domain=CO.UK", "CO.UK", DisplayName = "In the case sent")]
    public void StoreFromResponse_DomainIsAPublicSuffix_ReportsTheDrop(string domainAttribute, string printedDomain)
    {
        RecordingTransferEvents events = new();

        new CookieStore().StoreFromResponse(CoUk, [$"n1=v; Path=/; {domainAttribute}"], Now, events);

        CollectionAssert.AreEqual(new[] { $"cookie 'n1' dropped, domain 'www.example.co.uk' must not set cookies for '{printedDomain}'" }, events.Info);
    }

    [TestMethod]
    public void StoreFromResponse_NamesakeReplaced_ReportsReplacedCookie()
    {
        RecordingTransferEvents events = new();

        new CookieStore().StoreFromResponse(CoUk, ["r=1; Path=/", "r=2; Path=/"], Now, events);

        CollectionAssert.AreEqual(
            new[]
            {
                "Added cookie r=\"1\" for domain www.example.co.uk, path /, expire 0",
                "Replaced cookie r=\"2\" for domain www.example.co.uk, path /, expire 0",
            },
            events.Info);
    }

    /// <summary>
    /// Measured: <c>Max-Age=100</c> printed the receive time plus 100 seconds, and <c>Max-Age=0</c> printed
    /// <c>expire 1</c>, though the cookie is then removed. A quoted value keeps its quotes inside curl's own, an
    /// IP-address host names the address, and a path is printed as stored.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_ExpiryQuotesAndIpAddress_ReportedAsCurlPrintsThem()
    {
        RecordingTransferEvents events = new();
        CookieStore store = new();

        store.StoreFromResponse(CoUk, ["m=1; Path=/; Max-Age=100", "z=1; Path=/; Max-Age=0", "qv=\"a b\"; Path=/p"], Now, events);
        store.StoreFromResponse(Loopback, ["j=1; Domain=127.0.0.1; Path=/"], Now, events);

        CollectionAssert.AreEqual(
            new[]
            {
                $"Added cookie m=\"1\" for domain www.example.co.uk, path /, expire {Now.ToUnixTimeSeconds() + 100}",
                "Added cookie z=\"1\" for domain www.example.co.uk, path /, expire 1",
                "Added cookie qv=\"\"a b\"\" for domain www.example.co.uk, path /p, expire 0",
                "Added cookie j=\"1\" for domain 127.0.0.1, path /, expire 0",
            },
            events.Info);
    }

    /// <summary>
    /// Measured with <c>-b</c> naming a file that holds <c>www.example.co.uk FALSE / TRUE 0 s 1</c> (tab
    /// separated), and <c>Set-Cookie: s=2; Path=/</c> over plain HTTP. Loading the file printed nothing.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_WouldOverlayASecureCookie_ReportsTheDrop()
    {
        RecordingTransferEvents events = new();
        CookieStore store = new();
        using (StringReader file = new("www.example.co.uk\tFALSE\t/\tTRUE\t0\ts\t1\n"))
        {
            store.LoadCookieFile(file, discardSessionCookies: false, Now);
        }

        store.StoreFromResponse(CoUk, ["s=2; Path=/"], Now, events);

        CollectionAssert.AreEqual(new[] { "cookie 's' for domain 'www.example.co.uk' dropped, would overlay an existing cookie" }, events.Info);
    }

    /// <summary>
    /// Measured: of 52 cookies curl printed 50 <c>Added</c> lines and nothing for the rest. A header the parser
    /// refuses is reported (BL-443) but not counted.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_PastTheLimit_ReportsNothingForThem()
    {
        RecordingTransferEvents events = new();

        new CookieStore().StoreFromResponse(CoUk, ["noequals", .. Enumerable.Range(1, 52).Select(number => $"k{number}=v; Path=/")], Now, events);

        Assert.HasCount(CookieStore.MostCookiesStoredPerResponse + 1, events.Info);
        Assert.AreEqual("invalid cookie, dropped", events.Info[0]);
        Assert.AreEqual("Added cookie k50=\"v\" for domain www.example.co.uk, path /, expire 0", events.Info[^1]);
    }

    /// <summary>
    /// Measured (BL-468 Notes): after <c>Set-Cookie: a=1; Max-Age=0</c>, curl reports <c>a=2</c> in the same
    /// response as added, not replaced: the expired cookie is gone before the next header is stored.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_ExpiredArrivalThenNamesake_ReportsTheNamesakeAdded()
    {
        RecordingTransferEvents events = new();

        new CookieStore().StoreFromResponse(Loopback, ["a=1; Max-Age=0", "a=2"], Now, events);

        CollectionAssert.AreEqual(
            new[]
            {
                "Added cookie a=\"1\" for domain 127.0.0.1, path /, expire 1",
                "Added cookie a=\"2\" for domain 127.0.0.1, path /, expire 0",
            },
            events.Info);
    }

    /// <summary>
    /// One header at a time: the count comes back one higher for a cookie stored, unchanged for a header
    /// refused, and a header that arrives once the count has reached the limit is neither stored nor reported.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_OneHeader_CountsTowardTheLimit()
    {
        CookieStore store = new();
        RecordingTransferEvents events = new();

        int afterStored = store.StoreFromResponse(Loopback, "a=1", 7, Now, events);
        int afterRefused = store.StoreFromResponse(Loopback, "noequals", afterStored, Now, events);
        int pastTheLimit = store.StoreFromResponse(Loopback, "b=1", CookieStore.MostCookiesStoredPerResponse, Now, events);

        Assert.AreEqual(8, afterStored);
        Assert.AreEqual(8, afterRefused);
        Assert.AreEqual(CookieStore.MostCookiesStoredPerResponse, pastTheLimit);
        Assert.HasCount(2, events.Info);
        Assert.AreEqual("a", store.Cookies.Single().Name);
    }

    [TestMethod]
    public void StoreFromResponse_NullEvents_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CookieStore().StoreFromResponse(Www, [], Now, null!));

    [TestMethod]
    public void StoreFromResponse_OneHeaderNullArgument_Throws()
    {
        CookieStore store = new();

        Assert.ThrowsExactly<ArgumentNullException>(() => store.StoreFromResponse(null!, "a=1", 0, Now, NoTransferEvents.Instance));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.StoreFromResponse(Www, (string)null!, 0, Now, NoTransferEvents.Instance));
        Assert.ThrowsExactly<ArgumentNullException>(() => store.StoreFromResponse(Www, "a=1", 0, Now, null!));
    }

    /// <summary>
    /// curl 8.21.0's <c>Curl_cookie_getlist</c> reports the limit as soon as the 150th matching cookie is taken,
    /// so 151 and exactly 150 report it once and 149 report nothing (BL-1108).
    /// </summary>
    [TestMethod]
    [DataRow(151, true)]
    [DataRow(150, true)]
    [DataRow(149, false)]
    public void GetCookieHeader_ManyCookies_ReportsTheMostCookiesSent(int stored, bool reported)
    {
        CookieStore store = new();
        foreach (int number in Enumerable.Range(1, stored))
        {
            store.StoreFromResponse(Loopback, "k" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "=v", 0, Now, NoTransferEvents.Instance);
        }

        RecordingTransferEvents events = new();

        string header = store.GetCookieHeader(Loopback, secure: false, Now, [], events)!;

        Assert.HasCount(Math.Min(stored, CookieStore.MostCookiesSent), header.Split("; "));
        CollectionAssert.AreEqual(reported ? new[] { "Included max number of cookies (150) in request!" } : Array.Empty<string>(), events.Info);
    }

    /// <summary>
    /// curl 8.21.0's <c>http.c</c> names the first cookie that would make the header too long, sends none after
    /// it and leaves the <c>-b name=value</c> strings out (BL-1108).
    /// </summary>
    [TestMethod]
    public void GetCookieHeader_HeaderTooLong_ReportsTheFirstCookieLeftOut()
    {
        CookieStore store = new();
        store.StoreFromResponse(Loopback, ["aaa=" + new string('x', 4000), "bb=" + new string('x', 4000), "c=" + new string('x', 165), "dd=1"], Now, NoTransferEvents.Instance);
        RecordingTransferEvents events = new();

        string header = store.GetCookieHeader(Loopback, secure: false, Now, ["s=1"], events)!;

        Assert.AreEqual("aaa,dd,bb", string.Join(',', header.Split("; ").Select(pair => pair.Split('=')[0])));
        CollectionAssert.AreEqual(new[] { "Restricted outgoing cookies due to header size, 'c' not sent" }, events.Info);
    }

    [TestMethod]
    public void GetCookieHeader_WithinTheLimits_ReportsNothingAndSendsTheStrings()
    {
        CookieStore store = new();
        store.StoreFromResponse(Loopback, ["a=1"], Now, NoTransferEvents.Instance);
        RecordingTransferEvents events = new();

        Assert.AreEqual("a=1; s=1", store.GetCookieHeader(Loopback, secure: false, Now, ["s=1"], events));
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public void GetCookieHeader_NullEvents_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CookieStore().GetCookieHeader(Loopback, secure: false, Now, [], null!));
}
