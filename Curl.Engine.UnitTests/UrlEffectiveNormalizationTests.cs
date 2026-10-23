using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins <see cref="UrlEffective.Normalize(string, string, bool)" /> on the 20 inputs measured with curl
/// 8.22.0 (mingw, Schannel) on 2026-10-22 as <c>curl -sS -w '%{url_effective}' &lt;url&gt;</c> (BL-3173
/// Notes). The first argument is the URL after the runner guessed its scheme, the second as typed.
/// </summary>
[TestClass]
public sealed class UrlEffectiveNormalizationTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http://127.1:9/echo", "http://127.1:9/echo", "http://127.0.0.1:9/echo")]
    [DataRow("http://0x7f.1:9/", "http://0x7f.1:9/", "http://127.0.0.1:9/")]
    [DataRow("http://2130706433:9/", "http://2130706433:9/", "http://127.0.0.1:9/")]
    [DataRow("http://127.0.0.1.:9/", "http://127.0.0.1.:9/", "http://127.0.0.1:9/")]
    [DataRow("http://127.0.0.1:09/", "http://127.0.0.1:09/", "http://127.0.0.1:9/")]
    [DataRow("http://h.invalid:/echo", "http://h.invalid:/echo", "http://h.invalid/echo")]
    [DataRow("//127.0.0.1:18080/echo", "//127.0.0.1:18080/echo", "//127.0.0.1:18080/echo")]
    [DataRow("http://http:127.0.0.1:18080/echo", "http:127.0.0.1:18080/echo", "http:127.0.0.1:18080/echo")]
    [DataRow("http:// http://127.0.0.1:9/echo", " http://127.0.0.1:9/echo", " http://127.0.0.1:9/echo")]
    [DataRow("http://127.0.0.1:9/echo ", "http://127.0.0.1:9/echo ", "http://127.0.0.1:9/echo ")]
    [DataRow("http://[::1%25lo]:9/", "http://[::1%25lo]:9/", "http://[::1%25lo]:9/")]
    [DataRow("http://HOST.Invalid:9/", "http://HOST.Invalid:9/", "http://HOST.Invalid:9/")]
    [DataRow("http://0177.0.0.1:9/", "http://0177.0.0.1:9/", "http://127.0.0.1:9/")]
    [DataRow("http://127.0.0.1:65535/", "http://127.0.0.1:65535/", "http://127.0.0.1:65535/")]
    [DataRow("http://[0:0:0:0:0:0:0:1]:9/", "http://[0:0:0:0:0:0:0:1]:9/", "http://[::1]:9/")]
    [DataRow("http://user:pw@127.0.0.1:9/", "http://user:pw@127.0.0.1:9/", "http://user:pw@127.0.0.1:9/")]
    [DataRow("http://127.0.0.1:9/echo", "127.0.0.1:9/echo", "http://127.0.0.1:9/echo")]
    [DataRow("http://127.0.0.1:9", "http://127.0.0.1:9", "http://127.0.0.1:9/")]
    [DataRow("http://127.0.0.1:0009/", "http://127.0.0.1:0009/", "http://127.0.0.1:9/")]
    [DataRow("http://[::ffff:127.0.0.1]:9/", "http://[::ffff:127.0.0.1]:9/", "http://[::ffff:127.0.0.1]:9/")]
    public void Normalize_MeasuredUrl_PrintsTheEffectiveUrlCurlPrints(string url, string typedUrl, string expected)
    {
        string effectiveUrl = UrlEffective.Normalize(url, typedUrl, pathAsIs: false);

        Diagnostics.Assert("effective URL", expected, effectiveUrl);
        Assert.AreEqual(expected, effectiveUrl);
    }

    [TestMethod]
    public void Normalize_IPv6AddressWithoutAPort_NormalisesTheAddressAndAddsNoPort()
    {
        string effectiveUrl = UrlEffective.Normalize("http://[0:0:0:0:0:0:0:1]/", "http://[0:0:0:0:0:0:0:1]/", pathAsIs: false);

        Diagnostics.Assert("effective URL", "http://[::1]/", effectiveUrl);
        Assert.AreEqual("http://[::1]/", effectiveUrl);
    }
}
