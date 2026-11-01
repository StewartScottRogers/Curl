using System.Text;
using Curl.Api.Callbacks;
using Curl.Api.Codes;
using Curl.Api.EngineContracts;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Options;
using Curl.Api.Options.Behaviour;
using Curl.Api.Options.Network;
using Curl.Api.Options.Protocols;
using Curl.Authentication;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

public sealed partial class ManagedEasyTransferTests
{
    private const string SwitchingProtocolsHead = "HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\n\r\n";

    private static EasyOptions WebSocketConnectOnlyTo(string url, WebSocketOptionBits bits = WebSocketOptionBits.None) =>
        new()
        {
            Network = new EasyNetworkOptions { Url = url, ConnectOnly = ConnectOnly.WebSocket },
            Protocols = new EasyProtocolOptions { WebSocket = new EasyWebSocketOptions { Options = bits } },
        };

    private static (ManagedEasyTransfer Easy, TargetRecordingConnector Connector, QueuedConnection Peer) CreateWebSocketEasy(params byte[][] serverBytes)
    {
        QueuedConnection peer = new();
        peer.Enqueue(serverBytes);
        (IEasyTransfer easy, TargetRecordingConnector connector) = CreateConnectOnlyEasy(ConnectResult.Connected(peer));
        ManagedEasyTransfer managed = (ManagedEasyTransfer)easy;
        managed.WebSocketRandomSource = new ManagedEasyWebSocketTests.ZeroMaskSource();
        return (managed, connector, peer);
    }

    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    [TestMethod]
    public async Task PerformAsync_WebSocketConnectOnly_UpgradesAndExchangesFramesWithTheServer()
    {
        (ManagedEasyTransfer easy, TargetRecordingConnector connector, QueuedConnection peer) =
            CreateWebSocketEasy([.. Ascii(SwitchingProtocolsHead), .. ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Text, true, "hi")]);
        await using IEasyTransfer handle = easy;
        easy.Options = WebSocketConnectOnlyTo("ws://h:9000/chat");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        int requestLength = peer.Written.Length;
        byte[] buffer = new byte[8];
        CurlWebSocketReceiveResult received = await easy.WebSocket.ReceiveAsync(buffer, CancellationToken.None);
        CurlWebSocketSendResult sent = await easy.WebSocket.SendAsync(Ascii("yo"), 0, CurlWebSocketFlags.Text, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(("h", 9000, false), (connector.Targets.Single().Host, connector.Targets.Single().Port, connector.Targets.Single().UseTls));
        StringAssert.StartsWith(Encoding.ASCII.GetString(peer.Written), "GET /chat HTTP/1.1\r\nHost: h:9000\r\n");
        Assert.AreEqual(new CurlWebSocketReceiveResult(CurlCode.Ok, 2, new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 0, 2)), received);
        Assert.AreEqual("hi", Encoding.ASCII.GetString(buffer, 0, 2));
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 2), sent);
        CollectionAssert.AreEqual(ManagedEasyWebSocketTests.ClientFrame(WsOpcode.Text, true, "yo"), peer.Written[requestLength..]);
        Assert.AreEqual(101, easy.Info.ResponseCode);
        Assert.IsFalse(peer.IsDisposed);
    }

    [TestMethod]
    public async Task PerformAsync_WebSocketConnectOnly_ReceivesFragmentsAndAnswersAPingBetweenThem()
    {
        (ManagedEasyTransfer easy, _, QueuedConnection peer) = CreateWebSocketEasy(Ascii(SwitchingProtocolsHead));
        await using IEasyTransfer handle = easy;
        easy.Options = WebSocketConnectOnlyTo("wss://h/");
        await easy.PerformAsync(CancellationToken.None);
        int requestLength = peer.Written.Length;
        peer.Enqueue(
            ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Binary, false, "ab"),
            ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Ping, true, "?"),
            ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Continuation, true, "cd"));
        byte[] buffer = new byte[8];

        CurlWebSocketFrame first = (await easy.WebSocket.ReceiveAsync(buffer, CancellationToken.None)).Frame;
        CurlWebSocketReceiveResult afterPing = await easy.WebSocket.ReceiveAsync(buffer, CancellationToken.None);
        CurlWebSocketFrame last = (await easy.WebSocket.ReceiveAsync(buffer, CancellationToken.None)).Frame;

        Assert.AreEqual(CurlWebSocketFlags.Binary | CurlWebSocketFlags.Continuation, first.Flags);
        Assert.AreEqual(CurlCode.Again, afterPing.Result);
        Assert.AreEqual(CurlWebSocketFlags.Binary, last.Flags);
        Assert.AreEqual(last, easy.WebSocket.CurrentFrame);
        CollectionAssert.AreEqual(ManagedEasyWebSocketTests.ClientFrame(WsOpcode.Pong, true, "?"), peer.Written[requestLength..]);
    }

    [TestMethod]
    public async Task PerformAsync_WebSocketConnectOnlyWithNoAutoPong_SendsNoPong()
    {
        (ManagedEasyTransfer easy, _, QueuedConnection peer) =
            CreateWebSocketEasy([.. Ascii(SwitchingProtocolsHead), .. ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Ping, true, "?")]);
        await using IEasyTransfer handle = easy;
        easy.Options = WebSocketConnectOnlyTo("ws://h/", WebSocketOptionBits.NoAutoPong);

        await easy.PerformAsync(CancellationToken.None);
        int requestLength = peer.Written.Length;
        CurlWebSocketReceiveResult ping = await easy.WebSocket.ReceiveAsync(new byte[4], CancellationToken.None);

        Assert.AreEqual(CurlWebSocketFlags.Ping, ping.Frame.Flags);
        Assert.AreEqual(requestLength, peer.Written.Length);
    }

    [TestMethod]
    public async Task PerformAsync_WebSocketConnectOnlyWithRawMode_SendsTheBytesUnframedAndNoPong()
    {
        (ManagedEasyTransfer easy, _, QueuedConnection peer) =
            CreateWebSocketEasy([.. Ascii(SwitchingProtocolsHead), .. ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Ping, true, "?")]);
        await using IEasyTransfer handle = easy;
        easy.Options = WebSocketConnectOnlyTo("ws://h/", WebSocketOptionBits.RawMode);

        await easy.PerformAsync(CancellationToken.None);
        int requestLength = peer.Written.Length;
        CurlWebSocketReceiveResult ping = await easy.WebSocket.ReceiveAsync(new byte[4], CancellationToken.None);
        CurlWebSocketSendResult sent = await easy.WebSocket.SendAsync(Ascii("raw"), 0, CurlWebSocketFlags.None, CancellationToken.None);

        Assert.AreEqual(CurlWebSocketFlags.Ping, ping.Frame.Flags);
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 3), sent);
        CollectionAssert.AreEqual(Ascii("raw"), peer.Written[requestLength..]);
    }

    [TestMethod]
    public async Task PerformAsync_WebSocketUpgradeRefused_AnswersTheTransfersCodeAndLeavesNoWebSocket()
    {
        (ManagedEasyTransfer easy, _, QueuedConnection peer) = CreateWebSocketEasy(Ascii("HTTP/1.1 200 OK\r\nContent-Length: 0\r\n\r\n"));
        await using IEasyTransfer handle = easy;
        easy.Options = WebSocketConnectOnlyTo("ws://h/");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        CurlWebSocketReceiveResult received = await easy.WebSocket.ReceiveAsync(new byte[1], CancellationToken.None);

        Assert.AreEqual(CurlCode.HttpReturnedError, code);
        Assert.AreEqual(CurlCode.BadFunctionArgument, received.Result);
        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    public async Task PerformAsync_WebSocketServerClosesBeforeTheHeadEnds_AnswersOkAndLeavesNoWebSocket()
    {
        (ManagedEasyTransfer easy, _, QueuedConnection peer) = CreateWebSocketEasy(Ascii("HTTP/1.1 101 Switching\nUpgrade: websocket\n"), []);
        await using IEasyTransfer handle = easy;
        easy.Options = WebSocketConnectOnlyTo("ws://h/2304");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        CurlWebSocketSendResult sent = await easy.WebSocket.SendAsync(Ascii("foobar"), 0, CurlWebSocketFlags.Ping, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(CurlCode.BadFunctionArgument, sent.Result);
        Assert.IsTrue(peer.IsDisposed);
    }

    [TestMethod]
    public async Task PerformAsync_WebSocketConnectOnlyForAnHttpUrl_OnlyConnects()
    {
        (ManagedEasyTransfer easy, TargetRecordingConnector connector, QueuedConnection peer) = CreateWebSocketEasy();
        await using IEasyTransfer handle = easy;
        easy.Options = WebSocketConnectOnlyTo("http://h/");

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(new ConnectTarget("h", 80, false), connector.Targets.Single());
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task PerformAsyncAndDisposeAsync_AfterAWebSocketConnectOnlyPerform_CloseTheUpgradedConnection()
    {
        (ManagedEasyTransfer easy, _, QueuedConnection peer) = CreateWebSocketEasy(Ascii(SwitchingProtocolsHead));
        easy.Options = WebSocketConnectOnlyTo("ws://h/");
        await easy.PerformAsync(CancellationToken.None);
        ManagedEasyTransfer copy = (ManagedEasyTransfer)easy.Duplicate();

        bool openAfterPerform = !peer.IsDisposed;
        await easy.DisposeAsync();

        Assert.IsTrue(openAfterPerform);
        Assert.IsTrue(peer.IsDisposed);
        Assert.AreSame(easy.WebSocketRandomSource, copy.WebSocketRandomSource);
        await copy.DisposeAsync();
    }

    [TestMethod]
    [DataRow(WebSocketOptionBits.RawMode, "\x81\x02hi\x89\x01?")]
    [DataRow(WebSocketOptionBits.None, "hi")]
    public async Task PerformAsync_WebSocketTransferToTheWriteCallback_HandsOverRawFramesOnlyUnderRawMode(WebSocketOptionBits bits, string expectedBody)
    {
        byte[] frames = [.. ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Text, true, "hi"), .. ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Ping, true, "?")];
        ScriptedConnector connector = new([[.. Ascii(SwitchingProtocolsHead), .. frames]]);
        await using IEasyTransfer easy = new ManagedCurlEngine(TimeProvider.System, (options, _, cookies) => new ProtocolDispatcher(EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, ConnectFailure),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            cookies,
            webSocketRawMode: ManagedCurlEngine.IsWebSocketRawMode(options)))).CreateEasy();
        MemoryStream body = new();
        easy.Options = new EasyOptions
        {
            Network = new EasyNetworkOptions { Url = "ws://h/chat" },
            Protocols = new EasyProtocolOptions { WebSocket = new EasyWebSocketOptions { Options = bits } },
            Callbacks = new()
            {
                Write = data =>
                {
                    body.Write(data);
                    return data.Length;
                },
            },
        };

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(expectedBody, Encoding.Latin1.GetString(body.ToArray()));
        Assert.AreEqual(101, easy.Info.ResponseCode);
    }

    [TestMethod]
    public async Task PerformAsync_WebSocketUploadThroughTheReadCallback_SendsEachReadAsOneBinaryFrame()
    {
        // curl 8.22.0 lib/ws.c cr_ws_read: with no frame started, the bytes of each read become one
        // CURLWS_BINARY frame of their length (BL-2611).
        Queue<string> reads = new(["abc", "de"]);
        (IEasyTransfer easy, ScriptedConnector connector) = CreateWebSocketUploadEasy(buffer => GiveNext(reads, buffer));
        await using IEasyTransfer handle = easy;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        CollectionAssert.AreEqual(
            (byte[])[.. ManagedEasyWebSocketTests.ClientFrame(WsOpcode.Binary, true, "abc"), .. ManagedEasyWebSocketTests.ClientFrame(WsOpcode.Binary, true, "de")],
            AfterRequest(connector.Written));
    }

    [TestMethod]
    public async Task PerformAsync_ReadCallbackStartsATextFrame_SendsOneTextFrameAnnouncingItsBytes()
    {
        CurlCode started = CurlCode.SendError;
        Queue<string> reads = new(["hello"]);
        IEasyTransfer? self = null;
        (IEasyTransfer easy, ScriptedConnector connector) = CreateWebSocketUploadEasy(
            buffer =>
            {
                if (reads.Count > 0)
                {
                    started = self!.WebSocket.StartFrameAsync(CurlWebSocketFlags.Text, 5, CancellationToken.None).AsTask().GetAwaiter().GetResult();
                }

                return GiveNext(reads, buffer);
            });
        self = easy;
        await using IEasyTransfer handle = easy;

        CurlCode code = await easy.PerformAsync(CancellationToken.None);
        CurlCode afterPerform = await easy.WebSocket.StartFrameAsync(CurlWebSocketFlags.Text, 1, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, code);
        Assert.AreEqual(CurlCode.Ok, started);
        CollectionAssert.AreEqual(ManagedEasyWebSocketTests.ClientFrame(WsOpcode.Text, true, "hello"), AfterRequest(connector.Written));
        Assert.AreEqual(CurlCode.SendError, afterPerform);
    }

    [TestMethod]
    public async Task PerformAsync_ReadCallbackStartsAFrameWithBadBitsOrWhileOneIsUnfinished_IsRefused()
    {
        List<CurlCode> answers = [];
        bool first = true;
        IEasyTransfer? self = null;
        (IEasyTransfer easy, _) = CreateWebSocketUploadEasy(
            buffer =>
            {
                if (!first)
                {
                    return 0;
                }

                first = false;
                IEasyWebSocket webSocket = self!.WebSocket;
                answers.Add(webSocket.StartFrameAsync(CurlWebSocketFlags.Text | CurlWebSocketFlags.Binary, 1, CancellationToken.None).AsTask().GetAwaiter().GetResult());
                answers.Add(webSocket.StartFrameAsync(CurlWebSocketFlags.Text, -1, CancellationToken.None).AsTask().GetAwaiter().GetResult());
                answers.Add(webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 1, CancellationToken.None).AsTask().GetAwaiter().GetResult());
                answers.Add(webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 1, CancellationToken.None).AsTask().GetAwaiter().GetResult());
                buffer[0] = (byte)'x';
                return 1;
            });
        self = easy;
        await using IEasyTransfer handle = easy;

        await easy.PerformAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { CurlCode.BadFunctionArgument, CurlCode.SendError, CurlCode.Ok, CurlCode.SendError }, answers);
    }

    private static (IEasyTransfer Easy, ScriptedConnector Connector) CreateWebSocketUploadEasy(ReadCallback read)
    {
        ScriptedConnector connector = new([[.. Ascii(SwitchingProtocolsHead), .. ManagedEasyWebSocketTests.ServerFrame(WsOpcode.Text, true, "hi")]]);
        IEasyTransfer easy = new ManagedCurlEngine(TimeProvider.System, (options, state, _) => new ProtocolDispatcher(
            [new WsProtocolHandler(connector, ManagedCurlEngine.CreateWebSocketAuthenticator(options, state), new ManagedEasyWebSocketTests.ZeroMaskSource())])).CreateEasy();
        ((ManagedEasyTransfer)easy).WebSocketRandomSource = new ManagedEasyWebSocketTests.ZeroMaskSource();
        EasyOptions options = new()
        {
            Network = new EasyNetworkOptions { Url = "ws://h/chat" },
            Transfer = new EasyTransferOptions { Upload = true },
            Callbacks = new() { Read = read },
        };
        easy.Options = options;
        return (easy, connector);
    }

    private static long GiveNext(Queue<string> reads, Span<byte> buffer)
    {
        if (!reads.TryDequeue(out string? text))
        {
            return 0;
        }

        byte[] bytes = Ascii(text);
        bytes.CopyTo(buffer);
        return bytes.Length;
    }

    private static byte[] AfterRequest(byte[] written)
    {
        int requestEnd = Encoding.ASCII.GetString(written).IndexOf("\r\n\r\n", StringComparison.Ordinal) + 4;
        return written[requestEnd..];
    }

    [TestMethod]
    public void CreateWebSocketAuthenticator_DefaultOptions_BuildsTheRankedAuthenticator()
    {
        ManagedEngineSharedState shared = new(TimeProvider.System, _ => string.Empty);

        IHttpAuthenticator authenticator = ManagedCurlEngine.CreateWebSocketAuthenticator(new EasyOptions(), shared);

        Assert.IsInstanceOfType<RankedHttpAuthenticator>(authenticator);
    }
}
