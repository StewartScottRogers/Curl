using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Options.Protocols;
using Curl.Core;
using Curl.Protocol.Abstractions;
using Curl.Original.Protocol.Ws;

namespace Curl.Engine;

[TestClass]
public sealed class ManagedEasyWebSocketTests
{
    /// <summary>A server frame: unmasked, with a 7-bit length.</summary>
    internal static byte[] ServerFrame(WsOpcode opcode, bool isFinal, string payload)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(payload);
        return [(byte)((isFinal ? 0x80 : 0) | (int)opcode), (byte)bytes.Length, .. bytes];
    }

    /// <summary>A client frame as the engine writes it under <see cref="ZeroMaskSource" />: masked with four zero bytes.</summary>
    internal static byte[] ClientFrame(WsOpcode opcode, bool isFinal, string payload)
    {
        byte[] bytes = Encoding.ASCII.GetBytes(payload);
        return [(byte)((isFinal ? 0x80 : 0) | (int)opcode), (byte)(0x80 | bytes.Length), 0, 0, 0, 0, .. bytes];
    }

    private static async Task<(ManagedEasyWebSocket WebSocket, QueuedConnection Peer)> OpenAsync(WebSocketOptionBits options = WebSocketOptionBits.None, params byte[][] alreadyReceived)
    {
        ManagedEasyWebSocket webSocket = new(() => true, () => options, new ZeroMaskSource());
        QueuedConnection peer = new();
        await webSocket.OpenAsync(peer, alreadyReceived.SelectMany(chunk => chunk).ToArray());
        return (webSocket, peer);
    }

    private static string Text(byte[] buffer, CurlWebSocketReceiveResult result) =>
        Encoding.ASCII.GetString(buffer, 0, result.BytesReceived);

    [TestMethod]
    public async Task ReceiveAsync_BytesThatCameWithTheUpgrade_HandsTheFrameOverWithItsMetadata()
    {
        (ManagedEasyWebSocket webSocket, _) = await OpenAsync(WebSocketOptionBits.None, ServerFrame(WsOpcode.Text, true, "hello"));
        byte[] buffer = new byte[16];

        CurlWebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketReceiveResult(CurlCode.Ok, 5, new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 0, 5)), result);
        Assert.AreEqual("hello", Text(buffer, result));
        Assert.AreEqual(result.Frame, webSocket.CurrentFrame);
    }

    [TestMethod]
    public async Task ReceiveAsync_BufferSmallerThanTheFrame_HandsItOverInPiecesWithOffsetAndBytesLeft()
    {
        (ManagedEasyWebSocket webSocket, _) = await OpenAsync(WebSocketOptionBits.None, ServerFrame(WsOpcode.Binary, true, "abcde"));
        byte[] buffer = new byte[2];

        CurlWebSocketReceiveResult first = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        string firstText = Text(buffer, first);
        CurlWebSocketReceiveResult second = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        CurlWebSocketReceiveResult third = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 0, 3, 2), first.Frame);
        Assert.AreEqual("ab", firstText);
        Assert.AreEqual(new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 2, 1, 2), second.Frame);
        Assert.AreEqual(new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 4, 0, 1), third.Frame);
        Assert.AreEqual("e", Text(buffer, third));
    }

    [TestMethod]
    public async Task ReceiveAsync_FragmentedMessageWithAPingBetween_ReportsContinuationsAsTheMessagesTypeAndAnswersThePing()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        byte[] buffer = new byte[16];

        CurlWebSocketReceiveResult nothingYet = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        peer.Enqueue([.. ServerFrame(WsOpcode.Text, false, "Hel"), .. ServerFrame(WsOpcode.Ping, true, "p"), .. ServerFrame(WsOpcode.Continuation, true, "lo")]);
        CurlWebSocketReceiveResult first = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        CurlWebSocketReceiveResult last = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(CurlCode.Again, nothingYet.Result);
        Assert.AreEqual(CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation, first.Frame.Flags);
        Assert.AreEqual(CurlWebSocketFlags.Text, last.Frame.Flags);
        Assert.AreEqual("lo", Text(buffer, last));
        CollectionAssert.AreEqual(ClientFrame(WsOpcode.Pong, true, "p"), peer.Written);
    }

    [TestMethod]
    public async Task ReceiveAsync_PingWithAutoPongOn_AnswersThePingAndDoesNotHandItOver()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync(WebSocketOptionBits.None, ServerFrame(WsOpcode.Ping, true, "test"));
        byte[] buffer = new byte[16];

        CurlWebSocketReceiveResult afterPing = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        peer.Enqueue(ServerFrame(WsOpcode.Text, true, "next"));
        CurlWebSocketReceiveResult next = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        CollectionAssert.AreEqual(ClientFrame(WsOpcode.Pong, true, "test"), peer.Written);
        Assert.AreEqual(CurlCode.Again, afterPing.Result);
        Assert.AreEqual(new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 0, 4), next.Frame);
        Assert.AreEqual("next", Text(buffer, next));
    }

    [TestMethod]
    public async Task ReceiveAsync_PingWithAutoPongOff_HandsThePingOverAndSendsNothing()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync(WebSocketOptionBits.NoAutoPong, ServerFrame(WsOpcode.Ping, true, ""));

        CurlWebSocketReceiveResult ping = await webSocket.ReceiveAsync(new byte[4], CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketReceiveResult(CurlCode.Ok, 0, new CurlWebSocketFrame(0, CurlWebSocketFlags.Ping, 0, 0, 0)), ping);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task ReceiveAsync_PartOfAFrame_AnswersAgainUntilTheRestComes()
    {
        byte[] frame = ServerFrame(WsOpcode.Text, true, "abc");
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync(WebSocketOptionBits.None, frame[..2]);
        byte[] buffer = new byte[8];

        peer.Enqueue(frame[2..3]);
        CurlWebSocketReceiveResult firstByte = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        peer.Enqueue(frame[3..]);
        CurlWebSocketReceiveResult restOfFrame = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        CurlWebSocketReceiveResult nothingMore = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, firstByte.Result);
        Assert.AreEqual(new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 2, 1), firstByte.Frame);
        Assert.AreEqual(CurlCode.Ok, restOfFrame.Result);
        Assert.AreEqual(new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 1, 0, 2), restOfFrame.Frame);
        Assert.AreEqual(CurlCode.Again, nothingMore.Result);
    }

    [TestMethod]
    public async Task ReceiveAsync_OnlyAFrameHeader_AnswersAgain()
    {
        byte[] frame = ServerFrame(WsOpcode.Text, true, "abc");
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        peer.Enqueue(frame[..1]);

        CurlWebSocketReceiveResult result = await webSocket.ReceiveAsync(new byte[8], CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketReceiveResult(CurlCode.Again, 0, default), result);
    }

    [TestMethod]
    public async Task ReceiveAsync_ServerClosesTheConnection_AnswersGotNothing()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        peer.Enqueue([[]]);

        CurlWebSocketReceiveResult result = await webSocket.ReceiveAsync(new byte[8], CancellationToken.None);

        Assert.AreEqual(CurlCode.GotNothing, result.Result);
    }

    [TestMethod]
    public async Task ReceiveAsync_ReadFails_AnswersRecvError()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        peer.FailReads();

        CurlWebSocketReceiveResult result = await webSocket.ReceiveAsync(new byte[8], CancellationToken.None);

        Assert.AreEqual(CurlCode.RecvError, result.Result);
    }

    [TestMethod]
    public async Task ReceiveAsync_MaskedServerFrameAfterAFrame_HandsTheFrameOverThenAnswersRecvErrorFromThenOn()
    {
        byte[] masked = [0x81, 0x81, 1, 2, 3, 4, 5];
        (ManagedEasyWebSocket webSocket, _) = await OpenAsync(WebSocketOptionBits.None, ServerFrame(WsOpcode.Text, true, "ok"), masked);
        byte[] buffer = new byte[8];

        CurlWebSocketReceiveResult first = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        CurlWebSocketReceiveResult broken = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        CurlWebSocketReceiveResult again = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, first.Result);
        Assert.AreEqual(CurlCode.RecvError, broken.Result);
        Assert.AreEqual(CurlCode.RecvError, again.Result);
    }

    [TestMethod]
    public async Task ReceiveAsync_MaskedServerFrameAlone_AnswersRecvError()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        peer.Enqueue([0x81, 0x81, 1, 2, 3, 4, 5]);

        CurlWebSocketReceiveResult result = await webSocket.ReceiveAsync(new byte[8], CancellationToken.None);

        Assert.AreEqual(CurlCode.RecvError, result.Result);
    }

    [TestMethod]
    public async Task ReceiveAsync_PongCannotBeWritten_AnswersSendError()
    {
        ManagedEasyWebSocket webSocket = new(() => true, () => WebSocketOptionBits.None, new ZeroMaskSource());
        QueuedConnection peer = new() { WriteFailure = new IOException("gone") };
        await webSocket.OpenAsync(peer, ServerFrame(WsOpcode.Ping, true, "x"));
        byte[] buffer = new byte[8];

        CurlWebSocketReceiveResult failed = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(CurlCode.SendError, failed.Result);
    }

    [TestMethod]
    public async Task ReceiveAsync_PingThenAViolationAndThePongCannotBeWritten_KeepsRecvError()
    {
        ManagedEasyWebSocket webSocket = new(() => true, () => WebSocketOptionBits.None, new ZeroMaskSource());
        QueuedConnection peer = new() { WriteFailure = new IOException("gone") };
        await webSocket.OpenAsync(peer, (byte[])[.. ServerFrame(WsOpcode.Ping, true, "x"), 0x81, 0x81, 1, 2, 3, 4, 5]);
        byte[] buffer = new byte[8];

        await webSocket.ReceiveAsync(buffer, CancellationToken.None);
        CurlWebSocketReceiveResult failed = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(CurlCode.RecvError, failed.Result);
    }

    [TestMethod]
    public async Task ReceiveAndSend_NoConnection_AnswerBadFunctionArgument()
    {
        ManagedEasyWebSocket webSocket = new(() => true, () => WebSocketOptionBits.None, new ZeroMaskSource());

        CurlWebSocketReceiveResult received = await webSocket.ReceiveAsync(new byte[1], CancellationToken.None);
        CurlWebSocketSendResult sent = await webSocket.SendAsync("x"u8.ToArray(), 0, CurlWebSocketFlags.Text, CancellationToken.None);

        Assert.AreEqual(CurlCode.BadFunctionArgument, received.Result);
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0), sent);
        Assert.IsNull(webSocket.CurrentFrame);
    }

    [TestMethod]
    public async Task ReceiveAndSend_OptionsNoLongerConnectOnly_AnswerUnsupportedProtocol()
    {
        bool connectOnly = true;
        ManagedEasyWebSocket webSocket = new(() => connectOnly, () => WebSocketOptionBits.None, new ZeroMaskSource());
        await webSocket.OpenAsync(new QueuedConnection(), ServerFrame(WsOpcode.Text, true, "x"));
        connectOnly = false;

        CurlWebSocketReceiveResult received = await webSocket.ReceiveAsync(new byte[1], CancellationToken.None);
        CurlWebSocketSendResult sent = await webSocket.SendAsync("x"u8.ToArray(), 0, CurlWebSocketFlags.Text, CancellationToken.None);

        Assert.AreEqual(CurlCode.UnsupportedProtocol, received.Result);
        Assert.AreEqual(CurlCode.UnsupportedProtocol, sent.Result);
    }

    [TestMethod]
    public async Task SendAsync_FragmentedMessageWithAPingBetween_WritesTheFirstFragmentThenContinuations()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlWebSocketSendResult first = await webSocket.SendAsync("He"u8.ToArray(), 0, CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation, CancellationToken.None);
        await webSocket.SendAsync("p"u8.ToArray(), 0, CurlWebSocketFlags.Ping, CancellationToken.None);
        await webSocket.SendAsync("ll"u8.ToArray(), 0, CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation, CancellationToken.None);
        await webSocket.SendAsync("o"u8.ToArray(), 0, CurlWebSocketFlags.Text, CancellationToken.None);
        CurlWebSocketSendResult next = await webSocket.SendAsync("x"u8.ToArray(), 0, CurlWebSocketFlags.Binary, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 2), first);
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 1), next);
        byte[] expected =
        [
            .. ClientFrame(WsOpcode.Text, false, "He"),
            .. ClientFrame(WsOpcode.Ping, true, "p"),
            .. ClientFrame(WsOpcode.Continuation, false, "ll"),
            .. ClientFrame(WsOpcode.Continuation, true, "o"),
            .. ClientFrame(WsOpcode.Binary, true, "x"),
        ];
        CollectionAssert.AreEqual(expected, peer.Written);
    }

    [TestMethod]
    [DataRow(CurlWebSocketFlags.Continuation)]
    public async Task SendAsync_RefusedFlags_AnswersBadFunctionArgumentAndWritesNothing(CurlWebSocketFlags flags)
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlWebSocketSendResult result = await webSocket.SendAsync("x"u8.ToArray(), 1, flags, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0), result);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_WriteFails_AnswersSendError()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        peer.WriteFailure = new IOException("gone");

        CurlWebSocketSendResult result = await webSocket.SendAsync("x"u8.ToArray(), 0, CurlWebSocketFlags.Close, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.SendError, 0), result);
    }

    [TestMethod]
    public async Task SendAsync_FromTheWriteCallbackOfAPerformThatIsNotConnectOnly_WritesToTheConnectionItsFramesFlowOn()
    {
        // curl 8.22.0's curl_ws_send writes to data->conn from an ordinary perform's write callback: lib2301 answers
        // the ping that came with the 101 with a raw PONG (upstream test2301, BL-2710).
        ManagedEasyWebSocket webSocket = new(() => false, () => WebSocketOptionBits.RawMode, new ZeroMaskSource());
        byte[] pong = [0x8a, 0x00];
        ScriptedConnector connector = new([[.. Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\n\r\n"), 0x89, 0x00]]);
        IProtocolHandler handler = EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no datagrams"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            null,
            webSocketRawMode: true,
            webSocketPerformConnection: webSocket.PerformConnection).Single(candidate => candidate.SupportedSchemes.Contains("ws"));
        List<CurlWebSocketSendResult> sends = [];
        CallbackWriteStream output = new(data =>
        {
            sends.Add(webSocket.SendAsync(pong, 0, CurlWebSocketFlags.None, CancellationToken.None).AsTask().GetAwaiter().GetResult());
            return data.Length;
        });

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/2301"), Output = output });
        CurlWebSocketSendResult afterPerform = await webSocket.SendAsync(pong, 0, CurlWebSocketFlags.None, CancellationToken.None);

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(new[] { new CurlWebSocketSendResult(CurlCode.Ok, 2) }, sends);
        CollectionAssert.AreEqual(pong, connector.Written[^2..]);
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.UnsupportedProtocol, 0), afterPerform);
    }

    [TestMethod]
    public async Task CurrentFrame_FromTheWriteCallbackOfAPerformThatIsNotConnectOnly_IsTheFrameOfThePayloadWritten()
    {
        // curl 8.22.0's curl_ws_meta answers ws->recvframe from an ordinary perform's write callback: lib2302 prints
        // RECFLAGS: 1 for the text frame after the ping (upstream test2302, BL-2711).
        ManagedEasyWebSocket webSocket = new(() => false, () => WebSocketOptionBits.None, new ZeroMaskSource());
        ScriptedConnector connector = new([[.. Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\n\r\n"), 0x89, 0x00, 0x01, 0x03, (byte)'h', (byte)'e', (byte)'l', 0x80, 0x02, (byte)'l', (byte)'o', 0x82, 0x01, (byte)'!']]);
        IProtocolHandler handler = EngineComposition.CreateProtocolHandlers(
            connector,
            new RecordingDatagramConnector(CurlExitCode.CouldntConnect, "no datagrams"),
            new PassThroughTlsProvider(),
            new LoopbackDnsResolver(),
            null,
            webSocketPerformConnection: webSocket.PerformConnection).Single(candidate => candidate.SupportedSchemes.Contains("ws"));
        List<CurlWebSocketFrame?> frames = [];
        CallbackWriteStream output = new(data =>
        {
            frames.Add(webSocket.CurrentFrame);
            return data.Length;
        });

        TransferResult result = await handler.ExecuteAsync(new TransferContext { Url = CurlUrl.Parse("ws://h/2302"), Output = output });

        Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);
        CollectionAssert.AreEqual(
            new CurlWebSocketFrame?[]
            {
                new CurlWebSocketFrame(0, CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation, 0, 0, 3),
                new CurlWebSocketFrame(0, CurlWebSocketFlags.Text, 0, 0, 2),
                new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 0, 0, 1),
            },
            frames);
        Assert.AreEqual(new CurlWebSocketFrame(0, CurlWebSocketFlags.Binary, 0, 0, 1), webSocket.CurrentFrame);
    }

    [TestMethod]
    public async Task SendAsync_RawMode_WritesTheBytesUnframed()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync(WebSocketOptionBits.RawMode);
        byte[] raw = [0x81, 0x82, 0, 0, 0, 0, (byte)'h', (byte)'i'];

        CurlWebSocketSendResult result = await webSocket.SendAsync(raw, 0, CurlWebSocketFlags.None, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, raw.Length), result);
        CollectionAssert.AreEqual(raw, peer.Written);
    }

    [TestMethod]
    [DataRow(CurlWebSocketFlags.Text, 0L)]
    [DataRow(CurlWebSocketFlags.None, 4L)]
    public async Task SendAsync_RawModeWithFlagsOrFragmentSize_AnswersBadFunctionArgumentAndWritesNothing(CurlWebSocketFlags flags, long fragmentSize)
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync(WebSocketOptionBits.RawMode);

        CurlWebSocketSendResult result = await webSocket.SendAsync("x"u8.ToArray(), fragmentSize, flags, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0), result);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_RawModeWriteFails_AnswersSendError()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync(WebSocketOptionBits.RawMode);
        peer.WriteFailure = new IOException("gone");

        CurlWebSocketSendResult result = await webSocket.SendAsync("x"u8.ToArray(), 0, CurlWebSocketFlags.None, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.SendError, 0), result);
    }

    [TestMethod]
    public async Task ReceiveAsync_RawModePing_DecodesThePingAndSendsNoPong()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync(WebSocketOptionBits.RawMode, ServerFrame(WsOpcode.Ping, true, "p"));
        byte[] buffer = new byte[4];

        CurlWebSocketReceiveResult ping = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketReceiveResult(CurlCode.Ok, 1, new CurlWebSocketFrame(0, CurlWebSocketFlags.Ping, 0, 0, 1)), ping);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_TenByteTextInTwoOffsetParts_WritesOneHeadAnnouncingTenBytesAndTheMaskedPayload()
    {
        ManagedEasyWebSocket webSocket = new(() => true, () => WebSocketOptionBits.None, new CountingMaskSource());
        QueuedConnection peer = new();
        await webSocket.OpenAsync(peer, ReadOnlyMemory<byte>.Empty);

        CurlWebSocketSendResult first = await webSocket.SendAsync("hell"u8.ToArray(), 10, CurlWebSocketFlags.Text | CurlWebSocketFlags.Offset, CancellationToken.None);
        CurlWebSocketSendResult second = await webSocket.SendAsync("oworld"u8.ToArray(), 0, CurlWebSocketFlags.Text | CurlWebSocketFlags.Offset, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 4), first);
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 6), second);
        byte[] expected = WsFrameEncoder.Encode(WsOpcode.Text, true, "helloworld"u8, new CountingMaskSource());
        Assert.AreEqual(0x8a, expected[1]);
        CollectionAssert.AreEqual(expected, peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_FrameFinishedInParts_NextSendStartsANewFrame()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        await webSocket.SendAsync("ab"u8.ToArray(), 3, CurlWebSocketFlags.Binary | CurlWebSocketFlags.Offset, CancellationToken.None);
        await webSocket.SendAsync("c"u8.ToArray(), 0, CurlWebSocketFlags.Binary | CurlWebSocketFlags.Offset, CancellationToken.None);
        await webSocket.SendAsync("x"u8.ToArray(), 0, CurlWebSocketFlags.Text, CancellationToken.None);

        CollectionAssert.AreEqual(ClientFrame(WsOpcode.Binary, true, "abc").Concat(ClientFrame(WsOpcode.Text, true, "x")).ToArray(), peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_NonEmptyFirstPartWithFragmentSizeZero_AnswersBadFunctionArgumentAndWritesNothing()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlWebSocketSendResult result = await webSocket.SendAsync("a"u8.ToArray(), 0, CurlWebSocketFlags.Text | CurlWebSocketFlags.Offset, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0), result);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_AfterARefusedUnfinishedFirstPartWithFragmentSizeZero_StartsANewTextMessage()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        await webSocket.SendAsync("a"u8.ToArray(), 0, CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation | CurlWebSocketFlags.Offset, CancellationToken.None);

        CurlWebSocketSendResult result = await webSocket.SendAsync("b"u8.ToArray(), 0, CurlWebSocketFlags.Text, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 1), result);
        Assert.AreEqual(0x81, peer.Written[0]);
    }

    [TestMethod]
    public async Task SendAsync_FirstPartLongerThanTheFragmentSize_AnswersBadFunctionArgumentAndWritesNothing()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlWebSocketSendResult result = await webSocket.SendAsync("abc"u8.ToArray(), 2, CurlWebSocketFlags.Text | CurlWebSocketFlags.Offset, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0), result);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_PartPastWhatIsLeftOfTheFrame_AnswersBadFunctionArgumentAndWritesNothingMore()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        await webSocket.SendAsync("ab"u8.ToArray(), 4, CurlWebSocketFlags.Text | CurlWebSocketFlags.Offset, CancellationToken.None);
        int writtenBefore = peer.Written.Length;

        CurlWebSocketSendResult result = await webSocket.SendAsync("cde"u8.ToArray(), 0, CurlWebSocketFlags.Text | CurlWebSocketFlags.Offset, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.BadFunctionArgument, 0), result);
        Assert.HasCount(writtenBefore, peer.Written);
    }

    [TestMethod]
    public async Task SendAsync_NegativeFragmentSize_AnswersSendError()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlWebSocketSendResult result = await webSocket.SendAsync("a"u8.ToArray(), -1, CurlWebSocketFlags.Text | CurlWebSocketFlags.Offset, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.SendError, 0), result);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task StartFrameAsync_ThenSends_WritesTheHeadAndThePayloadAsOneFrame()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlCode started = await webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 5, CancellationToken.None);
        CurlWebSocketSendResult first = await webSocket.SendAsync("he"u8.ToArray(), 0, CurlWebSocketFlags.None, CancellationToken.None);
        CurlWebSocketSendResult second = await webSocket.SendAsync("llo"u8.ToArray(), 0, CurlWebSocketFlags.None, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, started);
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 2), first);
        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.Ok, 3), second);
        CollectionAssert.AreEqual(ClientFrame(WsOpcode.Text, true, "hello"), peer.Written);
    }

    [TestMethod]
    public async Task StartFrameAsync_EmptyFrame_WritesTheWholeFrame()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlCode result = await webSocket.StartFrameAsync(CurlWebSocketFlags.Ping, 0, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, result);
        CollectionAssert.AreEqual(ClientFrame(WsOpcode.Ping, true, string.Empty), peer.Written);
    }

    [TestMethod]
    public async Task StartFrameAsync_PreviousFrameUnfinished_AnswersSendErrorAndWritesNothingMore()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        await webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 3, CancellationToken.None);
        int writtenBefore = peer.Written.Length;

        CurlCode result = await webSocket.StartFrameAsync(CurlWebSocketFlags.Binary, 1, CancellationToken.None);

        Assert.AreEqual(CurlCode.SendError, result);
        Assert.HasCount(writtenBefore, peer.Written);
    }

    [TestMethod]
    [DataRow(CurlWebSocketFlags.Text, -1L, CurlCode.SendError)]
    [DataRow(CurlWebSocketFlags.Continuation, 1L, CurlCode.BadFunctionArgument)]
    [DataRow(CurlWebSocketFlags.Text | CurlWebSocketFlags.Binary, 1L, CurlCode.BadFunctionArgument)]
    public async Task StartFrameAsync_RefusedLengthOrBits_AnswersLibcurlsCodeAndWritesNothing(CurlWebSocketFlags flags, long frameLength, CurlCode expected)
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();

        CurlCode result = await webSocket.StartFrameAsync(flags, frameLength, CancellationToken.None);

        Assert.AreEqual(expected, result);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task StartFrameAsync_RawMode_AnswersFailedInit()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync(WebSocketOptionBits.RawMode);

        CurlCode result = await webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 1, CancellationToken.None);

        Assert.AreEqual(CurlCode.FailedInit, result);
        Assert.IsEmpty(peer.Written);
    }

    [TestMethod]
    public async Task StartFrameAsync_NoUpgradedConnection_AnswersSendError()
    {
        ManagedEasyWebSocket webSocket = new(() => true, () => WebSocketOptionBits.None, new ZeroMaskSource());

        CurlCode result = await webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 1, CancellationToken.None);

        Assert.AreEqual(CurlCode.SendError, result);
    }

    [TestMethod]
    public async Task StartFrameAsync_WriteFails_AnswersSendError()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        peer.WriteFailure = new IOException("gone");

        CurlCode result = await webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 1, CancellationToken.None);

        Assert.AreEqual(CurlCode.SendError, result);
    }

    [TestMethod]
    public async Task SendAsync_PartWriteFails_AnswersSendError()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection peer) = await OpenAsync();
        await webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 2, CancellationToken.None);
        peer.WriteFailure = new IOException("gone");

        CurlWebSocketSendResult result = await webSocket.SendAsync("a"u8.ToArray(), 0, CurlWebSocketFlags.None, CancellationToken.None);

        Assert.AreEqual(new CurlWebSocketSendResult(CurlCode.SendError, 0), result);
    }

    [TestMethod]
    public async Task OpenAsync_FrameUnfinishedOnTheOldConnection_TheNewConnectionStartsAFreshFrame()
    {
        (ManagedEasyWebSocket webSocket, _) = await OpenAsync();
        await webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 3, CancellationToken.None);
        QueuedConnection second = new();
        await webSocket.OpenAsync(second, ReadOnlyMemory<byte>.Empty);

        CurlCode result = await webSocket.StartFrameAsync(CurlWebSocketFlags.Binary, 0, CancellationToken.None);

        Assert.AreEqual(CurlCode.Ok, result);
        CollectionAssert.AreEqual(ClientFrame(WsOpcode.Binary, true, string.Empty), second.Written);
    }

    [TestMethod]
    public async Task OpenAsync_SecondConnection_ClosesTheFirstAndForgetsItsFrames()
    {
        (ManagedEasyWebSocket webSocket, QueuedConnection first) = await OpenAsync(WebSocketOptionBits.None, ServerFrame(WsOpcode.Text, true, "old"));
        await webSocket.ReceiveAsync(new byte[1], CancellationToken.None);
        QueuedConnection second = new();

        await webSocket.OpenAsync(second, ServerFrame(WsOpcode.Binary, true, "new"));
        byte[] buffer = new byte[8];
        CurlWebSocketReceiveResult result = await webSocket.ReceiveAsync(buffer, CancellationToken.None);

        Assert.IsTrue(first.IsDisposed);
        Assert.IsFalse(second.IsDisposed);
        Assert.AreEqual("new", Text(buffer, result));
        Assert.AreEqual(CurlWebSocketFlags.Binary, result.Frame.Flags);
    }

    [TestMethod]
    public async Task ReceiveAsync_Cancelled_Throws()
    {
        (ManagedEasyWebSocket webSocket, _) = await OpenAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await webSocket.ReceiveAsync(new byte[1], new CancellationToken(true)));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await webSocket.SendAsync(new byte[1], 0, CurlWebSocketFlags.Text, new CancellationToken(true)));
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await webSocket.StartFrameAsync(CurlWebSocketFlags.Text, 1, new CancellationToken(true)));
    }

    /// <summary>Masks every frame with four zero bytes, so a masked payload reads as it was given.</summary>
    internal sealed class ZeroMaskSource : IWebSocketRandomSource
    {
        public void Fill(Span<byte> destination) => destination.Clear();
    }

    /// <summary>Masks every frame with 01 02 03 04, so a part masked at the wrong offset shows.</summary>
    private sealed class CountingMaskSource : IWebSocketRandomSource
    {
        public void Fill(Span<byte> destination)
        {
            for (int index = 0; index < destination.Length; index++)
            {
                destination[index] = (byte)(index + 1);
            }
        }
    }
}
