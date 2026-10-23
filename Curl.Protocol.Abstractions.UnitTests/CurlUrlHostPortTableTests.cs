using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins the <see cref="CurlUrl.Host" /> and <see cref="CurlUrl.Port" /> that <c>%{url.host}</c> and
/// <c>%{url.port}</c> print for the inputs measured with curl 8.22.0 (mingw, Schannel) on 2026-10-22
/// (BL-3173 Notes), and the parse failure of the ones it rejects (where both print empty). An IPv6
/// zone id is dropped from the host.
/// </summary>
[TestClass]
public sealed class CurlUrlHostPortTableTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http://127.1:9/echo", "127.0.0.1", 9)]
    [DataRow("http://0x7f.1:9/", "127.0.0.1", 9)]
    [DataRow("http://2130706433:9/", "127.0.0.1", 9)]
    [DataRow("http://127.0.0.1.:9/", "127.0.0.1", 9)]
    [DataRow("http://127.0.0.1:09/", "127.0.0.1", 9)]
    [DataRow("http://h.invalid:/echo", "h.invalid", 80)]
    [DataRow("http://[::1%25lo]:9/", "[::1]", 9)]
    [DataRow("http://HOST.Invalid:9/", "HOST.Invalid", 9)]
    [DataRow("http://0177.0.0.1:9/", "127.0.0.1", 9)]
    [DataRow("http://127.0.0.1:65535/", "127.0.0.1", 65535)]
    [DataRow("http://[0:0:0:0:0:0:0:1]:9/", "[::1]", 9)]
    [DataRow("http://user:pw@127.0.0.1:9/", "127.0.0.1", 9)]
    [DataRow("http://127.0.0.1:9/echo", "127.0.0.1", 9)]
    [DataRow("http://127.0.0.1:9", "127.0.0.1", 9)]
    [DataRow("http://127.0.0.1:0009/", "127.0.0.1", 9)]
    [DataRow("http://[::ffff:127.0.0.1]:9/", "[::ffff:127.0.0.1]", 9)]
    public void TryParse_MeasuredUrl_YieldsTheHostAndPortCurlPrints(string url, string expectedHost, int expectedPort)
    {
        bool parsed = CurlUrl.TryParse(url, pathAsIs: false, out CurlUrl? result);

        Diagnostics.Assert("parsed", true, parsed);
        Diagnostics.Assert("host", expectedHost, result?.Host);
        Diagnostics.Assert("port", expectedPort, result?.Port);
        Assert.IsTrue(parsed);
        Assert.AreEqual(expectedHost, result!.Host);
        Assert.AreEqual(expectedPort, result.Port);
    }

    [TestMethod]
    [DataRow("//127.0.0.1:18080/echo")]
    [DataRow("http://http:127.0.0.1:18080/echo")]
    [DataRow("http:// http://127.0.0.1:9/echo")]
    [DataRow("http://127.0.0.1:9/echo ")]
    public void TryParse_UrlCurlRejects_Fails(string url)
    {
        bool parsed = CurlUrl.TryParse(url, pathAsIs: false, out CurlUrl? result);

        Diagnostics.Assert("parsed", false, parsed);
        Assert.IsFalse(parsed);
        Assert.IsNull(result);
    }
}
