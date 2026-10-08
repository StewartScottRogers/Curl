using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// Pins the ALPN lists curl offers for HTTP over TLS, as ADR-0141 measured them.
/// </summary>
[TestClass]
public sealed class HttpApplicationProtocolsTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Lists_AreCurlsMeasuredOffers()
    {
        Diagnostics.Arrange("measured offers", "ADR-0141: http/1.1 | h2, http/1.1 | h2");

        var http11Only = HttpApplicationProtocols.Http11Only.ToArray();
        var h2ThenHttp11 = HttpApplicationProtocols.H2ThenHttp11.ToArray();
        var h2Only = HttpApplicationProtocols.H2Only.ToArray();

        Diagnostics.Act("Http11Only", string.Join(", ", http11Only));
        Diagnostics.Act("H2ThenHttp11", string.Join(", ", h2ThenHttp11));
        Diagnostics.Act("H2Only", string.Join(", ", h2Only));
        Diagnostics.Assert("Http11Only", "http/1.1", string.Join(", ", http11Only));
        Diagnostics.Assert("H2ThenHttp11", "h2, http/1.1", string.Join(", ", h2ThenHttp11));
        Diagnostics.Assert("H2Only", "h2", string.Join(", ", h2Only));
        CollectionAssert.AreEqual(new[] { "http/1.1" }, HttpApplicationProtocols.Http11Only.ToArray());
        CollectionAssert.AreEqual(new[] { "h2", "http/1.1" }, HttpApplicationProtocols.H2ThenHttp11.ToArray());
        CollectionAssert.AreEqual(new[] { "h2" }, HttpApplicationProtocols.H2Only.ToArray());
    }
}
