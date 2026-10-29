using Curl.Api.Codes;
using Curl.Api.EngineContracts.Url;

namespace Curl.Engine;

// CGR-256 (BL-3785): setting the host to a bracketed IPv6 address with a zone id keeps the zone in the
// host text, as libcurl 8.22.0 does, so the whole URL writes the zone twice.
public sealed partial class ManagedCurlUrlHandleTests
{
    [TestMethod]
    public void Set_HostWithZoneId_ReadsBackWithTheZoneAndTheUrlWritesItTwice()
    {
        using ManagedCurlUrlHandle url = Parsed("http://example.com/");

        Assert.AreEqual(CurlUrlCode.Ok, url.Set(CurlUrlPart.Host, "[fe80::1%25eth0]", CurlUrlFlags.None));

        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Host, CurlUrlFlags.None, out string? host));
        Assert.AreEqual("[fe80::1%25eth0]", host);
        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.ZoneId, CurlUrlFlags.None, out string? zone));
        Assert.AreEqual("eth0", zone);
        Assert.AreEqual(CurlUrlCode.Ok, url.Get(CurlUrlPart.Url, CurlUrlFlags.None, out string? whole));
        Assert.AreEqual("http://[fe80::1%25eth0%25eth0]/", whole);
    }
}
