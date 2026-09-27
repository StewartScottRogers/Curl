using Curl.Protocol.Abstractions;

namespace Curl.Cli;

/// <summary>
/// Pins how <see cref="CommandLineProxy.TryGetKind"/> reads a proxy's scheme, as measured with the
/// reference curl 8.21.0 on 2026-09-26 (<c>Record-CurlExchange.ps1</c>, reading the first bytes the
/// proxy received, and <c>curl -x &lt;value&gt; http://127.0.0.1:1/</c> for the failures).
/// </summary>
[TestClass]
public sealed class CommandLineProxyTests
{
    [TestMethod]
    [DataRow("http://p:1", ProxyKind.Socks5, ProxyKind.Http)]
    [DataRow("HTTP://p:1", ProxyKind.Http, ProxyKind.Http)]
    [DataRow("https://p:1", ProxyKind.Http, ProxyKind.Https)]
    [DataRow("socks://p:1", ProxyKind.Http, ProxyKind.Socks4)]
    [DataRow("socks4://p:1", ProxyKind.Http, ProxyKind.Socks4)]
    [DataRow("socks4a://p:1", ProxyKind.Http, ProxyKind.Socks4a)]
    [DataRow("socks5://p:1", ProxyKind.Socks4, ProxyKind.Socks5)]
    [DataRow("socks5h://p:1", ProxyKind.Http, ProxyKind.Socks5Hostname)]
    [DataRow("SOCKS5H://p:1", ProxyKind.Http, ProxyKind.Socks5Hostname)]
    public void TryGetKind_SupportedScheme_OutranksTheOptionsKind(string address, ProxyKind kindWithoutScheme, ProxyKind expected)
    {
        bool supported = new CommandLineProxy(address, kindWithoutScheme).TryGetKind(out ProxyKind kind, out TransferResult? failure);

        Assert.IsTrue(supported);
        Assert.AreEqual(expected, kind);
        Assert.IsNull(failure);
    }

    [TestMethod]
    [DataRow("p:1", ProxyKind.Http)]
    [DataRow("p", ProxyKind.Socks4)]
    [DataRow("user:pw@p:1", ProxyKind.Socks4a)]
    [DataRow("", ProxyKind.Socks5)]
    [DataRow("p:1", ProxyKind.Socks5Hostname)]
    public void TryGetKind_NoScheme_IsTheOptionsKind(string address, ProxyKind kindWithoutScheme)
    {
        bool supported = new CommandLineProxy(address, kindWithoutScheme).TryGetKind(out ProxyKind kind, out TransferResult? failure);

        Assert.IsTrue(supported);
        Assert.AreEqual(kindWithoutScheme, kind);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void With_BothMembersReplaced_ReadsTheNewScheme()
    {
        CommandLineProxy proxy = new("socks5://p:1", ProxyKind.Http);

        CommandLineProxy replaced = proxy with { Address = "p:1", KindWithoutScheme = ProxyKind.Socks4a };

        Assert.IsTrue(replaced.TryGetKind(out ProxyKind kind, out _));
        Assert.AreEqual(ProxyKind.Socks4a, kind);
        Assert.AreEqual("socks5://p:1", proxy.Address);
    }

    [TestMethod]
    [DataRow("bogus://h:1")]
    [DataRow("socks6://h:1")]
    [DataRow("ftp://127.0.0.1:1")]
    public void TryGetKind_UnsupportedScheme_FailsWithExit7(string address)
    {
        bool supported = new CommandLineProxy(address, ProxyKind.Http).TryGetKind(out _, out TransferResult? failure);

        Assert.IsFalse(supported);
        Assert.IsNotNull(failure);
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.AreEqual($"Unsupported proxy scheme for '{address}'", failure.ErrorMessage);
        Assert.AreEqual(0, failure.BytesTransferred);
    }
}
