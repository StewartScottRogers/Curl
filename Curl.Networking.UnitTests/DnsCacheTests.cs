using System.Net;

namespace Curl.Networking;

/// <summary>Pins that <see cref="DnsCache.Clear" /> drops every entry, as libcurl's <c>CURLMNWC_CLEAR_DNS</c> does (BL-2427).</summary>
[TestClass]
public sealed class DnsCacheTests
{
    [TestMethod]
    public void Clear_TwoEntries_DropsBoth()
    {
        DnsCache cache = new();
        cache.Store("one.example:80", new DnsCacheEntry("one.example", 80, [IPAddress.Loopback]));
        cache.Store("two.example:443", new DnsCacheEntry("two.example", 443, [IPAddress.IPv6Loopback]));

        cache.Clear();

        Assert.IsFalse(cache.Contains("one.example:80"));
        Assert.IsFalse(cache.Contains("two.example:443"));
    }
}
