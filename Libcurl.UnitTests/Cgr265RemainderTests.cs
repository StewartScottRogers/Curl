using Curl.Api.Codes;
using Curl.Api.Options;
using Libcurl.Options;
using static Libcurl.UnitTests.Options.Tls.TlsOptionAssert;

namespace Libcurl.UnitTests;

// Flips the process-wide EasyTlsOptionSetters.Tls13CiphersIsBuiltIn and ProxyCaPathIsBuiltIn, so it must not run beside another test that reads them (ADR-0991).
[TestClass]
[DoNotParallelize]
public sealed class Cgr265RemainderTests
{
    [TestMethod]
    public void SetOption_Tls13CiphersBuiltIn_SetsTheOriginAndProxyTls13CiphersTheProxy()
    {
        WithFlags(tls13Ciphers: true, proxyCaPath: false, () =>
            TwinText(CurlOption.Tls13Ciphers, CurlOption.ProxyTls13Ciphers, p => p.Tls13Ciphers));
    }

    [TestMethod]
    public void SetOption_ProxyCapathBuiltIn_SetsTheOriginCapathAndProxyCapathTheProxy()
    {
        WithFlags(tls13Ciphers: false, proxyCaPath: true, () =>
            TwinText(CurlOption.Capath, CurlOption.ProxyCapath, p => p.CertificateAuthorityPath));
    }

    [TestMethod]
    public void SetOption_CapathWithoutProxyCapath_StillSetsTheOrigin()
    {
        WithFlags(tls13Ciphers: false, proxyCaPath: false, () =>
            Assert.AreEqual("dir", SetOk(CurlOption.Capath, "dir").Origin.CertificateAuthorityPath));
    }

    [TestMethod]
    [DataRow(CurlOption.Tls13Ciphers, "TLS_AES_128_GCM_SHA256")]
    [DataRow(CurlOption.Tls13Ciphers, null)]
    [DataRow(CurlOption.ProxyTls13Ciphers, "TLS_AES_128_GCM_SHA256")]
    [DataRow(CurlOption.ProxyTls13Ciphers, null)]
    [DataRow(CurlOption.ProxyCapath, "dir")]
    [DataRow(CurlOption.ProxyCapath, null)]
    public void SetOption_OptionTheBuildLacks_IsNotBuiltInAndChangesNothing(CurlOption option, string? value)
    {
        WithFlags(tls13Ciphers: false, proxyCaPath: false, () =>
        {
            var options = new EasyOptions();
            var before = options;

            Assert.AreEqual(CurlCode.NotBuiltIn, EasyOptionDispatch.Default.SetOption(ref options, option, value));
            Assert.AreSame(before, options);
        });
    }

    private static void WithFlags(bool tls13Ciphers, bool proxyCaPath, Action check)
    {
        var tls13CiphersWas = EasyTlsOptionSetters.Tls13CiphersIsBuiltIn;
        var proxyCaPathWas = EasyTlsOptionSetters.ProxyCaPathIsBuiltIn;
        EasyTlsOptionSetters.Tls13CiphersIsBuiltIn = tls13Ciphers;
        EasyTlsOptionSetters.ProxyCaPathIsBuiltIn = proxyCaPath;
        try
        {
            check();
        }
        finally
        {
            EasyTlsOptionSetters.Tls13CiphersIsBuiltIn = tls13CiphersWas;
            EasyTlsOptionSetters.ProxyCaPathIsBuiltIn = proxyCaPathWas;
        }
    }
}
