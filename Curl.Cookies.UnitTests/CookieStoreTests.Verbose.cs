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
        string[] headers = ["n2=v; Path=/; Domain=example.co.uk"];
        Diagnostics.ArrangeSetCookies(CoUk, headers, Now);

        new CookieStore().StoreFromResponse(CoUk, headers, Now, events);

        string[] expected = ["Added cookie n2=\"v\" for domain example.co.uk, path /, expire 0"];
        LogReportedLines(events, expected);
        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    [DataRow("Domain=co.uk", "co.uk", DisplayName = "As sent")]
    [DataRow("Domain=.co.uk", "co.uk", DisplayName = "Without its leading dot")]
    [DataRow("Domain=CO.UK", "CO.UK", DisplayName = "In the case sent")]
    public void StoreFromResponse_DomainIsAPublicSuffix_ReportsTheDrop(string domainAttribute, string printedDomain)
    {
        RecordingTransferEvents events = new();
        string[] headers = [$"n1=v; Path=/; {domainAttribute}"];
        Diagnostics.ArrangeSetCookies(CoUk, headers, Now);

        new CookieStore().StoreFromResponse(CoUk, headers, Now, events);

        string[] expected = [$"cookie 'n1' dropped, domain 'www.example.co.uk' must not set cookies for '{printedDomain}'"];
        LogReportedLines(events, expected);
        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    public void StoreFromResponse_NamesakeReplaced_ReportsReplacedCookie()
    {
        RecordingTransferEvents events = new();
        string[] headers = ["r=1; Path=/", "r=2; Path=/"];
        Diagnostics.ArrangeSetCookies(CoUk, headers, Now);

        new CookieStore().StoreFromResponse(CoUk, headers, Now, events);

        string[] expected =
        [
            "Added cookie r=\"1\" for domain www.example.co.uk, path /, expire 0",
            "Replaced cookie r=\"2\" for domain www.example.co.uk, path /, expire 0",
        ];
        LogReportedLines(events, expected);
        CollectionAssert.AreEqual(expected, events.Info);
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
        string[] coUkHeaders = ["m=1; Path=/; Max-Age=100", "z=1; Path=/; Max-Age=0", "qv=\"a b\"; Path=/p"];
        string[] loopbackHeaders = ["j=1; Domain=127.0.0.1; Path=/"];
        Diagnostics.ArrangeSetCookies(CoUk, coUkHeaders, Now);
        Diagnostics.ArrangeSetCookies(Loopback, loopbackHeaders, Now);

        store.StoreFromResponse(CoUk, coUkHeaders, Now, events);
        store.StoreFromResponse(Loopback, loopbackHeaders, Now, events);

        string[] expected =
        [
            $"Added cookie m=\"1\" for domain www.example.co.uk, path /, expire {Now.ToUnixTimeSeconds() + 100}",
            "Added cookie z=\"1\" for domain www.example.co.uk, path /, expire 1",
            "Added cookie qv=\"\"a b\"\" for domain www.example.co.uk, path /p, expire 0",
            "Added cookie j=\"1\" for domain 127.0.0.1, path /, expire 0",
        ];
        LogReportedLines(events, expected);
        CollectionAssert.AreEqual(expected, events.Info);
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
        Diagnostics.ArrangeText("cookie file", "www.example.co.uk\tFALSE\t/\tTRUE\t0\ts\t1\n");
        using (StringReader file = new("www.example.co.uk\tFALSE\t/\tTRUE\t0\ts\t1\n"))
        {
            store.LoadCookieFile(file, discardSessionCookies: false, Now);
        }

        string[] headers = ["s=2; Path=/"];
        Diagnostics.ArrangeSetCookies(CoUk, headers, Now);

        store.StoreFromResponse(CoUk, headers, Now, events);

        string[] expected = ["cookie 's' for domain 'www.example.co.uk' dropped, would overlay an existing cookie"];
        LogReportedLines(events, expected);
        CollectionAssert.AreEqual(expected, events.Info);
    }

    /// <summary>
    /// Measured: of 52 cookies curl printed 50 <c>Added</c> lines and nothing for the rest. A header the parser
    /// refuses is reported (BL-443) but not counted.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_PastTheLimit_ReportsNothingForThem()
    {
        RecordingTransferEvents events = new();
        string[] headers = ["noequals", .. Enumerable.Range(1, 52).Select(number => $"k{number}=v; Path=/")];
        Diagnostics.Arrange(
            "Set-Cookie headers",
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"noequals, then k1=v; Path=/ to k52=v; Path=/ ({headers.Length} in all) from {CoUk.OriginalString} at Unix {Now.ToUnixTimeSeconds()}"));

        new CookieStore().StoreFromResponse(CoUk, headers, Now, events);

        Diagnostics.Act("-v lines reported", events.Info.Count);
        Diagnostics.Assert("-v line count", CookieStore.MostCookiesStoredPerResponse + 1, events.Info.Count);
        Diagnostics.AssertText("first -v line", "invalid cookie, dropped", events.Info[0]);
        Diagnostics.AssertText("last -v line", "Added cookie k50=\"v\" for domain www.example.co.uk, path /, expire 0", events.Info[^1]);
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
        string[] headers = ["a=1; Max-Age=0", "a=2"];
        Diagnostics.ArrangeSetCookies(Loopback, headers, Now);

        new CookieStore().StoreFromResponse(Loopback, headers, Now, events);

        string[] expected =
        [
            "Added cookie a=\"1\" for domain 127.0.0.1, path /, expire 1",
            "Added cookie a=\"2\" for domain 127.0.0.1, path /, expire 0",
        ];
        LogReportedLines(events, expected);
        CollectionAssert.AreEqual(expected, events.Info);
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
        Diagnostics.Arrange(
            "single headers and the count so far",
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"a=1 at 7, noequals after the first, b=1 at the limit {CookieStore.MostCookiesStoredPerResponse}"));

        int afterStored = store.StoreFromResponse(Loopback, "a=1", 7, Now, events);
        int afterRefused = store.StoreFromResponse(Loopback, "noequals", afterStored, Now, events);
        int pastTheLimit = store.StoreFromResponse(Loopback, "b=1", CookieStore.MostCookiesStoredPerResponse, Now, events);

        Diagnostics.Act("counts returned (stored, refused, past the limit)", $"{afterStored}, {afterRefused}, {pastTheLimit}");
        Diagnostics.Assert("count after a stored header", 8, afterStored);
        Diagnostics.Assert("count after a refused header", 8, afterRefused);
        Diagnostics.Assert("count past the limit", CookieStore.MostCookiesStoredPerResponse, pastTheLimit);
        Diagnostics.Assert("-v line count", 2, events.Info.Count);
        Diagnostics.Assert("stored cookie name", "a", store.Cookies.Single().Name);
        Assert.AreEqual(8, afterStored);
        Assert.AreEqual(8, afterRefused);
        Assert.AreEqual(CookieStore.MostCookiesStoredPerResponse, pastTheLimit);
        Assert.HasCount(2, events.Info);
        Assert.AreEqual("a", store.Cookies.Single().Name);
    }

    [TestMethod]
    public void StoreFromResponse_NullEvents_Throws()
    {
        ArrangeNullArgument("events");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new CookieStore().StoreFromResponse(Www, [], Now, null!));

        LogThrown(thrown);
    }

    [TestMethod]
    public void StoreFromResponse_OneHeaderNullArgument_Throws()
    {
        CookieStore store = new();

        ArrangeNullArgument("url");
        LogThrown(Assert.ThrowsExactly<ArgumentNullException>(() => store.StoreFromResponse(null!, "a=1", 0, Now, NoTransferEvents.Instance)));
        ArrangeNullArgument("setCookieHeader");
        LogThrown(Assert.ThrowsExactly<ArgumentNullException>(() => store.StoreFromResponse(Www, (string)null!, 0, Now, NoTransferEvents.Instance)));
        ArrangeNullArgument("events");
        LogThrown(Assert.ThrowsExactly<ArgumentNullException>(() => store.StoreFromResponse(Www, "a=1", 0, Now, null!)));
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
        Diagnostics.Arrange("cookies stored, k1=v to k<n>=v", stored);
        foreach (int number in Enumerable.Range(1, stored))
        {
            store.StoreFromResponse(Loopback, "k" + number.ToString(System.Globalization.CultureInfo.InvariantCulture) + "=v", 0, Now, NoTransferEvents.Instance);
        }

        RecordingTransferEvents events = new();

        string header = store.GetCookieHeader(Loopback, secure: false, Now, [], events)!;

        string[] expected = reported ? ["Included max number of cookies (150) in request!"] : [];
        Diagnostics.Act("cookies in the Cookie header", header.Split("; ").Length);
        Diagnostics.Assert("cookies in the Cookie header", Math.Min(stored, CookieStore.MostCookiesSent), header.Split("; ").Length);
        LogReportedLines(events, expected);
        Assert.HasCount(Math.Min(stored, CookieStore.MostCookiesSent), header.Split("; "));
        CollectionAssert.AreEqual(expected, events.Info);
    }

    /// <summary>
    /// curl 8.21.0's <c>http.c</c> names the first cookie that would make the header too long, sends none after
    /// it and leaves the <c>-b name=value</c> strings out (BL-1108).
    /// </summary>
    [TestMethod]
    public void GetCookieHeader_HeaderTooLong_ReportsTheFirstCookieLeftOut()
    {
        CookieStore store = new();
        Diagnostics.Arrange("cookies (name:value length)", "aaa:4000 bb:4000 c:165 dd:1, plus the -b string s=1");
        store.StoreFromResponse(Loopback, ["aaa=" + new string('x', 4000), "bb=" + new string('x', 4000), "c=" + new string('x', 165), "dd=1"], Now, NoTransferEvents.Instance);
        RecordingTransferEvents events = new();

        string header = store.GetCookieHeader(Loopback, secure: false, Now, ["s=1"], events)!;

        string names = string.Join(',', header.Split("; ").Select(pair => pair.Split('=')[0]));
        Diagnostics.Act("header length", header.Length);
        Diagnostics.AssertText("names sent", "aaa,dd,bb", names);
        string[] expected = ["Restricted outgoing cookies due to header size, 'c' not sent"];
        LogReportedLines(events, expected);
        Assert.AreEqual("aaa,dd,bb", names);
        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    public void GetCookieHeader_WithinTheLimits_ReportsNothingAndSendsTheStrings()
    {
        CookieStore store = new();
        string[] headers = ["a=1"];
        Diagnostics.ArrangeSetCookies(Loopback, headers, Now);
        Diagnostics.Arrange("-b string", "s=1");
        store.StoreFromResponse(Loopback, headers, Now, NoTransferEvents.Instance);
        RecordingTransferEvents events = new();

        string? header = store.GetCookieHeader(Loopback, secure: false, Now, ["s=1"], events);

        Diagnostics.ActText("Cookie header", header);
        Diagnostics.AssertText("Cookie header", "a=1; s=1", header);
        LogReportedLines(events, []);
        Assert.AreEqual("a=1; s=1", header);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public void GetCookieHeader_EventsHeaderTooLong_ReportsTheFirstCookieLeftOut()
    {
        CookieStore store = new();
        Diagnostics.Arrange("cookies (name:value length)", "aaa:4000 bb:4000 c:165 dd:1, plus the added string s=1");
        store.StoreFromResponse(Loopback, ["aaa=" + new string('x', 4000), "bb=" + new string('x', 4000), "c=" + new string('x', 165), "dd=1"], Now, NoTransferEvents.Instance);
        store.AddCookieString("s=1");
        RecordingTransferEvents events = new();

        string header = store.GetCookieHeader(Loopback, secure: false, Now, events)!;

        string names = string.Join(',', header.Split("; ").Select(pair => pair.Split('=')[0]));
        Diagnostics.Act("header length", header.Length);
        Diagnostics.AssertText("names sent", "aaa,dd,bb", names);
        string[] expected = ["Restricted outgoing cookies due to header size, 'c' not sent"];
        LogReportedLines(events, expected);
        Assert.AreEqual("aaa,dd,bb", names);
        CollectionAssert.AreEqual(expected, events.Info);
    }

    [TestMethod]
    public void GetCookieHeader_EventsWithinTheLimits_SendsTheAddedCookieStrings()
    {
        CookieStore store = new();
        string[] headers = ["a=1"];
        Diagnostics.ArrangeSetCookies(Loopback, headers, Now);
        store.StoreFromResponse(Loopback, headers, Now, NoTransferEvents.Instance);
        store.AddCookieString("s=1");
        Diagnostics.Arrange("added -b string", "s=1");
        RecordingTransferEvents events = new();

        string? header = store.GetCookieHeader(Loopback, secure: false, Now, events);

        Diagnostics.ActText("Cookie header", header);
        Diagnostics.AssertText("Cookie header", "a=1; s=1", header);
        LogReportedLines(events, []);
        Assert.AreEqual("a=1; s=1", header);
        Assert.IsEmpty(events.Info);
    }

    [TestMethod]
    public void GetCookieHeader_NullEvents_Throws()
    {
        ArrangeNullArgument("events");

        ArgumentNullException thrown = Assert.ThrowsExactly<ArgumentNullException>(() => new CookieStore().GetCookieHeader(Loopback, secure: false, Now, [], null!));

        LogThrown(thrown);
    }

    /// <summary>Writes the <c>-v</c> lines the store reported as an ACT line, then the ASSERT and DIFF lines against <paramref name="expected"/>.</summary>
    private void LogReportedLines(RecordingTransferEvents events, IEnumerable<string> expected)
    {
        Diagnostics.Act("-v lines reported", CookieTestDiagnostics.Shown(events.Info));
        Diagnostics.AssertTexts("-v lines reported", expected, events.Info);
    }
}
