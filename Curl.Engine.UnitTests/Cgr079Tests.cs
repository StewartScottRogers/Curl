using System.Net;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins CGR-079 (BL-3608, ADR-0825): the proxy URL's own user information wins over <c>-U</c>, as
/// curl's <c>parse_proxy</c> does (measured on curl 8.22.0, Windows: <c>-x http://a:b@h -U c:d</c>
/// sends <c>Basic YTpi</c>, <c>-x http://a@h</c> sends <c>Basic YTo=</c>, <c>-x http://:b@h</c>
/// sends <c>Basic OmI=</c>, and <c>-x http://h</c> sends <c>-U</c>'s <c>Basic Yzpk</c>).
/// </summary>
[TestClass]
public sealed class Cgr079Tests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("http://example.test/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("http://a:b@127.0.0.1:20600", "a:b")]
    [DataRow("http://a@127.0.0.1:20600", "a:")]
    [DataRow("http://:b@127.0.0.1:20600", ":b")]
    [DataRow("socks5://a:b@127.0.0.1:20600", "a:b")]
    [DataRow("socks4://a@127.0.0.1:20600", "a:")]
    [DataRow("http://127.0.0.1:20600", "c:d")]
    public void TrySelect_ProxyUrlWithAndWithoutUserInformation_KeepsTheUrlsOverProxyUser(string proxyText, string expected)
    {
        EasyProxyOptions options = new() { Proxy = proxyText };
        Diagnostics.Arrange("proxy / -U", $"{proxyText} / c:d");

        TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, new NetworkCredential("c", "d"), Url, out ProxyEndpoint? proxy, out _);
        string actual = $"{proxy?.Credential?.UserName}:{proxy?.Credential?.Password}";
        Diagnostics.Act("credential", actual);

        Diagnostics.Assert("credential", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void TrySelect_EnvironmentProxyWithUserInformation_KeepsItOverProxyUser()
    {
        ProxySelector selector = new(name => name == "http_proxy" ? "http://a:b@127.0.0.1:20600" : null);

        TransferProxySelection.TrySelect(selector, new EasyProxyOptions(), false, new NetworkCredential("c", "d"), Url, out ProxyEndpoint? proxy, out _);
        string actual = $"{proxy?.Credential?.UserName}:{proxy?.Credential?.Password}";
        Diagnostics.Act("credential", actual);

        Assert.AreEqual("a:b", actual);
    }
}
