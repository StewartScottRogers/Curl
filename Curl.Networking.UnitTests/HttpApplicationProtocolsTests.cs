namespace Curl.Networking;

/// <summary>
/// Pins the ALPN lists curl offers for HTTP over TLS, as ADR-0141 measured them.
/// </summary>
[TestClass]
public sealed class HttpApplicationProtocolsTests
{
    [TestMethod]
    public void Lists_AreCurlsMeasuredOffers()
    {
        CollectionAssert.AreEqual(new[] { "http/1.1" }, HttpApplicationProtocols.Http11Only.ToArray());
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, HttpApplicationProtocols.H2ThenHttp11.ToArray());
        CollectionAssert.AreEqual(new[] { "h2" }, HttpApplicationProtocols.H2Only.ToArray());
    }
}
