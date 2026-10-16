using System.Net;
using Curl.Api.Messages;
using Curl.Api.Options;

namespace Curl.Api.HttpMessageHandler.Proxy;

[TestClass]
public sealed class HandlerProxyMappingTests
{
    private static async Task<EasyOptions> SendAsync(string url, Action<CurlHttpMessageHandler> configure, CurlClientOptions? options = null)
    {
        ScriptedEasyTransfer easy = new();
        await using CurlClient client = new(new ScriptedCurlEngine(easy), options ?? new CurlClientOptions(), ownsEngine: true);
        CurlHttpMessageHandler handler = new(client);
        configure(handler);
        using HttpClient http = new(handler);
        using HttpResponseMessage response = await http.GetAsync(url);
        return easy.PerformedOptions!;
    }

    [TestMethod]
    public async Task Handler_Defaults_UseProxyWithNoProxyOrCredentials()
    {
        await using CurlClient client = new(new ScriptedCurlEngine(new ScriptedEasyTransfer()));
        using CurlHttpMessageHandler handler = new(client);

        Assert.IsTrue(handler.UseProxy);
        Assert.IsNull(handler.Proxy);
        Assert.IsNull(handler.DefaultProxyCredentials);
        Assert.IsNull(handler.Credentials);
    }

    [TestMethod]
    public async Task Send_NoHandlerSettings_KeepsTheClientsProxyAndAuthentication()
    {
        CurlClientOptions options = new()
        {
            Proxy = new CurlProxyOptions { Proxy = "http://client-proxy:3128" },
            Authentication = new CurlAuthenticationOptions { UserName = "client" },
        };

        EasyOptions sent = await SendAsync("http://example/", _ => { }, options);

        Assert.AreEqual("http://client-proxy:3128", sent.Proxy.Proxy);
        Assert.AreEqual("client", sent.Authentication.UserName);
        Assert.IsNull(sent.Authentication.ProxyUserName);
    }

    [TestMethod]
    public async Task Send_WebProxyWithCredentials_SetsTheProxyAndItsUserAndPassword()
    {
        WebProxy proxy = new("http://proxy.test:8080/path") { Credentials = new NetworkCredential("pu", "pp") };
        CurlClientOptions options = new() { Proxy = new CurlProxyOptions { HttpProxyTunnel = true } };

        EasyOptions sent = await SendAsync("http://example/", handler => handler.Proxy = proxy, options);

        Assert.AreEqual("http://proxy.test:8080", sent.Proxy.Proxy);
        Assert.IsTrue(sent.Proxy.HttpProxyTunnel);
        Assert.AreEqual("pu", sent.Authentication.ProxyUserName);
        Assert.AreEqual("pp", sent.Authentication.ProxyPassword);
        Assert.IsNull(sent.Authentication.UserName);
    }

    [TestMethod]
    public async Task Send_ProxyWithoutCredentials_UsesTheDefaultProxyCredentialsWithTheirDomain()
    {
        EasyOptions sent = await SendAsync("http://example/", handler =>
        {
            handler.Proxy = new WebProxy("http://proxy.test:8080");
            handler.DefaultProxyCredentials = new NetworkCredential("pu", "pp", "CORP");
        });

        Assert.AreEqual(@"CORP\pu", sent.Authentication.ProxyUserName);
        Assert.AreEqual("pp", sent.Authentication.ProxyPassword);
    }

    [TestMethod]
    public async Task Send_ProxyWithNoCredentialsAnywhere_SetsOnlyTheProxy()
    {
        CurlClientOptions options = new() { Authentication = new CurlAuthenticationOptions { ProxyUserName = "client" } };

        EasyOptions sent = await SendAsync("http://example/", handler => handler.Proxy = new WebProxy("http://proxy.test:8080"), options);

        Assert.AreEqual("http://proxy.test:8080", sent.Proxy.Proxy);
        Assert.AreEqual("client", sent.Authentication.ProxyUserName);
    }

    [TestMethod]
    public async Task Send_HostOnTheBypassList_IsReachedDirectlyWithoutProxyCredentials()
    {
        WebProxy proxy = new("http://proxy.test:8080", BypassOnLocal: false, BypassList: [@"bypassed\.example"]) { Credentials = new NetworkCredential("pu", "pp") };

        EasyOptions bypassed = await SendAsync("http://bypassed.example/", handler => handler.Proxy = proxy);
        EasyOptions proxied = await SendAsync("http://other.example/", handler => handler.Proxy = proxy);

        Assert.AreEqual(HandlerProxyMapping.NoProxy, bypassed.Proxy.Proxy);
        Assert.IsNull(bypassed.Authentication.ProxyUserName);
        Assert.AreEqual("http://proxy.test:8080", proxied.Proxy.Proxy);
        Assert.AreEqual("pu", proxied.Authentication.ProxyUserName);
    }

    [TestMethod]
    public async Task Send_ProxyGivingNoProxyForTheHost_IsReachedDirectly()
    {
        EasyOptions sent = await SendAsync("http://example/", handler => handler.Proxy = new NullProxy());

        Assert.AreEqual(HandlerProxyMapping.NoProxy, sent.Proxy.Proxy);
    }

    [TestMethod]
    public async Task Send_UseProxyFalse_ReachesTheHostDirectlyPastTheClientsProxy()
    {
        CurlClientOptions options = new() { Proxy = new CurlProxyOptions { Proxy = "http://client-proxy:3128" } };

        EasyOptions sent = await SendAsync("http://example/", handler =>
        {
            handler.Proxy = new WebProxy("http://proxy.test:8080") { Credentials = new NetworkCredential("pu", "pp") };
            handler.UseProxy = false;
        }, options);

        Assert.AreEqual(HandlerProxyMapping.NoProxy, sent.Proxy.Proxy);
        Assert.IsNull(sent.Authentication.ProxyUserName);
    }

    [TestMethod]
    public async Task Send_ServerAndProxyCredentials_KeepTheServerCredentialBesideTheProxys()
    {
        CurlClientOptions options = new() { Authentication = new CurlAuthenticationOptions { ProxyUserName = "client" } };

        EasyOptions sent = await SendAsync("http://example/", handler =>
        {
            handler.Credentials = new NetworkCredential("su", "sp");
            handler.Proxy = new WebProxy("http://proxy.test:8080") { Credentials = new NetworkCredential("pu", "pp") };
        }, options);

        Assert.AreEqual("su", sent.Authentication.UserName);
        Assert.AreEqual("sp", sent.Authentication.Password);
        Assert.AreEqual("pu", sent.Authentication.ProxyUserName);
        Assert.AreEqual("pp", sent.Authentication.ProxyPassword);
    }

    [TestMethod]
    public async Task Send_CredentialsGivingNoneForTheHost_KeepTheClientsAuthentication()
    {
        CurlClientOptions options = new() { Authentication = new CurlAuthenticationOptions { UserName = "client" } };

        EasyOptions sent = await SendAsync("http://example/", handler => handler.Credentials = new CredentialCache(), options);

        Assert.AreEqual("client", sent.Authentication.UserName);
    }

    /// <summary>A proxy that bypasses nothing yet gives no proxy for any host.</summary>
    private sealed class NullProxy : IWebProxy
    {
        public ICredentials? Credentials { get; set; }

        public Uri? GetProxy(Uri destination) => null;

        public bool IsBypassed(Uri host) => false;
    }
}
