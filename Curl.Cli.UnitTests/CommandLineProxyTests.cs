using Curl.Protocol.Abstractions;
using Curl.Testing;

namespace Curl.Cli;

/// <summary>
/// Pins how <see cref="CommandLineProxy.TryGetKind"/> reads a proxy's scheme, as measured with the
/// reference curl 8.21.0 on 2026-09-26 (<c>Record-CurlExchange.ps1</c>, reading the first bytes the
/// proxy received, and <c>curl -x &lt;value&gt; http://127.0.0.1:1/</c> for the failures).
/// </summary>
[TestClass]
public sealed class CommandLineProxyTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

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
    [DataRow("http://p:1", ProxyKind.Http10, ProxyKind.Http10)]
    [DataRow("HTTP://p:1", ProxyKind.Http10, ProxyKind.Http10)]
    [DataRow("socks5://p:1", ProxyKind.Http10, ProxyKind.Socks5)]
    public void TryGetKind_SupportedScheme_OutranksTheOptionsKind(string address, ProxyKind kindWithoutScheme, ProxyKind expected)
    {
        bool supported = TryGetKind(address, kindWithoutScheme, out ProxyKind kind, out TransferResult? failure);

        Diagnostics.Assert("kind", expected, kind);
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
    [DataRow("p:1", ProxyKind.Http10)]
    public void TryGetKind_NoScheme_IsTheOptionsKind(string address, ProxyKind kindWithoutScheme)
    {
        bool supported = TryGetKind(address, kindWithoutScheme, out ProxyKind kind, out TransferResult? failure);

        Diagnostics.Assert("kind", kindWithoutScheme, kind);
        Assert.IsTrue(supported);
        Assert.AreEqual(kindWithoutScheme, kind);
        Assert.IsNull(failure);
    }

    [TestMethod]
    public void With_BothMembersReplaced_ReadsTheNewScheme()
    {
        CommandLineProxy proxy = new("socks5://p:1", ProxyKind.Http);
        Diagnostics.Arrange("proxy", proxy);

        CommandLineProxy replaced = proxy with { Address = "p:1", KindWithoutScheme = ProxyKind.Socks4a };
        Diagnostics.Act("replaced", replaced);

        bool supported = replaced.TryGetKind(out ProxyKind kind, out _);
        Diagnostics.Act("supported", supported);
        Diagnostics.Assert("kind", ProxyKind.Socks4a, kind);
        Diagnostics.Assert("original address", "socks5://p:1", proxy.Address);
        Assert.IsTrue(supported);
        Assert.AreEqual(ProxyKind.Socks4a, kind);
        Assert.AreEqual("socks5://p:1", proxy.Address);
    }

    [TestMethod]
    [DataRow("bogus://h:1")]
    [DataRow("socks6://h:1")]
    [DataRow("ftp://127.0.0.1:1")]
    public void TryGetKind_UnsupportedScheme_FailsWithExit7(string address)
    {
        bool supported = TryGetKind(address, ProxyKind.Http, out _, out TransferResult? failure);

        Diagnostics.Assert("exit code", CurlExitCode.CouldntConnect, failure?.ExitCode);
        Diagnostics.Assert("error message", $"Unsupported proxy scheme for '{address}'", failure?.ErrorMessage);
        Assert.IsFalse(supported);
        Assert.IsNotNull(failure);
        Assert.AreEqual(CurlExitCode.CouldntConnect, failure.ExitCode);
        Assert.AreEqual($"Unsupported proxy scheme for '{address}'", failure.ErrorMessage);
        Assert.AreEqual(0, failure.BytesTransferred);
    }

    private bool TryGetKind(string address, ProxyKind kindWithoutScheme, out ProxyKind kind, out TransferResult? failure)
    {
        Diagnostics.Arrange("address", $"\"{address}\"");
        Diagnostics.Arrange("kind without scheme", kindWithoutScheme);
        bool supported = new CommandLineProxy(address, kindWithoutScheme).TryGetKind(out kind, out failure);
        Diagnostics.Act("supported", supported);
        Diagnostics.Act("kind", kind);
        Diagnostics.Act("failure", failure is null ? "null" : $"exit {(int)failure.ExitCode} ({failure.ExitCode}), \"{failure.ErrorMessage}\", {failure.BytesTransferred} bytes");
        return supported;
    }
}
