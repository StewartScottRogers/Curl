namespace Curl.Core.AltSvc;

/// <summary>Pins the ALPN tokens <see cref="AltSvcAlpnToken" /> reads and writes.</summary>
[TestClass]
public sealed class AltSvcAlpnTokenTests
{
    [TestMethod]
    [DataRow(AltSvcAlpn.H1, "h1")]
    [DataRow(AltSvcAlpn.H2, "h2")]
    [DataRow(AltSvcAlpn.H3, "h3")]
    public void FormatAndParse_EachVersion_RoundTrip(AltSvcAlpn alpn, string token)
    {
        Assert.AreEqual(token, AltSvcAlpnToken.Format(alpn));
        Assert.AreEqual(alpn, AltSvcAlpnToken.Parse(token));
    }

    [TestMethod]
    [DataRow("H2")]
    [DataRow("h4")]
    [DataRow("")]
    public void Parse_UnknownToken_GivesNull(string token) =>
        Assert.IsNull(AltSvcAlpnToken.Parse(token));
}
