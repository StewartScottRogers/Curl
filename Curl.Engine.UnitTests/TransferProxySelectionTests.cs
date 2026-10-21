using System.Net;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that <see cref="TransferProxySelection" /> chooses a SOCKS proxy at <c>localhost</c> with a
/// path as one reached over that Unix domain socket, as curl's <c>parse_proxy</c> reads
/// <c>--proxy socks5h://localhost/path</c> and <c>--socks5 localhost/path</c> (upstream tests 1467,
/// 1468 and 1470; BL-2955).
/// </summary>
[TestClass]
public sealed class TransferProxySelectionTests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("https://this.is.a.host.name:8443/1470");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("socks5h://localhost/some/path", ProxyType.Http, ProxyKind.Socks5Hostname)]
    [DataRow("localhost/some/path", ProxyType.Socks5, ProxyKind.Socks5)]
    [DataRow("localhost/some/path", ProxyType.Socks5Hostname, ProxyKind.Socks5Hostname)]
    public void TrySelect_SocksAtLocalhostWithAPath_IsReachedOverThatUnixSocket(string proxyText, ProxyType type, ProxyKind kind)
    {
        EasyProxyOptions options = new() { Proxy = proxyText, ProxyType = type };
        Diagnostics.Arrange("proxy / type", $"{proxyText} / {type}");

        bool selected = TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, null, Url, out ProxyEndpoint? proxy, out TransferResult? failure);
        Diagnostics.Act("kind / unix socket", $"{proxy?.Kind} / {proxy?.UnixSocketPath}");

        Diagnostics.Assert("kind / unix socket", $"{kind} / /some/path", $"{proxy?.Kind} / {proxy?.UnixSocketPath}");
        Assert.IsTrue(selected);
        Assert.IsNull(failure);
        Assert.AreEqual(kind, proxy!.Kind);
        Assert.AreEqual("/some/path", proxy.UnixSocketPath);
    }

    [TestMethod]
    public void TrySelect_SocksOverAUnixSocketWithAProxyUser_KeepsTheSocket()
    {
        EasyProxyOptions options = new() { Proxy = "socks5h://localhost/some/path" };

        TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, new NetworkCredential("u", "p"), Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("unix socket / user", $"{proxy?.UnixSocketPath} / {proxy?.Credential?.UserName}");

        Assert.AreEqual("/some/path", proxy!.UnixSocketPath);
        Assert.AreEqual("u", proxy.Credential!.UserName);
    }

    [TestMethod]
    [DataRow("socks5h://proxy.example:1080/some/path")]
    [DataRow("socks5h://localhost:1080")]
    public void TrySelect_SocksNotAtLocalhostOrWithNoPath_DialsHostAndPort(string proxyText)
    {
        EasyProxyOptions options = new() { Proxy = proxyText };

        TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, null, Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("host / port / unix socket", $"{proxy?.Host} / {proxy?.Port} / {proxy?.UnixSocketPath}");

        Assert.IsNull(proxy!.UnixSocketPath);
        Assert.AreEqual(1080, proxy.Port);
    }
}
