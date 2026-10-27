using System.Net.Security;

using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// CGR-023 (BL-3552, ADR-0817): <c>--ciphers</c> is an OpenSSL cipher string, so keywords,
/// aliases, rule prefixes and <c>@</c> commands select suites in the order OpenSSL 3.5.5's
/// <c>openssl ciphers -s -tls1_2</c> lists them, as curl 8.18.0's OpenSSL build uses them.
/// </summary>
[TestClass]
public sealed class Cgr023Tests
{
    private const string Tls13Ciphers = "TLS_AES_128_GCM_SHA256";

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("DEFAULT")]
    [DataRow("DEFAULT@SECLEVEL=0")]
    [DataRow("DEFAULT:@SECLEVEL=5")]
    public void Select_WithDefaultKeyword_OffersOpenSslsDefaultList(string ciphers)
    {
        var suites = SelectTls12(ciphers);

        CollectionAssert.AreEqual(OpenSslCipherSuites.DefaultTls12Suites.ToArray(), suites);
    }

    [TestMethod]
    [DataRow("HIGH:!aNULL")]
    [DataRow("ALL")]
    public void Select_WithHighOrAll_OffersEveryAliasedSuiteInOpenSslsAllOrder(string ciphers)
    {
        var suites = SelectTls12(ciphers);

        CollectionAssert.AreEqual(OpenSslCipherSuites.AliasedTls12Names.Select(entry => entry.Suite).ToArray(), suites);
    }

    [TestMethod]
    public void Select_WithARsa_OffersTheRsaAuthenticatedSuitesInOpenSslsOrder()
    {
        var suites = SelectTls12("aRSA");

        AssertNames(
            [
                "ECDHE-RSA-AES256-GCM-SHA384", "DHE-RSA-AES256-GCM-SHA384", "ECDHE-RSA-CHACHA20-POLY1305",
                "DHE-RSA-CHACHA20-POLY1305", "ECDHE-RSA-AES128-GCM-SHA256", "DHE-RSA-AES128-GCM-SHA256",
                "ECDHE-RSA-AES256-SHA384", "DHE-RSA-AES256-SHA256", "ECDHE-RSA-AES128-SHA256", "DHE-RSA-AES128-SHA256",
                "ECDHE-RSA-AES256-SHA", "DHE-RSA-AES256-SHA", "ECDHE-RSA-AES128-SHA", "DHE-RSA-AES128-SHA",
                "AES256-GCM-SHA384", "AES128-GCM-SHA256", "AES256-SHA256", "AES128-SHA256", "AES256-SHA", "AES128-SHA",
            ],
            suites);
    }

    [TestMethod]
    [DataRow("ECDHE+AESGCM", "ECDHE-ECDSA-AES256-GCM-SHA384,ECDHE-RSA-AES256-GCM-SHA384,ECDHE-ECDSA-AES128-GCM-SHA256,ECDHE-RSA-AES128-GCM-SHA256")]
    [DataRow("kRSA:-AES128-SHA:AES128-SHA", "AES256-GCM-SHA384,AES128-GCM-SHA256,AES256-SHA256,AES128-SHA256,AES256-SHA,AES128-SHA")]
    [DataRow("AES128-SHA:AES256-SHA:+AES128-SHA", "AES256-SHA,AES128-SHA")]
    [DataRow("AES128-SHA:AES256-SHA@STRENGTH", "AES256-SHA,AES128-SHA")]
    [DataRow("DHE-DSS-DES-CBC3-SHA:AES128-SHA:AES256-SHA:@STRENGTH", "AES256-SHA,AES128-SHA,DHE-DSS-DES-CBC3-SHA")]
    [DataRow("BOGUS+AES:AES128-SHA", "AES128-SHA")]
    [DataRow("aDSS+SHA1+CAMELLIA256;TLS_RSA_WITH_AES_128_CBC_SHA", "DHE-DSS-CAMELLIA256-SHA,AES128-SHA")]
    public void Select_WithRulesAndCommands_AppliesThemAsOpenSslDoes(string ciphers, string expectedNames)
    {
        var suites = SelectTls12(ciphers);

        AssertNames(expectedNames.Split(','), suites);
    }

    [TestMethod]
    [DataRow("@FOO")]
    [DataRow("!!")]
    [DataRow("@")]
    [DataRow("DEFAULT@SECLEVEL=6")]
    [DataRow("DEFAULT@SECLEVEL=10")]
    [DataRow("DEFAULT@SECLEVELX0")]
    [DataRow("DEFAULT@SECLEVEL=-")]
    [DataRow("!AES128-SHA:AES128-SHA")]
    public void Select_WithAnInvalidCommandOrNoSuiteLeft_FailsWithTheCipherListMessage(string ciphers)
    {
        Diagnostics.Arrange("--ciphers", ciphers);

        var (suites, failureMessage) = OpenSslCipherSuites.Select(ciphers, Tls13Ciphers);

        Diagnostics.Diff("failure message", $"failed setting cipher list: {ciphers}", failureMessage ?? "null");
        Assert.IsNull(suites);
        Assert.AreEqual($"failed setting cipher list: {ciphers}", failureMessage);
    }

    private TlsCipherSuite[] SelectTls12(string ciphers)
    {
        Diagnostics.Arrange("--ciphers", ciphers);

        var (suites, failureMessage) = OpenSslCipherSuites.Select(ciphers, Tls13Ciphers);

        Diagnostics.Assert("failure message", "null", failureMessage ?? "null");
        Assert.IsNull(failureMessage);
        Assert.AreEqual(TlsCipherSuite.TLS_AES_128_GCM_SHA256, suites![0]);
        return suites.Skip(1).ToArray();
    }

    private void AssertNames(string[] expectedNames, TlsCipherSuite[] suites)
    {
        var expected = expectedNames.Select(name => OpenSslCipherSuites.Find(name)!.Value).ToArray();
        Diagnostics.Diff("suites", string.Join(":", expected), string.Join(":", suites));
        CollectionAssert.AreEqual(expected, suites);
    }
}
