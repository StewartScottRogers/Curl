using Curl.Protocol.Abstractions;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what a TLS connect to an Alt-Svc alternative offers through ALPN, as curl.se's 8.18.0 build
/// offered it (measured, BL-733 Notes cases 4 and 5, BL-948): the switched-to version alone, and the
/// connector's own list for an alternative of the version it was found under.
/// </summary>
[TestClass]
public sealed class AltSvcApplicationProtocolsTests
{
    [TestMethod]
    public void Of_NoRoute_IsNullForTheConnectorsOwnList()
    {
        Assert.IsNull(AltSvcApplicationProtocols.Of(null));
    }

    [TestMethod]
    [DataRow("h1", "h2", "h2", DisplayName = "an h1 origin switching to h2 offers h2 alone")]
    [DataRow("h2", "h1", "http/1.1", DisplayName = "an h2 origin switching to h1 offers http/1.1 alone")]
    [DataRow("h3", "h2", "h2", DisplayName = "an h3 origin switching to h2 offers h2 alone")]
    [DataRow("h3", "h1", "http/1.1", DisplayName = "an h3 origin switching to h1 offers http/1.1 alone")]
    public void Of_AnAlternativeThatSwitchesVersion_OffersItsVersionAlone(string originAlpn, string alternativeAlpn, string offered)
    {
        IReadOnlyList<string>? protocols = AltSvcApplicationProtocols.Of(new AltSvcRoute(originAlpn, new AltSvcAlternative(alternativeAlpn, "localhost", 18444)));

        CollectionAssert.AreEqual(new[] { offered }, protocols!.ToArray());
    }

    [TestMethod]
    [DataRow("h1", "h1", DisplayName = "an h1 alternative found under h1")]
    [DataRow("h2", "h2", DisplayName = "an h2 alternative found under h2")]
    [DataRow("h1", "h3", DisplayName = "an h3 alternative, which QUIC carries")]
    public void Of_AnAlternativeThatDoesNotSwitchToTcpVersion_IsNullForTheConnectorsOwnList(string originAlpn, string alternativeAlpn)
    {
        Assert.IsNull(AltSvcApplicationProtocols.Of(new AltSvcRoute(originAlpn, new AltSvcAlternative(alternativeAlpn, "localhost", 18444))));
    }
}
