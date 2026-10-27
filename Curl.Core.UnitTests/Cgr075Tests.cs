using Curl.Core.AltSvc;

namespace Curl.Core;

/// <summary>
/// Pins CGR-075 of the 2026-10-25 gap report: curl's <c>Curl_alpn2alpnid</c> reads the ALPN id
/// <c>http/1.1</c> as <c>h1</c>, so an <c>Alt-Svc: http/1.1="localhost:20502"</c> header is stored
/// as an <c>h1</c> entry, where <see cref="AltSvcAlpnToken" /> used to ignore it. The match is
/// case-sensitive: curl drops <c>HTTP/1.1</c> as it drops <c>H2</c> (BL-3604 Notes).
/// </summary>
[TestClass]
public sealed class Cgr075Tests
{
    [TestMethod]
    public void Parse_Http11Token_IsH1()
    {
        Assert.AreEqual(AltSvcAlpn.H1, AltSvcAlpnToken.Parse("http/1.1"));
    }

    [TestMethod]
    public void Parse_UppercaseHttp11Token_IsUnknown()
    {
        Assert.IsNull(AltSvcAlpnToken.Parse("HTTP/1.1"));
    }

    [TestMethod]
    public void HeaderParse_Http11AndH2List_KeepsBothAlternatives()
    {
        // curl 8.21.0 -sSk --alt-svc, Alt-Svc: http/1.1=":20502", h2=":443" -> an h1 and an h2 line.
        AltSvcHeader header = AltSvcHeaderParser.Parse("http/1.1=\":20502\", h2=\":443\"");

        CollectionAssert.AreEqual(
            new[] { (AltSvcAlpn.H1, 20502), (AltSvcAlpn.H2, 443) },
            header.Alternatives.Select(alternative => (alternative.Alpn, alternative.Port)).ToArray());
    }
}
