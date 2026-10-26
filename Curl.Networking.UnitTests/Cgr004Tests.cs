using System.Security.Authentication;

using Curl.Protocol.Abstractions;

namespace Curl.Networking;

/// <summary>
/// CGR-004 of the third-party gap report (BL-3533): with no TLS version option curl 8.22.0's
/// Schannel build offers only TLS 1.3 and TLS 1.2 (supported_versions <c>00 2b 00 05 04 03 04 03 03</c>)
/// and refuses a TLS 1.0 or 1.1-only server with exit 35, so the default range is TLS 1.2 to 1.3,
/// never the operating system's own default, which offers TLS 1.1 and 1.0 too.
/// </summary>
[TestClass]
public sealed class Cgr004Tests
{
    [TestMethod]
    [DataRow(TlsVersion.SystemDefault)]
    [DataRow(TlsVersion.Tls13)]
    public void ToSslProtocols_WithNoMinimum_OffersOnlyTls12AndTls13(TlsVersion maximum)
    {
        var offered = TlsVersionRange.ToSslProtocols(TlsVersion.SystemDefault, maximum);

        Assert.AreEqual(SslProtocols.Tls12 | SslProtocols.Tls13, offered);
    }
}
