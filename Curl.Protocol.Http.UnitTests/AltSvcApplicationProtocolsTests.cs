using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Protocol.Http;

/// <summary>
/// Pins what a TLS connect to an Alt-Svc alternative offers through ALPN, as curl.se's 8.18.0 build
/// offered it (measured, BL-733 Notes cases 4 and 5, BL-948): the switched-to version alone, and the
/// connector's own list for an alternative of the version it was found under.
/// </summary>
[TestClass]
public sealed class AltSvcApplicationProtocolsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Of_NoRoute_IsNullForTheConnectorsOwnList()
    {
        Diagnostics.Arrange("route", "none");

        IReadOnlyList<string>? protocols = AltSvcApplicationProtocols.Of(null);

        Diagnostics.Act("offered protocols", Describe(protocols));
        Diagnostics.Assert("offered protocols", "null (the connector's own list)", Describe(protocols));
        Assert.IsNull(protocols);
    }

    [TestMethod]
    [DataRow("h1", "h2", "h2", DisplayName = "an h1 origin switching to h2 offers h2 alone")]
    [DataRow("h2", "h1", "http/1.1", DisplayName = "an h2 origin switching to h1 offers http/1.1 alone")]
    [DataRow("h3", "h2", "h2", DisplayName = "an h3 origin switching to h2 offers h2 alone")]
    [DataRow("h3", "h1", "http/1.1", DisplayName = "an h3 origin switching to h1 offers http/1.1 alone")]
    public void Of_AnAlternativeThatSwitchesVersion_OffersItsVersionAlone(string originAlpn, string alternativeAlpn, string offered)
    {
        Diagnostics.Arrange("origin ALPN", originAlpn);
        Diagnostics.Arrange("alternative ALPN", alternativeAlpn);

        IReadOnlyList<string>? protocols = AltSvcApplicationProtocols.Of(new AltSvcRoute(originAlpn, new AltSvcAlternative(alternativeAlpn, "localhost", 18444)));

        Diagnostics.Act("offered protocols", Describe(protocols));
        Diagnostics.Assert("offered protocols", offered, Describe(protocols));
        CollectionAssert.AreEqual(new[] { offered }, protocols!.ToArray());
    }

    [TestMethod]
    [DataRow("h1", "h1", DisplayName = "an h1 alternative found under h1")]
    [DataRow("h2", "h2", DisplayName = "an h2 alternative found under h2")]
    [DataRow("h1", "h3", DisplayName = "an h3 alternative, which QUIC carries")]
    public void Of_AnAlternativeThatDoesNotSwitchToTcpVersion_IsNullForTheConnectorsOwnList(string originAlpn, string alternativeAlpn)
    {
        Diagnostics.Arrange("origin ALPN", originAlpn);
        Diagnostics.Arrange("alternative ALPN", alternativeAlpn);

        IReadOnlyList<string>? protocols = AltSvcApplicationProtocols.Of(new AltSvcRoute(originAlpn, new AltSvcAlternative(alternativeAlpn, "localhost", 18444)));

        Diagnostics.Act("offered protocols", Describe(protocols));
        Diagnostics.Assert("offered protocols", "null (the connector's own list)", Describe(protocols));
        Assert.IsNull(protocols);
    }

    private static string Describe(IReadOnlyList<string>? protocols) =>
        protocols is null ? "null (the connector's own list)" : string.Join(", ", protocols);
}
