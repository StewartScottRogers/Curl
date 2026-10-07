using Curl.Testing;

namespace Curl.Core.AltSvc;

/// <summary>Pins the ALPN tokens <see cref="AltSvcAlpnToken" /> reads and writes.</summary>
[TestClass]
public sealed class AltSvcAlpnTokenTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(AltSvcAlpn.H1, "h1")]
    [DataRow(AltSvcAlpn.H2, "h2")]
    [DataRow(AltSvcAlpn.H3, "h3")]
    public void FormatAndParse_EachVersion_RoundTrip(AltSvcAlpn alpn, string token)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("alpn", alpn);
        diagnostics.Arrange("token", token);

        var formatted = AltSvcAlpnToken.Format(alpn);
        var parsed = AltSvcAlpnToken.Parse(token);

        diagnostics.Act("formatted", formatted);
        diagnostics.Act("parsed", parsed);
        diagnostics.Assert("formatted", token, formatted);
        diagnostics.Assert("parsed", alpn, parsed);
        Assert.AreEqual(token, formatted);
        Assert.AreEqual(alpn, parsed);
    }

    [TestMethod]
    [DataRow("H2")]
    [DataRow("h4")]
    [DataRow("")]
    public void Parse_UnknownToken_GivesNull(string token)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("token", token);

        var parsed = AltSvcAlpnToken.Parse(token);

        diagnostics.Act("parsed", parsed?.ToString() ?? "(null)");
        diagnostics.Assert("parsed", "(null)", parsed?.ToString() ?? "(null)");
        Assert.IsNull(parsed);
    }
}
