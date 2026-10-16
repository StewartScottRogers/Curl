using Curl.Api.Options.Authentication;
using Curl.Api.Options.Proxy;
using Curl.Authentication;
using Curl.Core;
using Curl.Networking;
using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Engine;

/// <summary>
/// Pins that the engine's proxy members read <see cref="EasyProxyOptions" /> as ADR-0515 maps it
/// (BL-2325): the proxy type is the kind of proxy text with no scheme, <see cref="ProxyType.Https2" />
/// stays HTTP and asks the tunnel for HTTP/2, libcurl's default SOCKS5 methods allow both, and the
/// pre-proxy and HAProxy line come from the record alone.
/// </summary>
[TestClass]
public sealed class EasyProxyOptionsReadingTests
{
    private static readonly CurlUrl Url = CurlUrl.Parse("http://h/");

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(ProxyType.Http, ProxyKind.Http)]
    [DataRow(ProxyType.Http10, ProxyKind.Http10)]
    [DataRow(ProxyType.Https, ProxyKind.Https)]
    [DataRow(ProxyType.Https2, ProxyKind.Http)]
    [DataRow(ProxyType.Socks4, ProxyKind.Socks4)]
    [DataRow(ProxyType.Socks5, ProxyKind.Socks5)]
    [DataRow(ProxyType.Socks4A, ProxyKind.Socks4a)]
    [DataRow(ProxyType.Socks5Hostname, ProxyKind.Socks5Hostname)]
    public void TrySelect_ProxyWithNoScheme_IsTheKindItsProxyTypeNames(ProxyType type, ProxyKind kind)
    {
        EasyProxyOptions options = new() { Proxy = "proxy.example:3128", ProxyType = type };
        Diagnostics.Arrange("proxy type", type);

        bool selected = TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, null, Url, out ProxyEndpoint? proxy, out TransferResult? failure);
        Diagnostics.Act("selected / kind", $"{selected} / {proxy?.Kind}");

        Diagnostics.Assert("selected / kind", $"True / {kind}", $"{selected} / {proxy?.Kind}");
        Assert.IsTrue(selected);
        Assert.IsNull(failure);
        Assert.AreEqual(kind, proxy!.Kind);
    }

    [TestMethod]
    public void TrySelect_Http10ProxyTypeWithAnHttpScheme_KeepsHttp10()
    {
        EasyProxyOptions options = new() { Proxy = "http://proxy.example:3128", ProxyType = ProxyType.Http10 };

        TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, null, Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("kind", proxy?.Kind);

        Diagnostics.Assert("kind", ProxyKind.Http10, proxy?.Kind);
        Assert.AreEqual(ProxyKind.Http10, proxy!.Kind);
    }

    [TestMethod]
    public void TrySelect_ProxyUser_ReplacesTheProxysCredential()
    {
        EasyProxyOptions options = new() { Proxy = "http://a:b@proxy.example:3128" };
        System.Net.NetworkCredential user = new("u", "p");

        TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, user, Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("credential user", proxy?.Credential?.UserName);

        Diagnostics.Assert("credential user", "u", proxy?.Credential?.UserName);
        Assert.AreSame(user, proxy!.Credential);
    }

    [TestMethod]
    public void TrySelect_ThroughAUnixSocket_SelectsNoProxy()
    {
        EasyProxyOptions options = new() { Proxy = "http://proxy.example:3128" };

        bool selected = TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, true, null, Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("selected / proxy", $"{selected} / {proxy}");

        Diagnostics.Assert("selected / proxy is null", "True / True", $"{selected} / {proxy is null}");
        Assert.IsTrue(selected);
        Assert.IsNull(proxy);
    }

    [TestMethod]
    public void TrySelect_SocksPreProxyAndNoProxy_IsThePreProxy()
    {
        EasyProxyOptions options = new() { PreProxy = "socks5://127.0.0.1:1080" };

        bool selected = TransferProxySelection.TrySelect(new ProxySelector(_ => null), options, false, null, Url, out ProxyEndpoint? proxy, out _);
        Diagnostics.Act("selected / kind", $"{selected} / {proxy?.Kind}");

        Diagnostics.Assert("selected / kind", "True / Socks5", $"{selected} / {proxy?.Kind}");
        Assert.AreEqual(ProxyKind.Socks5, proxy!.Kind);
    }

    [TestMethod]
    public void PreProxyOf_RecordWithASocksPreProxy_IsThatProxy()
    {
        ProxyEndpoint? preProxy = EngineComposition.PreProxyOf(new EasyProxyOptions { PreProxy = "127.0.0.1:1080" });
        Diagnostics.Act("pre-proxy kind", preProxy?.Kind);

        Diagnostics.Assert("pre-proxy kind", ProxyKind.Socks4, preProxy?.Kind);
        Assert.AreEqual(ProxyKind.Socks4, preProxy!.Kind);
    }

    [TestMethod]
    public void HaproxyProtocolOf_RecordWithOnlyAClientIp_SendsTheLineWithIt()
    {
        HaproxyProtocolHeader? header = EngineComposition.HaproxyProtocolOf(new EasyProxyOptions { HaProxyClientIp = "1.2.3.4" });
        Diagnostics.Act("client IP", header?.ClientIp);

        Diagnostics.Assert("client IP", "1.2.3.4", header?.ClientIp);
        Assert.AreEqual("1.2.3.4", header!.ClientIp);
    }

    [TestMethod]
    public void HaproxyProtocolOf_DefaultRecord_IsNull()
    {
        HaproxyProtocolHeader? header = EngineComposition.HaproxyProtocolOf(new EasyProxyOptions());

        Diagnostics.Assert("header is null", true, header is null);
        Assert.IsNull(header);
    }

    [TestMethod]
    public void Socks5AuthenticationMappingOf_DefaultRecord_AllowsBothMethodsWithTheDefaultService()
    {
        Socks5AuthenticationOptions options = Socks5AuthenticationMapping.Of(new EasyProxyOptions(), new NegotiateOptions(null, null, SecurityDelegation.Always), null, usesSspi: false);
        Diagnostics.Act("basic / gssapi / service / delegation", $"{options.AllowUserNameAndPassword} / {options.AllowGssapi} / {options.GssapiServiceName} / {options.GssapiDelegation}");

        Diagnostics.Assert("basic / gssapi / service / delegation", "True / True / rcmd / Always", $"{options.AllowUserNameAndPassword} / {options.AllowGssapi} / {options.GssapiServiceName} / {options.GssapiDelegation}");
        Assert.AreEqual((true, true), (options.AllowUserNameAndPassword, options.AllowGssapi));
        Assert.AreEqual(Socks5AuthenticationOptions.DefaultGssapiServiceName, options.GssapiServiceName);
        Assert.AreEqual(SecurityDelegation.Always, options.GssapiDelegation);
    }

    [TestMethod]
    public void Socks5AuthenticationMappingOf_GssApiOnlyWithBothServiceNames_PrefersTheSocks5Service()
    {
        EasyProxyOptions proxy = new() { Socks5Authentication = Socks5Authentication.GssApi, Socks5GssApiService = "socks", Socks5GssApiNec = true };

        Socks5AuthenticationOptions options = Socks5AuthenticationMapping.Of(proxy, new NegotiateOptions(null, "proxy", SecurityDelegation.Always), null, usesSspi: true);
        Diagnostics.Act("basic / gssapi / service / nec / delegation", $"{options.AllowUserNameAndPassword} / {options.AllowGssapi} / {options.GssapiServiceName} / {options.GssapiNec} / {options.GssapiDelegation}");

        Diagnostics.Assert("basic / gssapi / service / nec / delegation", "False / True / socks / True / None", $"{options.AllowUserNameAndPassword} / {options.AllowGssapi} / {options.GssapiServiceName} / {options.GssapiNec} / {options.GssapiDelegation}");
        Assert.AreEqual((false, true), (options.AllowUserNameAndPassword, options.AllowGssapi));
        Assert.AreEqual("socks", options.GssapiServiceName);
    }

    [TestMethod]
    public void Socks5AuthenticationMappingOf_NoSocks5Service_UsesTheProxyServiceName()
    {
        Socks5AuthenticationOptions options = Socks5AuthenticationMapping.Of(new EasyProxyOptions(), new NegotiateOptions(null, "proxy", SecurityDelegation.None), null, usesSspi: false);

        Diagnostics.Assert("service", "proxy", options.GssapiServiceName);
        Assert.AreEqual("proxy", options.GssapiServiceName);
    }

    [TestMethod]
    [DataRow(ProxyType.Https2, true)]
    [DataRow(ProxyType.Http, false)]
    public void CreateProxyTunnelOptions_ProxyType_AsksForAnHttp2TunnelOnlyForHttps2(ProxyType type, bool http2)
    {
        EasyProxyOptions proxy = new() { ProxyType = type, ProxyHeaders = ["X-Proxy: 1"] };

        HttpProxyTunnelOptions tunnel = EngineComposition.CreateProxyTunnelOptions(new Curl.Api.Options.Http.EasyHttpOptions(), proxy, new EasyAuthenticationOptions(), new LateBoundSecurityContextFactory());
        Diagnostics.Act("HTTP/2 / headers", $"{tunnel.ProxyHttp2} / {string.Join('|', tunnel.ProxyHeaders)}");

        Diagnostics.Assert("HTTP/2 / headers", $"{http2} / X-Proxy: 1", $"{tunnel.ProxyHttp2} / {string.Join('|', tunnel.ProxyHeaders)}");
        Assert.AreEqual(http2, tunnel.ProxyHttp2);
        CollectionAssert.AreEqual(new[] { "X-Proxy: 1" }, tunnel.ProxyHeaders.ToArray());
    }
}
