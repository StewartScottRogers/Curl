using Curl.Core.AltSvc;

namespace Curl.Console;

/// <summary>
/// Pins <see cref="AltSvcTransferCache.SourceAlpnOf" />, the source ALPN an <c>Alt-Svc</c> header is learned
/// under, against curl 8.21.0's <c>Curl_altsvc_parse</c> (BL-947).
/// </summary>
[TestClass]
public sealed class AltSvcTransferCacheTests
{
    [TestMethod]
    [DataRow(3, 0, AltSvcAlpn.H3)]
    [DataRow(2, 0, AltSvcAlpn.H2)]
    [DataRow(1, 1, AltSvcAlpn.H1)]
    [DataRow(1, 0, AltSvcAlpn.H1)]
    public void SourceAlpnOf_ResponseVersion_GivesCurlsSourceAlpn(int major, int minor, AltSvcAlpn expected)
    {
        Assert.AreEqual(expected, AltSvcTransferCache.SourceAlpnOf(new Version(major, minor)));
    }
}
