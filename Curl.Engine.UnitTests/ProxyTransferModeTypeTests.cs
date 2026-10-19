using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class ProxyTransferModeTypeTests
{
    [TestMethod]
    [DataRow(ProxyKind.Http, false, 'i', DisplayName = "Binary")]
    [DataRow(ProxyKind.Http, true, 'a', DisplayName = "ASCII")]
    [DataRow(ProxyKind.Http10, true, 'a', DisplayName = "HTTP/1.0 proxy")]
    public void Of_ForwardedWithTheModeSet_GivesTheType(ProxyKind kind, bool ascii, char expected)
    {
        char? type = ProxyTransferModeType.Of(new ProxyEndpoint(kind, "proxy", 3128, null), tunnels: false, proxyTransferMode: true, ascii);

        Assert.AreEqual(expected, type);
    }

    [TestMethod]
    [DataRow(ProxyKind.Http, false, false, DisplayName = "Mode not set")]
    [DataRow(ProxyKind.Http, true, true, DisplayName = "Tunnelled")]
    [DataRow(ProxyKind.Socks5, false, true, DisplayName = "SOCKS proxy")]
    [DataRow(ProxyKind.Https, false, true, DisplayName = "HTTPS proxy")]
    public void Of_NotForwardedWithTheMode_GivesNone(ProxyKind kind, bool tunnels, bool mode)
    {
        Assert.IsNull(ProxyTransferModeType.Of(new ProxyEndpoint(kind, "proxy", 1080, null), tunnels, mode, false));
    }

    [TestMethod]
    public void Of_NoProxy_GivesNone()
    {
        Assert.IsNull(ProxyTransferModeType.Of(null, false, true, false));
    }

    [TestMethod]
    [DataRow(false, "GET ftp://www.example.com/moo/549;type=i HTTP/1.1\r\n", DisplayName = "Binary (upstream test549)")]
    [DataRow(true, "GET ftp://www.example.com/moo/550;type=a HTTP/1.1\r\n", DisplayName = "ASCII (upstream test550)")]
    public async Task PerformAsync_FtpForwardedWithTheMode_SendsTheTypeButLeavesItOffTheEffectiveUrl(bool ascii, string expectedRequestLine)
    {
        string url = ascii ? "ftp://www.example.com/moo/550" : "ftp://www.example.com/moo/549";
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nhi")]);
        await using IEasyTransfer easy = CreateEasy(connector, url, ascii);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        StringAssert.StartsWith(Encoding.ASCII.GetString(connector.Written), expectedRequestLine);
        Assert.AreEqual(url, easy.Info.EffectiveUrl);
    }

    [TestMethod]
    public async Task PerformAsync_FtpForwardedWithTheModeRedirectedToAQuery_ResolvesItAgainstTheUrlWithoutTheType()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes("HTTP/1.1 302 Found\r\nLocation: ?x\r\nContent-Length: 0\r\n\r\n")]);
        await using IEasyTransfer easy = CreateEasy(connector, "ftp://h/moo/549", ascii: false);

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("ftp://h/moo/549?x", easy.Info.RedirectUrl);
    }

    private static IEasyTransfer CreateEasy(ScriptedConnector connector, string url, bool ascii)
    {
        IEasyTransfer easy = new ManagedCurlEngine(
            TimeProvider.System,
            (_, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
                connector,
                new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no datagrams"),
                new PassThroughTlsProvider(),
                new LoopbackDnsResolver(),
                cookies)),
            readEnvironmentVariable: _ => null).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = url },
            Proxy = new EasyProxyOptions { Proxy = "http://proxy:3128" },
            Transfer = new EasyTransferOptions { ProxyTransferMode = true, TransferText = ascii },
        };
        return easy;
    }
}
