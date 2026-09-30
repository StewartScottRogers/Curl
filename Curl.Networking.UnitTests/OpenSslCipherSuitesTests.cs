using System.Net.Security;

namespace Curl.Networking;

/// <summary>
/// <see cref="OpenSslCipherSuites" />: every entry of the OpenSSL name table, and how the
/// <c>--ciphers</c> and <c>--tls13-ciphers</c> values become one list of suites or an
/// exit 59 message, as ADR-0011 decides from the measured OpenSSL build of curl.
/// </summary>
[TestClass]
public sealed class OpenSslCipherSuitesTests
{
    [TestMethod]
    [DataRow("ECDHE-ECDSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384)]
    [DataRow("ECDHE-RSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384)]
    [DataRow("DHE-RSA-AES256-GCM-SHA384", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_GCM_SHA384)]
    [DataRow("ECDHE-ECDSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_CHACHA20_POLY1305_SHA256)]
    [DataRow("ECDHE-RSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_ECDHE_RSA_WITH_CHACHA20_POLY1305_SHA256)]
    [DataRow("DHE-RSA-CHACHA20-POLY1305", TlsCipherSuite.TLS_DHE_RSA_WITH_CHACHA20_POLY1305_SHA256)]
    [DataRow("ECDHE-ECDSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_GCM_SHA256)]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256)]
    [DataRow("DHE-RSA-AES128-GCM-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_GCM_SHA256)]
    [DataRow("ECDHE-ECDSA-AES256-SHA384", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA384)]
    [DataRow("ECDHE-RSA-AES256-SHA384", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA384)]
    [DataRow("DHE-RSA-AES256-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA256)]
    [DataRow("ECDHE-ECDSA-AES128-SHA256", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA256)]
    [DataRow("ECDHE-RSA-AES128-SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA256)]
    [DataRow("DHE-RSA-AES128-SHA256", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA256)]
    [DataRow("ECDHE-ECDSA-AES256-SHA", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_CBC_SHA)]
    [DataRow("ECDHE-RSA-AES256-SHA", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_CBC_SHA)]
    [DataRow("DHE-RSA-AES256-SHA", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_256_CBC_SHA)]
    [DataRow("ECDHE-ECDSA-AES128-SHA", TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_128_CBC_SHA)]
    [DataRow("ECDHE-RSA-AES128-SHA", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_CBC_SHA)]
    [DataRow("DHE-RSA-AES128-SHA", TlsCipherSuite.TLS_DHE_RSA_WITH_AES_128_CBC_SHA)]
    [DataRow("AES256-GCM-SHA384", TlsCipherSuite.TLS_RSA_WITH_AES_256_GCM_SHA384)]
    [DataRow("AES128-GCM-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_128_GCM_SHA256)]
    [DataRow("AES256-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA256)]
    [DataRow("AES128-SHA256", TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA256)]
    [DataRow("AES256-SHA", TlsCipherSuite.TLS_RSA_WITH_AES_256_CBC_SHA)]
    [DataRow("AES128-SHA", TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA)]
    public void Find_WithAnOpenSslName_ReturnsItsSuite(string openSslName, TlsCipherSuite expected)
    {
        Assert.AreEqual(expected, OpenSslCipherSuites.Find(openSslName));
    }

    [TestMethod]
    [DataRow("DHE-DSS-AES128-SHA", (ushort)0x0032)]
    [DataRow("DHE-DSS-AES256-SHA", (ushort)0x0038)]
    [DataRow("DHE-DSS-AES128-SHA256", (ushort)0x0040)]
    [DataRow("DHE-DSS-AES256-SHA256", (ushort)0x006a)]
    [DataRow("DHE-DSS-AES128-GCM-SHA256", (ushort)0x00a2)]
    [DataRow("DHE-DSS-AES256-GCM-SHA384", (ushort)0x00a3)]
    [DataRow("DHE-DSS-CAMELLIA128-SHA", (ushort)0x0044)]
    [DataRow("DHE-DSS-CAMELLIA256-SHA", (ushort)0x0087)]
    [DataRow("DHE-DSS-CAMELLIA128-SHA256", (ushort)0x00bd)]
    [DataRow("DHE-DSS-CAMELLIA256-SHA256", (ushort)0x00c3)]
    [DataRow("DHE-DSS-ARIA128-GCM-SHA256", (ushort)0xc056)]
    [DataRow("DHE-DSS-ARIA256-GCM-SHA384", (ushort)0xc057)]
    [DataRow("DHE-DSS-DES-CBC3-SHA", (ushort)0x0013)]
    public void Find_WithADheDssName_ReturnsItsCodePoint(string openSslName, ushort expectedCodePoint)
    {
        Assert.AreEqual((TlsCipherSuite)expectedCodePoint, OpenSslCipherSuites.Find(openSslName));
    }

    [TestMethod]
    public void DefaultTls12Suites_AreEveryOpenSslNamedSuiteOnce()
    {
        Assert.HasCount(27, OpenSslCipherSuites.DefaultTls12Suites);
        Assert.HasCount(27, OpenSslCipherSuites.DefaultTls12Suites.Distinct());
        Assert.AreEqual(TlsCipherSuite.TLS_ECDHE_ECDSA_WITH_AES_256_GCM_SHA384, OpenSslCipherSuites.DefaultTls12Suites[0]);
    }

    [TestMethod]
    public void DefaultTls12Suites_WhenCiphersIsAbsent_HaveNoDheDssSuite()
    {
        Assert.IsFalse(OpenSslCipherSuites.DefaultTls12Suites.Any(suite => suite.ToString().StartsWith("TLS_DHE_DSS_", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void Select_WithADheDssName_OffersItAfterTheDefaultTls13Suites()
    {
        var (suites, failureMessage) = OpenSslCipherSuites.Select("DHE-DSS-AES128-GCM-SHA256", null);

        Assert.IsNull(failureMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                TlsCipherSuite.TLS_AES_256_GCM_SHA384,
                TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                TlsCipherSuite.TLS_AES_128_GCM_SHA256,
                TlsCipherSuite.TLS_DHE_DSS_WITH_AES_128_GCM_SHA256,
            },
            suites!.ToArray());
    }

    [TestMethod]
    public void DefaultTls13Suites_AreOpenSslsDefaultInItsOrder()
    {
        CollectionAssert.AreEqual(
            new[]
            {
                TlsCipherSuite.TLS_AES_256_GCM_SHA384,
                TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                TlsCipherSuite.TLS_AES_128_GCM_SHA256,
            },
            OpenSslCipherSuites.DefaultTls13Suites.ToArray());
    }

    [TestMethod]
    [DataRow("TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256", TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256)]
    [DataRow("TLS_AES_128_GCM_SHA256", TlsCipherSuite.TLS_AES_128_GCM_SHA256)]
    [DataRow("TLS_PSK_WITH_AES_128_GCM_SHA256", TlsCipherSuite.TLS_PSK_WITH_AES_128_GCM_SHA256)]
    public void Find_WithAnIanaName_ReturnsItsSuite(string ianaName, TlsCipherSuite expected)
    {
        Assert.AreEqual(expected, OpenSslCipherSuites.Find(ianaName));
    }

    [TestMethod]
    [DataRow("BOGUS")]
    [DataRow("TLS_BOGUS")]
    [DataRow("49199")]
    [DataRow("ecdhe-rsa-aes128-gcm-sha256")]
    [DataRow("tls_aes_128_gcm_sha256")]
    [DataRow("HIGH")]
    [DataRow("!aNULL")]
    public void Find_WithANameNeitherTableHolds_ReturnsNull(string entry)
    {
        Assert.IsNull(OpenSslCipherSuites.Find(entry));
    }

    [TestMethod]
    public void Select_WithNeitherOption_SetsNoPolicy()
    {
        var (suites, failureMessage) = OpenSslCipherSuites.Select(null, null);

        Assert.IsNull(suites);
        Assert.IsNull(failureMessage);
    }

    [TestMethod]
    public void Select_WithCiphersOnly_OffersTheDefaultTls13SuitesThenTheNamedOnes()
    {
        var (suites, failureMessage) = OpenSslCipherSuites.Select("ECDHE-RSA-AES128-GCM-SHA256", null);

        Assert.IsNull(failureMessage);
        CollectionAssert.AreEqual(
            new[]
            {
                TlsCipherSuite.TLS_AES_256_GCM_SHA384,
                TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                TlsCipherSuite.TLS_AES_128_GCM_SHA256,
                TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
            },
            suites!.ToArray());
    }

    [TestMethod]
    public void Select_WithTls13CiphersOnly_OffersTheNamedOnesThenEveryTableSuite()
    {
        var (suites, failureMessage) = OpenSslCipherSuites.Select(null, "TLS_AES_128_GCM_SHA256");

        Assert.IsNull(failureMessage);
        CollectionAssert.AreEqual(
            new[] { TlsCipherSuite.TLS_AES_128_GCM_SHA256 }.Concat(OpenSslCipherSuites.DefaultTls12Suites).ToArray(),
            suites!.ToArray());
    }

    [TestMethod]
    [DataRow("ECDHE-RSA-AES256-GCM-SHA384:TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256")]
    [DataRow("ECDHE-RSA-AES256-GCM-SHA384,TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256")]
    [DataRow("ECDHE-RSA-AES256-GCM-SHA384 TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256")]
    [DataRow(":ECDHE-RSA-AES256-GCM-SHA384, :TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256:")]
    public void Select_WithAListSplitOnAnyOpenSslSeparator_KeepsItsOrder(string ciphers)
    {
        var (suites, _) = OpenSslCipherSuites.Select(ciphers, "TLS_AES_128_GCM_SHA256");

        CollectionAssert.AreEqual(
            new[]
            {
                TlsCipherSuite.TLS_AES_128_GCM_SHA256,
                TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384,
                TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256,
            },
            suites!.ToArray());
    }

    [TestMethod]
    public void Select_WithUnknownRepeatedAndTls13EntriesInCiphers_DropsThem()
    {
        var (suites, _) = OpenSslCipherSuites.Select(
            "BOGUS:ECDHE-RSA-AES128-GCM-SHA256:TLS_AES_256_GCM_SHA384:TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256",
            "TLS_AES_128_GCM_SHA256");

        CollectionAssert.AreEqual(
            new[] { TlsCipherSuite.TLS_AES_128_GCM_SHA256, TlsCipherSuite.TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256 },
            suites!.ToArray());
    }

    [TestMethod]
    public void Select_WithUnknownAndTls12EntriesInTls13Ciphers_DropsThem()
    {
        var (suites, _) = OpenSslCipherSuites.Select(
            "AES128-SHA",
            "TLS_AES_128_GCM_SHA256:BOGUS:ECDHE-RSA-AES128-GCM-SHA256:TLS_CHACHA20_POLY1305_SHA256");

        CollectionAssert.AreEqual(
            new[]
            {
                TlsCipherSuite.TLS_AES_128_GCM_SHA256,
                TlsCipherSuite.TLS_CHACHA20_POLY1305_SHA256,
                TlsCipherSuite.TLS_RSA_WITH_AES_128_CBC_SHA,
            },
            suites!.ToArray());
    }

    [TestMethod]
    [DataRow("BOGUS")]
    [DataRow("HIGH:!aNULL")]
    [DataRow("TLS_AES_128_GCM_SHA256")]
    [DataRow("")]
    public void Select_WithCiphersNamingNoTls12Suite_FailsWithTheCipherListMessage(string ciphers)
    {
        var (suites, failureMessage) = OpenSslCipherSuites.Select(ciphers, "BOGUS");

        Assert.IsNull(suites);
        Assert.AreEqual($"failed setting cipher list: {ciphers}", failureMessage);
    }

    [TestMethod]
    [DataRow("BOGUS")]
    [DataRow("ECDHE-RSA-AES128-GCM-SHA256")]
    public void Select_WithTls13CiphersNamingNoTls13Suite_FailsWithTheTls13CipherSuiteMessage(string tls13Ciphers)
    {
        var (suites, failureMessage) = OpenSslCipherSuites.Select(null, tls13Ciphers);

        Assert.IsNull(suites);
        Assert.AreEqual($"failed setting TLS 1.3 cipher suite: {tls13Ciphers}", failureMessage);
    }

    [TestMethod]
    public void Unapplied_WithCiphers_IsTheCipherListMessage()
    {
        Assert.AreEqual(
            "failed setting cipher list: AES128-SHA",
            OpenSslCipherSuites.Unapplied("AES128-SHA", "TLS_AES_128_GCM_SHA256"));
    }

    [TestMethod]
    public void Unapplied_WithTls13CiphersOnly_IsTheTls13CipherSuiteMessage()
    {
        Assert.AreEqual(
            "failed setting TLS 1.3 cipher suite: TLS_AES_128_GCM_SHA256",
            OpenSslCipherSuites.Unapplied(null, "TLS_AES_128_GCM_SHA256"));
    }
}
