using Curl.Protocol.Abstractions;

namespace Curl.Cookies;

/// <summary>
/// Pins BL-4929: a <c>Set-Cookie:</c> line without a <c>Domain</c> in a <c>-b</c> file is sent only to a host that
/// is an IP address, as curl 8.22.0 does (outside review finding STD-019).
/// </summary>
[TestClass]
public sealed class DomainlessCookieMatchTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_790_460_351);

    [TestMethod]
    [DataRow("http://localhost:18200/show", null)]
    [DataRow("http://www.example.test:18200/show", null)]
    [DataRow("http://127.0.0.1:18200/show", "hdrstyle=v11")]
    public void GetCookieHeader_DomainlessSetCookieLine_GoesOnlyToIpAddressHosts(string address, string? expected)
    {
        CookieStore store = new();
        store.LoadCookieFile(new StringReader("Set-Cookie: hdrstyle=v11; path=/\n"), discardSessionCookies: false, Now);

        string? actual = store.GetCookieHeader(CurlUrl.Parse(address), secure: false, Now);

        Assert.AreEqual(expected, actual);
    }
}
