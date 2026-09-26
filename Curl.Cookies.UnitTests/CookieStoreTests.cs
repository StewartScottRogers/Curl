namespace Curl.Cookies;

/// <summary>
/// Pins <see cref="CookieStore"/> to curl 8.21.0 (<c>x86_64-w64-mingw32</c>, Schannel, libpsl), measured on
/// 2026-09-26 with <c>Record-CurlExchange.ps1</c>: a first transfer received the <c>Set-Cookie</c> headers
/// (or curl loaded the same cookies from a <c>-b</c> file, in the order written here), and a later transfer's
/// <c>Cookie</c> header, or the <c>-c</c> jar, is the expected text. Hosts other than <c>127.0.0.1</c> were
/// reached with <c>--connect-to ::127.0.0.1:&lt;port&gt;</c>. BL-220's Notes record the runs re-measured when it finished.
/// </summary>
[TestClass]
public sealed class CookieStoreTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_790_460_351);

    private static readonly Uri Loopback = new("http://127.0.0.1/");

    private static readonly Uri Www = new("http://www.example.test/");

    private static readonly Uri SecureWww = new("https://www.example.test/");

    /// <summary>
    /// Measured: <c>curl -b none.txt http://127.0.0.1:18220/ http://127.0.0.1:18220/p/q/r?x=/a</c>. Longest
    /// path first, then longest name, then newest first; the <c>Secure</c> cookie is sent to plain HTTP on
    /// loopback, and neither <c>/P</c> nor <c>/pq</c> matches <c>/p/q/r</c>.
    /// </summary>
    [TestMethod]
    public void GetCookieHeader_OneHost_OrdersByPathThenNameThenNewestFirst()
    {
        CookieStore store = new();
        store.StoreFromResponse(
            Loopback,
            ["a=1; Path=/", "bbb=2; Path=/", "cc=3; Path=/", "z=4; Path=/p", "y=5; Path=/p/q", "d=6; Domain=127.0.0.1; Path=/", "s=7; Secure; Path=/", "e=8; Max-Age=1; Path=/", "x=9; Path=/pq", "w=10; Path=/P"],
            Now);

        Assert.AreEqual("y=5; z=4; bbb=2; cc=3; e=8; s=7; d=6; a=1", store.GetCookieHeader(new Uri("http://127.0.0.1/p/q/r?x=/a"), secure: false, Now));
    }

    /// <summary>
    /// Measured: <c>curl -b in.txt --connect-to ::127.0.0.1:18220 http://www.example.test/ http://www.example.test/</c>,
    /// the first response setting <c>r1=new</c>. Longer domains go first; a replaced cookie keeps its place;
    /// a <c>Secure</c> cookie is withheld from plain HTTP, as are a sibling host's and an expired cookie.
    /// </summary>
    [TestMethod]
    public void GetCookieHeader_ParentDomains_OrdersByDomainLengthAndReplacementKeepsItsPlace()
    {
        CookieStore store = new();
        store.StoreFromResponse(Www, ["hostonly=1", "domcookie=2; Domain=example.test", "parent=4; Domain=example.test"], Now);
        store.StoreFromResponse(SecureWww, ["sec=5; Secure"], Now);
        store.StoreFromResponse(Www, ["old=6; Max-Age=10", "r1=7", "r2=8"], Now.AddSeconds(-20));
        store.StoreFromResponse(new Uri("http://other.example.test/"), ["sib=9"], Now);
        store.StoreFromResponse(Www, ["upper=10"], Now);

        Assert.AreEqual("hostonly=1; upper=10; r2=8; r1=7; domcookie=2; parent=4", store.GetCookieHeader(Www, secure: false, Now));

        store.StoreFromResponse(Www, ["r1=new"], Now);

        Assert.AreEqual("hostonly=1; upper=10; r2=8; r1=new; domcookie=2; parent=4", store.GetCookieHeader(Www, secure: false, Now));
    }

    [TestMethod]
    public void GetCookieHeader_SecureRequest_SendsSecureCookies()
    {
        CookieStore store = new();
        store.StoreFromResponse(SecureWww, ["sec=5; Secure", "plain=1"], Now);

        Assert.AreEqual("plain=1; sec=5", store.GetCookieHeader(SecureWww, secure: true, Now));
        Assert.AreEqual("plain=1", store.GetCookieHeader(Www, secure: false, Now));
    }

    /// <summary>
    /// Measured: the jar in <c>in.txt</c>, then <c>http://www.example.test/x/</c> answered with the headers
    /// below, then <c>http://www.example.test/x/X</c>. <c>sec=x</c> may not overlay the <c>Secure</c> cookie,
    /// <c>r2=gone</c> has another path so deletes nothing, and <c>/X</c> does not match <c>/x/X</c>.
    /// </summary>
    [TestMethod]
    public void GetCookieHeader_AfterReplacementsAndRefusals_MatchesCurl()
    {
        CookieStore store = new();
        store.StoreFromResponse(Www, ["a=1"], Now);
        store.StoreFromResponse(Www, ["a=2; Domain=example.test"], Now);
        store.StoreFromResponse(SecureWww, ["sec=5; Secure"], Now);
        store.StoreFromResponse(Www, ["r2=8", "p=1", "q=1; Path=/x", "dd=1; Domain=.example.test"], Now);
        Uri first = new("http://www.example.test/x/");

        Assert.AreEqual("q=1; r2=8; p=1; a=1; dd=1; a=2", store.GetCookieHeader(first, secure: false, Now));

        store.StoreFromResponse(first, ["a=3", "sec=x", "r2=gone; Max-Age=0", "p=2; Path=/x", "q=2; Path=/X", "dd=host", "n=1; Domain=EXAMPLE.test"], Now);

        Assert.AreEqual(
            "dd=host; p=2; a=3; q=1; n=1; r2=8; p=1; a=1; dd=1; a=2",
            store.GetCookieHeader(new Uri("http://www.example.test/x/X"), secure: false, Now));
    }

    /// <summary>
    /// Measured: the jar in <c>in.txt</c>, then <c>http://www.example.test/y/</c> answered with the headers
    /// below; the expected set is the <c>-c</c> jar curl wrote. Names and paths replace case-sensitively and
    /// domains in any case; an insecure cookie may not overlay a <c>Secure</c> one on a related domain whose
    /// path's first segment starts its path.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_ReplacementAndSecureOverlay_MatchCurl()
    {
        CookieStore store = new();
        store.StoreFromResponse(Www, ["Nm=1; Path=/", "pp=1; Path=/Y", "dc=1; Domain=example.test; Path=/"], Now);
        store.StoreFromResponse(SecureWww, ["s1=1; Secure; Domain=example.test; Path=/"], Now);
        store.StoreFromResponse(new Uri("https://other.example.test/"), ["s2=1; Secure; Path=/"], Now);
        store.StoreFromResponse(SecureWww, ["s3=1; Secure; Path=/login/en", "s4=1; Secure; Path=/Z", "s5=1; Secure; Path=/q"], Now);

        store.StoreFromResponse(
            new Uri("http://www.example.test/y/"),
            ["nm=2; Path=/", "pp=2; Path=/y", "dc=2; Domain=EXAMPLE.TEST; Path=/", "s1=2; Path=/y", "s2=2; Domain=example.test; Path=/", "s3=2; Path=/loginhelper", "s4=2; Path=/z", "s5=2; Path=/y"],
            Now);

        CollectionAssert.AreEquivalent(
            new[]
            {
                "www.example.test|/|Nm=1", "www.example.test|/Y|pp=1", "EXAMPLE.TEST|/|dc=2", "example.test|/|s1=1", "other.example.test|/|s2=1",
                "www.example.test|/login/en|s3=1", "www.example.test|/Z|s4=1", "www.example.test|/q|s5=1", "www.example.test|/|nm=2",
                "www.example.test|/y|pp=2", "www.example.test|/z|s4=2", "www.example.test|/y|s5=2",
            },
            store.Cookies.Select(cookie => $"{cookie.Domain}|{cookie.Path}|{cookie.Name}={cookie.Value}").ToArray());
    }

    /// <summary>
    /// Measured the same way: an insecure <c>sx</c> is not <c>Sx</c>'s namesake, a domain cookie does not
    /// replace a host-only one, and an already expired cookie deletes its namesake.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_NameCaseHostOnlyFlagAndExpiredArrivals_MatchCurl()
    {
        CookieStore store = new();
        store.StoreFromResponse(SecureWww, ["Sx=1; Secure"], Now);
        store.StoreFromResponse(Www, ["t1=1", "gone=1"], Now);
        store.StoreFromResponse(SecureWww, ["t2=1; Secure"], Now);
        store.StoreFromResponse(Www, ["ex=1"], Now);

        store.StoreFromResponse(
            Www,
            ["sx=2; Path=/", "t1=2; Domain=www.example.test; Path=/", "gone=2; Max-Age=0; Path=/", "t2=2; Path=/", "ex=2; Expires=Thu, 01 Jan 1970 00:00:00 GMT; Path=/"],
            Now);

        CollectionAssert.AreEqual(
            new[] { "Sx=1|False", "t1=1|False", "t2=1|False", "sx=2|False", "t1=2|True" },
            store.Cookies.Select(cookie => $"{cookie.Name}={cookie.Value}|{cookie.IncludesSubdomains}").ToArray());
    }

    [TestMethod]
    public void StoreFromResponse_SecureOrigin_OverlaysASecureCookie()
    {
        CookieStore store = new();
        store.StoreFromResponse(SecureWww, ["s=1; Secure"], Now);

        store.StoreFromResponse(SecureWww, ["s=2"], Now);

        Assert.AreEqual("s=2", store.GetCookieHeader(SecureWww, secure: true, Now));
    }

    /// <summary>
    /// Measured: <c>curl -b none.txt -c jar.txt http://www.example.test/</c> answered with a refused
    /// cookie, a refused <c>Secure</c> one and <c>c1</c> to <c>c55</c>; the jar held <c>c1</c> to <c>c50</c>.
    /// </summary>
    [TestMethod]
    public void StoreFromResponse_ManyCookies_StoresTheFirstFiftyAccepted()
    {
        CookieStore store = new();
        string[] headers = ["bad", "s=1; Secure", .. Enumerable.Range(1, 55).Select(number => $"c{number}=v")];

        store.StoreFromResponse(Www, headers, Now);

        CollectionAssert.AreEqual(Enumerable.Range(1, 50).Select(number => $"c{number}").ToArray(), store.Cookies.Select(cookie => cookie.Name).ToArray());
    }

    /// <summary>
    /// Measured: <c>curl -b in.txt http://127.0.0.1:18220/</c> with <c>k1</c> to <c>k200</c> in the file sent
    /// the 150 oldest, <c>k150</c> to <c>k1</c>, longest name first and newest first.
    /// </summary>
    [TestMethod]
    public void GetCookieHeader_ManyCookies_SendsTheOldestHundredAndFiftySorted()
    {
        CookieStore store = new();
        foreach (int first in new[] { 1, 51, 101, 151 })
        {
            store.StoreFromResponse(Loopback, [.. Enumerable.Range(first, 50).Select(number => $"k{number}=v")], Now);
        }

        string expected = string.Join("; ", Enumerable.Range(1, 150).Reverse().Where(number => number >= 100)
            .Concat(Enumerable.Range(10, 90).Reverse())
            .Concat(Enumerable.Range(1, 9).Reverse())
            .Select(number => $"k{number}=v"));
        Assert.AreEqual(expected, store.GetCookieHeader(Loopback, secure: false, Now));
    }

    /// <summary>
    /// Measured: <c>curl -b in.txt http://127.0.0.1:18220/</c> with these cookies in the file. A header of
    /// 8183 characters is sent whole; the cookie that would make it longer, and every cookie after it, is
    /// left out.
    /// </summary>
    [TestMethod]
    [DataRow("aaa:4000 bb:4000 c:164 dd:1", "aaa,dd,bb,c", 8183)]
    [DataRow("aaa:4000 bb:4000 c:165 dd:1", "aaa,dd,bb", 8015)]
    [DataRow("aaaa:4000 bbb:4000 cc:4000 d:1", "aaaa,bbb", 8011)]
    public void GetCookieHeader_LongCookies_StopsAtTheLongestHeader(string cookies, string expectedNames, int expectedLength)
    {
        CookieStore store = new();
        foreach (string cookie in cookies.Split(' '))
        {
            string[] nameAndLength = cookie.Split(':');
            store.StoreFromResponse(Loopback, [$"{nameAndLength[0]}={new string('x', int.Parse(nameAndLength[1], System.Globalization.CultureInfo.InvariantCulture))}"], Now);
        }

        string header = store.GetCookieHeader(Loopback, secure: false, Now)!;

        Assert.AreEqual(expectedLength, header.Length);
        Assert.AreEqual(expectedNames, string.Join(',', header.Split("; ").Select(pair => pair.Split('=')[0])));
    }

    /// <summary>
    /// Measured: <c>t3</c> has the path <c>/x/</c> and <c>t4</c> the path <c>/x/y</c>; curl sent <c>t4</c> to
    /// <c>/x/y</c> and <c>/x/y/z</c>, <c>t3</c> only to <c>/x/</c>, and nothing to <c>/</c>.
    /// </summary>
    [TestMethod]
    [DataRow("http://127.0.0.1/", null)]
    [DataRow("http://127.0.0.1/x/y", "t4=v")]
    [DataRow("http://127.0.0.1/x/", "t3=v")]
    [DataRow("http://127.0.0.1/x/y/z", "t4=v")]
    public void GetCookieHeader_Path_MatchesWholeSegments(string url, string? expectedHeader)
    {
        CookieStore store = new();
        store.StoreFromResponse(Loopback, ["t3=v; Path=/x//", "t4=v; Path=/x/y"], Now);

        Assert.AreEqual(expectedHeader, store.GetCookieHeader(new Uri(url), secure: false, Now));
    }

    /// <summary>libcurl's <c>remove_expired</c>: a cookie is dropped once its expiry is before the passed time.</summary>
    [TestMethod]
    [DataRow(10, "s=1; m=1")]
    [DataRow(11, "s=1")]
    public void GetCookieHeader_Expiry_UsesThePassedTime(int secondsLater, string expectedHeader)
    {
        CookieStore store = new();
        store.StoreFromResponse(Loopback, ["m=1; Max-Age=10", "s=1"], Now);

        Assert.AreEqual(expectedHeader, store.GetCookieHeader(Loopback, secure: false, Now.AddSeconds(secondsLater)));
        Assert.HasCount(expectedHeader.Split("; ").Length, store.Cookies);
    }

    [TestMethod]
    public void GetCookieHeader_IPv6Loopback_SendsItsSecureCookie()
    {
        CookieStore store = new();
        Uri ipv6 = new("http://[::1]/");
        store.StoreFromResponse(ipv6, ["s=1; Secure"], Now);

        Assert.AreEqual("s=1", store.GetCookieHeader(ipv6, secure: false, Now));
    }

    [TestMethod]
    public void GetCookieHeader_EmptyStore_ReturnsNull() =>
        Assert.IsNull(new CookieStore().GetCookieHeader(Www, secure: true, Now));

    [TestMethod]
    public void GetCookieHeader_NullUri_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CookieStore().GetCookieHeader(null!, secure: false, Now));

    [TestMethod]
    public void StoreFromResponse_NullUri_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CookieStore().StoreFromResponse(null!, [], Now));

    [TestMethod]
    public void StoreFromResponse_NullHeaders_Throws() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => new CookieStore().StoreFromResponse(Www, null!, Now));
}
