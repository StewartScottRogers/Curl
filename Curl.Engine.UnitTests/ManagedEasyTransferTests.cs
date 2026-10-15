using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.Options;
using Curl.Api.Options.Network;
using Curl.Api.Options.Proxy;
using Curl.Api.Options.Tls;
using Curl.Core;
using Curl.Protocol.Abstractions;

namespace Curl.Engine;

[TestClass]
public sealed class ManagedEasyTransferTests
{
    private const string Response = "HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Type: text/plain\r\n\r\nhello";

    private const string ConnectFailure = "Failed to connect to h port 80 after 0 ms: Could not connect to server";

    [TestMethod]
    public async Task PerformAsync_HttpGetOnFakeConnector_GivesTheBodyToTheWriteCallbackAndTheHeadersToTheHeaderCallback()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        MemoryStream body = new();
        List<string> headers = [];
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new()
            {
                Write = data =>
                {
                    body.Write(data);
                    return data.Length;
                },
                Header = header =>
                {
                    headers.Add(Encoding.ASCII.GetString(header));
                    return header.Length;
                },
            },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual("hello", Encoding.ASCII.GetString(body.ToArray()));
        Assert.AreEqual("HTTP/1.1 200 OK\r\nContent-Length: 5\r\nContent-Type: text/plain\r\n\r\n", string.Concat(headers));
        StringAssert.StartsWith(Encoding.ASCII.GetString(connector.Written), "GET /x HTTP/1.1\r\nHost: h\r\n");
        Assert.AreEqual(200, easy.Info.ResponseCode);
        Assert.AreEqual("http://h/x", easy.Info.EffectiveUrl);
        Assert.AreEqual(string.Empty, easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_HttpGetOnFakeConnector_FillsInfoFromTheTransferReport()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual("text/plain", easy.Info.ContentType);
    }

    [TestMethod]
    public async Task PerformAsync_NoCallbacksSet_DiscardsTheBodyAndHeadersAndSucceeds()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/x" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
    }

    [TestMethod]
    public async Task PerformAsync_WriteCallbackTakesFewerBytes_ReturnsWriteError()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Callbacks = new() { Write = _ => 0 },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.WriteError, code);
    }

    [TestMethod]
    public async Task PerformAsync_ConnectionRefused_ReturnsCouldntConnectWithTheConnectorsText()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.CouldntConnect, code);
        Assert.AreEqual(ConnectFailure, easy.ErrorText);
        Assert.AreEqual(0, easy.Info.ResponseCode);
    }

    [TestMethod]
    public async Task PerformAsync_NoUrlSet_ReturnsUrlMalformatWithNoUrlSet()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.AreEqual(ManagedEasyTransfer.NoUrlSetText, easy.ErrorText);
    }

    [TestMethod]
    public async Task PerformAsync_UnparsableUrl_ReturnsUrlMalformatAndKeepsTheUrlAsEffective()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://[::1/" } };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.UrlMalformat, code);
        Assert.AreEqual(ManagedEasyTransfer.MalformedUrlText, easy.ErrorText);
        Assert.AreEqual("http://[::1/", easy.Info.EffectiveUrl);
    }

    [TestMethod]
    public async Task PerformAsync_ResumeFromGiven_SendsTheRange()
    {
        ScriptedConnector connector = new([Encoding.ASCII.GetBytes(Response)]);
        await using IEasyTransfer easy = CreateEngine(connector).CreateEasy();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "http://h/x" },
            Transfer = new() { ResumeFrom = 2 },
        };

        await easy.PerformAsync(CancellationToken.None);

        StringAssert.Contains(Encoding.ASCII.GetString(connector.Written), "Range: bytes=2-\r\n");
    }

    [TestMethod]
    public async Task Reset_AfterOptionsSet_RestoresDefaultOptions()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/" } };

        easy.Reset();

        Assert.AreEqual(new EasyOptions(), easy.Options);
    }

    [TestMethod]
    public async Task Duplicate_AfterAFailedTransfer_CopiesTheOptionsAndNotTheResults()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();
        easy.Options = new EasyOptions { Network = new EasyNetworkOptions { Url = "http://h/" } };
        await easy.PerformAsync(CancellationToken.None);

        await using IEasyTransfer copy = easy.Duplicate();

        Assert.AreSame(easy.Options, copy.Options);
        Assert.AreEqual(string.Empty, copy.ErrorText);
        Assert.AreEqual(new Api.Info.TransferInfo(), copy.Info);
        Assert.AreEqual(CurlCode.CouldntConnect, await copy.PerformAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task Pause_GivenDirections_SucceedsAndRecordsThem()
    {
        await using ManagedEasyTransfer easy = (ManagedEasyTransfer)CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();

        CurlCode code = easy.Pause(CurlPause.Receive);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(CurlPause.Receive, easy.PausedDirections);
    }

    [TestMethod]
    public async Task UpkeepAsync_IdleHandle_ReturnsOk()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();

        Assert.AreEqual(CurlCode.Ok, await easy.UpkeepAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task HeadersWebSocketAndConnection_NotYetBuilt_ThrowNotSupported()
    {
        await using IEasyTransfer easy = CreateEngine(new RecordingConnector(CurlExitCode.CouldntConnect, ConnectFailure)).CreateEasy();

        Assert.ThrowsExactly<NotSupportedException>(() => easy.Headers);
        Assert.ThrowsExactly<NotSupportedException>(() => easy.WebSocket);
        Assert.ThrowsExactly<NotSupportedException>(() => easy.Connection);
    }

    [TestMethod]
    public void TransferSettingsOf_EasyGroups_CarriesVerifyPeerKeyPasswordEngineTunnelAndUnixSocket()
    {
        EasyOptions options = new()
        {
            Tls = new EasyTlsOptions { Origin = new TlsPeerOptions { VerifyPeer = false, KeyPassword = "pw" }, Engine = "e" },
            Proxy = new EasyProxyOptions { HttpProxyTunnel = true },
            Network = new EasyNetworkOptions { UnixSocketPath = "/s" },
        };

        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(options);

        Assert.IsTrue(settings.Insecure);
        Assert.AreEqual("pw", settings.Passphrase);
        Assert.AreEqual("e", settings.Engine);
        Assert.IsTrue(settings.ProxyTunnel);
        Assert.IsTrue(settings.OverUnixSocket);
        Assert.AreEqual("http://h/?a", settings.AppendUrlQuery("http://h/?a"));
    }

    [TestMethod]
    public void TransferSettingsOf_DefaultOptions_VerifiesPeerWithNoTunnelOrUnixSocket()
    {
        EngineTransferSettings settings = ManagedEasyTransfer.TransferSettingsOf(new EasyOptions());

        Assert.IsFalse(settings.Insecure);
        Assert.IsFalse(settings.ProxyTunnel);
        Assert.IsFalse(settings.OverUnixSocket);
        Assert.IsTrue(settings.Silent);
    }

    private static ManagedCurlEngine CreateEngine(IConnector connector) =>
        new(TimeProvider.System, _ => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver())));
}
