using System.Net.Sockets;
using System.Text;
using Curl.Api.Options.Tls;
using Curl.Networking;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

/// <summary>
/// CGR-037 (ADR-0904): curl 8.22.0's DoH sub-transfers go through the proxy the environment
/// names - an <c>http://</c> DoH URL POSTed to an HTTP proxy in absolute form, an <c>https://</c>
/// one tunnelled with a CONNECT carrying no <c>User-Agent</c> - with the bytes measured from real
/// curl against a proxy answering 403.
/// </summary>
[TestClass]
public sealed class Cgr037Tests
{
    private static readonly byte[] Forbidden = Encoding.Latin1.GetBytes("HTTP/1.1 403 Forbidden\r\nContent-Length: 0\r\n\r\n");

    [TestMethod]
    public async Task CreateDohResolver_UnderHttpProxy_PostsTheQueryToTheProxyInAbsoluteForm()
    {
        ScriptedConnector connector = new([Forbidden]);
        IDnsResolver resolver = EngineComposition.CreateDohResolver(
            "http://127.0.0.1:21800/dns-query", connector, AddressFamily.InterNetwork, readEnvironmentVariable: Environment(("http_proxy", "http://127.0.0.1:21803"), ("no_proxy", "foo.test")));

        var addresses = await resolver.ResolveAsync("foo.test", CancellationToken.None);

        Assert.IsEmpty(addresses);
        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual("127.0.0.1:21803", $"{target.Host}:{target.Port}");
        Assert.IsTrue(target.IsForwardProxy);
        Assert.IsFalse(target.UseTls);
        Assert.AreEqual(
            "POST http://127.0.0.1:21800/dns-query HTTP/1.1\r\n"
            + "Host: 127.0.0.1:21800\r\n"
            + "Accept: */*\r\n"
            + "Proxy-Connection: Keep-Alive\r\n"
            + "Content-Type: application/dns-message\r\n"
            + "Content-Length: 26\r\n"
            + "\r\n"
            + "\0\0\u0001\0\0\u0001\0\0\0\0\0\0\u0003foo\u0004test\0\0\u0001\0\u0001",
            Encoding.Latin1.GetString(connector.Written));
    }

    [TestMethod]
    public async Task CreateDohResolver_HttpsDohUrlUnderHttpsProxy_TunnelsToTheDohServer()
    {
        ScriptedConnector connector = new([Forbidden]);
        IDnsResolver resolver = EngineComposition.CreateDohResolver(
            "https://127.0.0.1:21801/dns-query", connector, AddressFamily.InterNetwork, readEnvironmentVariable: Environment(("https_proxy", "http://127.0.0.1:21803")));

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual("127.0.0.1:21801", $"{target.Host}:{target.Port}");
        Assert.IsTrue(target.UseTls);
        Assert.IsFalse(target.IsForwardProxy);
        Assert.AreEqual(new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 21803, null), target.Proxy);
        StringAssert.StartsWith(Encoding.Latin1.GetString(connector.Written), "POST /dns-query HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task CreateDohResolver_HttpDohUrlUnderSocksProxy_TunnelsToTheDohServer()
    {
        ScriptedConnector connector = new([Forbidden]);
        IDnsResolver resolver = EngineComposition.CreateDohResolver(
            "http://127.0.0.1:21800/dns-query", connector, AddressFamily.InterNetwork, readEnvironmentVariable: Environment(("ALL_PROXY", "socks5://127.0.0.1:21803")));

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual("127.0.0.1:21800", $"{target.Host}:{target.Port}");
        Assert.AreEqual(ProxyKind.Socks5, target.Proxy?.Kind);
        StringAssert.StartsWith(Encoding.Latin1.GetString(connector.Written), "POST /dns-query HTTP/1.1\r\n");
    }

    [TestMethod]
    public async Task CreateDohResolver_HttpDohUrlUnderAnHttpsProxy_ForwardsOverTls()
    {
        ScriptedConnector connector = new([Forbidden]);
        IDnsResolver resolver = EngineComposition.CreateDohResolver(
            "http://127.0.0.1/dns-query", connector, AddressFamily.InterNetwork, readEnvironmentVariable: Environment(("http_proxy", "https://127.0.0.1:21803")));

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        ConnectTarget target = connector.Targets.Single();
        Assert.IsTrue(target.IsForwardProxy);
        Assert.IsTrue(target.UseTls);
        StringAssert.StartsWith(Encoding.Latin1.GetString(connector.Written), "POST http://127.0.0.1/dns-query HTTP/1.1\r\nHost: 127.0.0.1\r\n");
    }

    [TestMethod]
    public async Task CreateDohResolver_NoProxyEnvironment_ConnectsToTheDohServerDirectly()
    {
        ScriptedConnector connector = new([Forbidden]);
        IDnsResolver resolver = EngineComposition.CreateDohResolver(
            "http://127.0.0.1:21800/dns-query", connector, AddressFamily.InterNetwork, readEnvironmentVariable: Environment());

        await resolver.ResolveAsync("foo.test", CancellationToken.None);

        ConnectTarget target = connector.Targets.Single();
        Assert.AreEqual("127.0.0.1:21800", $"{target.Host}:{target.Port}");
        Assert.IsNull(target.Proxy);
        Assert.IsFalse(target.IsForwardProxy);
    }

    [TestMethod]
    public void CreateDohResolver_UnusableProxyText_ResolvesNothing()
    {
        IDnsResolver resolver = EngineComposition.CreateDohResolver(
            "http://127.0.0.1:21800/dns-query", new ScriptedConnector([]), AddressFamily.Unspecified, readEnvironmentVariable: Environment(("http_proxy", "http://[bad")));

        Assert.IsInstanceOfType<UnusableDohUrlResolver>(resolver);
    }

    [TestMethod]
    public async Task CreateDohConnector_ThroughAProxy_SendsTheConnectWithNoUserAgent()
    {
        ScriptedConnector proxy = new([Forbidden]);
        TcpConnector connector = EngineComposition.CreateDohConnector(
            EngineTraceComponents.None, new EasyTlsOptions(), new ScriptedTcpDialer(proxy), TimeProvider.System);

        ConnectResult result = await connector.ConnectAsync(
            new ConnectTarget("127.0.0.1", 21801, true) { Proxy = new ProxyEndpoint(ProxyKind.Http, "127.0.0.1", 21803, null) },
            CancellationToken.None);

        Assert.IsNull(result.Connection);
        Assert.AreEqual(
            "CONNECT 127.0.0.1:21801 HTTP/1.1\r\nHost: 127.0.0.1:21801\r\nProxy-Connection: Keep-Alive\r\n\r\n",
            Encoding.Latin1.GetString(proxy.Written));
    }

    private static Func<string, string?> Environment(params (string Name, string Value)[] variables) =>
        name => variables.FirstOrDefault(variable => string.Equals(variable.Name, name, StringComparison.Ordinal)).Value;
}
