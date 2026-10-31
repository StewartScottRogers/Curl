using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;
using static Libcurl.UnitTests.Options.Tls.TlsOptionAssert;

namespace Libcurl.UnitTests;

// Flips the process-wide build flags of EasyTlsOptionSetters and EasyNetworkOptionSetters, so it must not run beside another test that reads them (ADR-0995).
[TestClass]
[DoNotParallelize]
public sealed class Cgr265LastPartTests
{
    [TestMethod]
    public void BuildFlags_ByDefault_MatchThePlatformsCurl()
    {
        var openSsl = !OperatingSystem.IsWindows();

        Assert.AreEqual(openSsl, EasyTlsOptionSetters.RevocationListFileIsBuiltIn);
        Assert.AreEqual(openSsl, EasyTlsOptionSetters.IssuerCertificateIsBuiltIn);
        Assert.AreEqual(openSsl, EasyTlsOptionSetters.EllipticCurvesIsBuiltIn);
        Assert.AreEqual(openSsl, EasyTlsOptionSetters.SignatureAlgorithmsIsBuiltIn);
        Assert.AreEqual(openSsl, EasyTlsOptionSetters.OpenSslEngineIsBuiltIn);
        Assert.AreEqual(openSsl, EasyNetworkOptionSetters.TcpFastOpenIsBuiltIn);
        Assert.IsTrue(EasyTlsOptionSetters.CertificateStatusIsBuiltIn);
        Assert.IsTrue(EasyTlsOptionSetters.EncryptedClientHelloIsBuiltIn);
        Assert.IsTrue(EasyNetworkOptionSetters.AresIsBuiltIn);
    }

    [TestMethod]
    [DataRow(CurlOption.Crlfile, "crl.pem", CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.ProxyCrlfile, "crl.pem", CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.Issuercert, "issuer.pem", CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.ProxyIssuercert, "issuer.pem", CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.IssuercertBlob, null, CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.ProxyIssuercertBlob, null, CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.SslEcCurves, "X25519", CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.SslSignatureAlgorithms, "ECDSA+SHA256", CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.SslVerifystatus, 1L, CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.DohSslVerifystatus, 1L, CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.Ech, "true", CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.Ech, null, CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.Sslengine, "pkcs11", CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.SslengineDefault, 1L, CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.TcpFastopen, 1L, CurlCode.NotBuiltIn)]
    [DataRow(CurlOption.DnsServers, "192.0.2.1", CurlCode.UnknownOption)]
    [DataRow(CurlOption.DnsInterface, "eth0", CurlCode.UnknownOption)]
    [DataRow(CurlOption.DnsLocalIp4, "192.0.2.2", CurlCode.UnknownOption)]
    [DataRow(CurlOption.DnsLocalIp6, "2001:db8::2", CurlCode.UnknownOption)]
    public void SetOption_OptionTheBuildLacks_AnswersItsCodeAndChangesNothing(CurlOption option, object? value, CurlCode expected)
    {
        WithFlags(false, () => AnswersAndChangesNothing(expected, option, value));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("private")]
    public void SetOption_Krblevel_IsNotBuiltInForEveryValue(string? level)
    {
        AnswersAndChangesNothing(CurlCode.NotBuiltIn, CurlOption.Krblevel, level);
    }

    [TestMethod]
    public void SetOption_CrlfileBuiltIn_SetsTheOriginAndProxyCrlfileTheProxy()
    {
        WithFlags(true, () => TwinText(CurlOption.Crlfile, CurlOption.ProxyCrlfile, p => p.RevocationListFile));
    }

    [TestMethod]
    public void SetOption_IssuercertBuiltIn_SetsTheOriginAndProxyIssuercertTheProxy()
    {
        WithFlags(true, () => TwinText(CurlOption.Issuercert, CurlOption.ProxyIssuercert, p => p.IssuerCertificate));
    }

    [TestMethod]
    public void SetOption_IssuercertBlobBuiltIn_CopiesTheBytesIntoTheOriginAndProxyIssuercertBlobIntoTheProxy()
    {
        WithFlags(true, () => TwinBlob(CurlOption.IssuercertBlob, CurlOption.ProxyIssuercertBlob, p => p.IssuerCertificateBlob));
    }

    [TestMethod]
    public void SetOption_SslEcCurvesBuiltIn_SetsTheCurvesAndNullClearsThem()
    {
        WithFlags(true, () => Text(CurlOption.SslEcCurves, t => t.EllipticCurves));
    }

    [TestMethod]
    public void SetOption_SslSignatureAlgorithmsBuiltIn_SetsTheAlgorithmsAndNullClearsThem()
    {
        WithFlags(true, () => Text(CurlOption.SslSignatureAlgorithms, t => t.SignatureAlgorithms));
    }

    [TestMethod]
    public void SetOption_VerifystatusBuiltIn_IsOnForNonZeroAndOffForZeroForTheOriginAndDoh()
    {
        WithFlags(true, () =>
        {
            Switch(CurlOption.SslVerifystatus, t => t.VerifyStatus);
            Switch(CurlOption.DohSslVerifystatus, t => t.DohVerifyStatus);
        });
    }

    [TestMethod]
    public void SetOption_SslengineWithOpenSsl_AnswersEngineNotFoundForANameAndClearsForNullOrEmpty()
    {
        WithFlags(true, () =>
        {
            AnswersAndChangesNothing(CurlCode.SslEngineNotFound, CurlOption.Sslengine, "pkcs11");
            Assert.IsNull(SetOk(CurlOption.Sslengine, null).Engine);
            Assert.IsNull(SetOk(CurlOption.Sslengine, string.Empty).Engine);
            Assert.AreEqual(CurlCode.Ok, Set(new EasyOptions(), CurlOption.SslengineDefault, 1L));
        });
    }

    [TestMethod]
    public void SetOption_SslengineWithSchannel_StillClearsForNullOrEmpty()
    {
        WithFlags(false, () =>
        {
            Assert.IsNull(SetOk(CurlOption.Sslengine, null).Engine);
            Assert.IsNull(SetOk(CurlOption.Sslengine, string.Empty).Engine);
        });
    }

    [TestMethod]
    public void SetOption_EchBuiltIn_SetsTheModeAndNullTurnsItOff()
    {
        WithFlags(true, () =>
        {
            foreach (var mode in new[] { "false", "grease", "true", "hard" })
            {
                Assert.AreEqual(mode, SetOk(CurlOption.Ech, mode).EncryptedClientHello);
            }

            var options = SetOk(new EasyOptions(), CurlOption.Ech, "hard");
            Assert.IsNull(SetOk(options, CurlOption.Ech, null).Tls.EncryptedClientHello);
        });
    }

    [TestMethod]
    public void SetOption_EchBuiltIn_KeepsTheConfigListAndPublicNameBesideTheMode()
    {
        WithFlags(true, () =>
        {
            var options = SetOk(new EasyOptions(), CurlOption.Ech, "true");
            options = SetOk(options, CurlOption.Ech, "ecl:AEX+");
            options = SetOk(options, CurlOption.Ech, "pn:example.com");

            Assert.AreEqual("true", options.Tls.EncryptedClientHello);
            Assert.AreEqual("AEX+", options.Tls.EncryptedClientHelloConfigList);
            Assert.AreEqual("example.com", options.Tls.EncryptedClientHelloPublicName);
        });
    }

    [TestMethod]
    public void SetOption_EchBuiltIn_ChangesNothingForAnUnknownWordOrAnEmptyPrefix()
    {
        WithFlags(true, () =>
        {
            var unset = new EasyOptions().Tls;

            foreach (var text in new[] { "maybe", "ecl:", "ecl:x", "pn:", "pn:x" })
            {
                Assert.AreEqual(unset, SetOk(CurlOption.Ech, text), text);
            }
        });
    }

    [TestMethod]
    public void SetOption_DnsOptionsWithAres_SetTheirFieldAndNullClearsIt()
    {
        WithFlags(true, () =>
        {
            var options = new EasyOptions();
            options = SetOk(options, CurlOption.DnsServers, "192.0.2.1");
            options = SetOk(options, CurlOption.DnsInterface, "eth0");
            options = SetOk(options, CurlOption.DnsLocalIp4, "192.0.2.2");
            options = SetOk(options, CurlOption.DnsLocalIp6, "2001:db8::2");

            Assert.AreEqual(("192.0.2.1", "eth0", "192.0.2.2", "2001:db8::2"), (options.Network.DnsServers, options.Network.DnsInterface, options.Network.DnsLocalIPv4, options.Network.DnsLocalIPv6));

            foreach (var option in new[] { CurlOption.DnsServers, CurlOption.DnsInterface, CurlOption.DnsLocalIp4, CurlOption.DnsLocalIp6 })
            {
                options = SetOk(options, option, null);
            }

            Assert.AreEqual(new EasyOptions().Network, options.Network);
        });
    }

    [TestMethod]
    public void SetOption_TcpFastopenBuiltIn_IsOnForNonZeroAndOffForZero()
    {
        WithFlags(true, () =>
        {
            Assert.IsTrue(SetOk(new EasyOptions(), CurlOption.TcpFastopen, 2L).Network.TcpFastOpen);
            Assert.IsFalse(SetOk(new EasyOptions(), CurlOption.TcpFastopen, 0L).Network.TcpFastOpen);
        });
    }

    private static CurlCode Set(EasyOptions options, CurlOption option, object? value) =>
        EasyOptionDispatch.Default.SetOption(ref options, option, value);

    private static void AnswersAndChangesNothing(CurlCode expected, CurlOption option, object? value)
    {
        var options = new EasyOptions();
        var before = options;

        Assert.AreEqual(expected, EasyOptionDispatch.Default.SetOption(ref options, option, value), option.ToString());
        Assert.AreSame(before, options, option.ToString());
    }

    private static void WithFlags(bool builtIn, Action check)
    {
        bool[] was =
        [
            EasyTlsOptionSetters.RevocationListFileIsBuiltIn, EasyTlsOptionSetters.IssuerCertificateIsBuiltIn,
            EasyTlsOptionSetters.EllipticCurvesIsBuiltIn, EasyTlsOptionSetters.SignatureAlgorithmsIsBuiltIn,
            EasyTlsOptionSetters.CertificateStatusIsBuiltIn, EasyTlsOptionSetters.EncryptedClientHelloIsBuiltIn,
            EasyTlsOptionSetters.OpenSslEngineIsBuiltIn, EasyNetworkOptionSetters.AresIsBuiltIn,
            EasyNetworkOptionSetters.TcpFastOpenIsBuiltIn,
        ];
        SetFlags([builtIn, builtIn, builtIn, builtIn, builtIn, builtIn, builtIn, builtIn, builtIn]);
        try
        {
            check();
        }
        finally
        {
            SetFlags(was);
        }
    }

    private static void SetFlags(bool[] flags)
    {
        EasyTlsOptionSetters.RevocationListFileIsBuiltIn = flags[0];
        EasyTlsOptionSetters.IssuerCertificateIsBuiltIn = flags[1];
        EasyTlsOptionSetters.EllipticCurvesIsBuiltIn = flags[2];
        EasyTlsOptionSetters.SignatureAlgorithmsIsBuiltIn = flags[3];
        EasyTlsOptionSetters.CertificateStatusIsBuiltIn = flags[4];
        EasyTlsOptionSetters.EncryptedClientHelloIsBuiltIn = flags[5];
        EasyTlsOptionSetters.OpenSslEngineIsBuiltIn = flags[6];
        EasyNetworkOptionSetters.AresIsBuiltIn = flags[7];
        EasyNetworkOptionSetters.TcpFastOpenIsBuiltIn = flags[8];
    }
}
