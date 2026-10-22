using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins curl's <c>Curl_cookie_getlist</c> rule for a request to an IP address: a cookie that includes subdomains is
/// sent only when its domain is that address, never by a tail match (upstream test8, BL-2858: <c>-b</c> reads
/// <c>Set-Cookie: partialip=nono; domain=.0.0.1;</c> from a header file and curl 8.22.0 does not send it to
/// <c>127.0.0.1</c>).
/// </summary>
public sealed partial class CookieStoreTests
{
    [TestMethod]
    [DataRow("Set-Cookie: partialip=nono; domain=.0.0.1;\n", null, DisplayName = "A partial IP domain from a header file")]
    [DataRow(".0.0.1\tTRUE\t/\tFALSE\t0\tpartialip\tnono\n", null, DisplayName = "A partial IP domain from a cookie file")]
    [DataRow(".127.0.0.1\tTRUE\t/\tFALSE\t0\tfull\tyes\n", "full=yes", DisplayName = "The host's own address, including subdomains")]
    public void GetCookieHeader_HostIsAnIpAddress_SendsOnlyACookieForThatExactAddress(string file, string? expectedHeader)
    {
        CookieStore store = new();
        Diagnostics.ArrangeText("cookie file", file);

        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now);
        string? header = store.GetCookieHeader(CurlUrl.Parse("http://127.0.0.1/we/want/8"), secure: false, Now);
        Diagnostics.Act("Cookie header", header);

        Diagnostics.Assert("Cookie header", expectedHeader, header);
        Assert.AreEqual(expectedHeader, header);
    }

    [TestMethod]
    public void GetCookieHeader_HostIsANameUnderTheCookieDomain_SendsTheTailmatchingCookie()
    {
        const string file = ".example.com\tTRUE\t/\tFALSE\t0\tn\tv\n";
        CookieStore store = new();
        Diagnostics.ArrangeText("cookie file", file);

        store.LoadCookieFile(new StringReader(file), discardSessionCookies: false, Now);
        string? header = store.GetCookieHeader(CurlUrl.Parse("http://www.example.com/"), secure: false, Now);
        Diagnostics.Act("Cookie header", header);

        Diagnostics.Assert("Cookie header", "n=v", header);
        Assert.AreEqual("n=v", header);
    }
}
