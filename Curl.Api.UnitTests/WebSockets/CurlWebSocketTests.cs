using System.Text;
using Curl.Api.Codes;
using Curl.Api.EngineContracts.WebSocket;
using Curl.Api.Errors;

namespace Curl.Api.WebSockets;

[TestClass]
public sealed class CurlWebSocketTests
{
    private readonly FakeWebSocketTransfer _transfer = new();
    private int _waits;

    private FakeEasyWebSocket Fake => _transfer.FakeWebSocket;

    [TestMethod]
    public async Task ReceiveAsync_TextFrame_GivesTextMessage()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue("hello"u8.ToArray(), CurlWebSocketFlags.Text);

        CurlWebSocketMessage message = await socket.ReceiveAsync();

        Assert.AreEqual(CurlWebSocketMessageType.Text, message.Type);
        Assert.AreEqual("hello", message.Text);
        Assert.IsNull(message.CloseStatus);
        Assert.AreEqual(string.Empty, message.CloseReason);
    }

    [TestMethod]
    public async Task ReceiveAsync_BinaryFrame_GivesBinaryMessage()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue([1, 2, 3], CurlWebSocketFlags.Binary);

        CurlWebSocketMessage message = await socket.ReceiveAsync();

        Assert.AreEqual(CurlWebSocketMessageType.Binary, message.Type);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, message.Data.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_DataFrameWithNoTypeBit_GivesBinaryMessage()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue([9], CurlWebSocketFlags.None);

        CurlWebSocketMessage message = await socket.ReceiveAsync();

        Assert.AreEqual(CurlWebSocketMessageType.Binary, message.Type);
    }

    [TestMethod]
    public async Task ReceiveAsync_FragmentedMessage_JoinsFragmentsAndPieces()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue("he"u8.ToArray(), CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation, bytesLeft: 1)
            .Queue("l"u8.ToArray(), CurlWebSocketFlags.Text | CurlWebSocketFlags.Continuation)
            .Queue("lo"u8.ToArray(), CurlWebSocketFlags.Text, bytesLeft: 1)
            .Queue("!"u8.ToArray(), CurlWebSocketFlags.Text)
            .Queue("next"u8.ToArray(), CurlWebSocketFlags.Text);

        CurlWebSocketMessage first = await socket.ReceiveAsync();
        CurlWebSocketMessage second = await socket.ReceiveAsync();

        Assert.AreEqual("hello!", first.Text);
        Assert.AreEqual("next", second.Text);
    }

    [TestMethod]
    public async Task ReceiveAsync_PingBetweenFragments_GivesPingThenWholeMessage()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue([1], CurlWebSocketFlags.Binary | CurlWebSocketFlags.Continuation)
            .Queue("p"u8.ToArray(), CurlWebSocketFlags.Ping, bytesLeft: 1)
            .Queue("g"u8.ToArray(), CurlWebSocketFlags.Ping)
            .Queue([2], CurlWebSocketFlags.Binary);

        CurlWebSocketMessage ping = await socket.ReceiveAsync();
        CurlWebSocketMessage data = await socket.ReceiveAsync();

        Assert.AreEqual(CurlWebSocketMessageType.Ping, ping.Type);
        Assert.AreEqual("pg", ping.Text);
        Assert.AreEqual(CurlWebSocketMessageType.Binary, data.Type);
        CollectionAssert.AreEqual(new byte[] { 1, 2 }, data.Data.ToArray());
    }

    [TestMethod]
    public async Task ReceiveAsync_Pong_GivesPongMessage()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue("x"u8.ToArray(), CurlWebSocketFlags.Pong);

        CurlWebSocketMessage message = await socket.ReceiveAsync();

        Assert.AreEqual(CurlWebSocketMessageType.Pong, message.Type);
        Assert.AreEqual("x", message.Text);
    }

    [TestMethod]
    public async Task ReceiveAsync_CloseWithStatusAndReason_GivesBoth()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue([0x03, 0xE9, (byte)'b', (byte)'y', (byte)'e'], CurlWebSocketFlags.Close);

        CurlWebSocketMessage message = await socket.ReceiveAsync();

        Assert.AreEqual(CurlWebSocketMessageType.Close, message.Type);
        Assert.AreEqual((ushort)1001, message.CloseStatus);
        Assert.AreEqual("bye", message.CloseReason);
    }

    [TestMethod]
    public async Task ReceiveAsync_EmptyClose_HasNoStatus()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue([], CurlWebSocketFlags.Close);

        CurlWebSocketMessage message = await socket.ReceiveAsync();

        Assert.AreEqual(CurlWebSocketMessageType.Close, message.Type);
        Assert.IsNull(message.CloseStatus);
        Assert.AreEqual(string.Empty, message.CloseReason);
    }

    [TestMethod]
    public async Task ReceiveAsync_ConnectionClosed_ThrowsCurlExceptionWithStrerrorText()
    {
        await using CurlWebSocket socket = Create();
        Fake.QueueResult(CurlCode.GotNothing);

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(async () => await socket.ReceiveAsync());

        Assert.AreEqual(CurlCode.GotNothing, failure.Code);
        Assert.AreEqual(CurlCodeText.Of(CurlCode.GotNothing), failure.Message);
    }

    [TestMethod]
    public async Task ReceiveFrameAsync_AnsweredAgain_WaitsAndAsksAgain()
    {
        await using CurlWebSocket socket = Create();
        Fake.QueueResult(CurlCode.Again).QueueResult(CurlCode.Again).Queue([7, 8], CurlWebSocketFlags.Binary, bytesLeft: 3);
        byte[] buffer = new byte[4];

        CurlWebSocketFramePiece piece = await socket.ReceiveFrameAsync(buffer);

        Assert.AreEqual(2, _waits);
        Assert.AreEqual(2, piece.BytesReceived);
        Assert.AreEqual(3L, piece.Frame.BytesLeft);
        Assert.AreEqual(CurlWebSocketFlags.Binary, piece.Frame.Flags);
        CollectionAssert.AreEqual(new byte[] { 7, 8, 0, 0 }, buffer);
    }

    [TestMethod]
    public async Task ReceiveAsync_CancelledWhileWaiting_ThrowsOperationCanceled()
    {
        using CancellationTokenSource cancellation = new();
        await using CurlWebSocket socket = new(_transfer, _ =>
        {
            cancellation.Cancel();
            return Task.CompletedTask;
        });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await socket.ReceiveAsync(cancellation.Token));

        Assert.AreEqual(1, Fake.ReceiveCount);
    }

    [TestMethod]
    public async Task ReadMessagesAsync_MessagesThenClose_GivesEachThroughTheClose()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue("a"u8.ToArray(), CurlWebSocketFlags.Text)
            .Queue([1], CurlWebSocketFlags.Binary)
            .Queue([0x03, 0xE8], CurlWebSocketFlags.Close)
            .Queue("after"u8.ToArray(), CurlWebSocketFlags.Text);
        List<CurlWebSocketMessageType> types = [];

        await foreach (CurlWebSocketMessage message in socket.ReadMessagesAsync())
        {
            types.Add(message.Type);
        }

        CollectionAssert.AreEqual(new[] { CurlWebSocketMessageType.Text, CurlWebSocketMessageType.Binary, CurlWebSocketMessageType.Close }, types);
    }

    [TestMethod]
    public async Task ReadMessagesAsync_ReceivesCompletingLater_GivesEachThroughTheClose()
    {
        await using CurlWebSocket socket = new(_transfer, async _ => await Task.Yield());
        Fake.QueueResult(CurlCode.Again).Queue("a"u8.ToArray(), CurlWebSocketFlags.Text)
            .QueueResult(CurlCode.Again).Queue([], CurlWebSocketFlags.Close);
        using CancellationTokenSource first = new();
        using CancellationTokenSource second = new();
        int count = 0;

        await foreach (CurlWebSocketMessage message in socket.ReadMessagesAsync(first.Token).WithCancellation(second.Token))
        {
            count++;
        }

        Assert.AreEqual(2, count);
    }

    [TestMethod]
    public async Task ReadMessagesAsync_EnumeratorTokenCancelled_ThrowsOperationCanceled()
    {
        await using CurlWebSocket socket = Create();
        using CancellationTokenSource cancellation = new();
        await using IAsyncEnumerator<CurlWebSocketMessage> messages = socket.ReadMessagesAsync().GetAsyncEnumerator(cancellation.Token);
        await cancellation.CancelAsync();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await messages.MoveNextAsync());
    }

    [TestMethod]
    public async Task ReadMessagesAsync_CurrentBeforeFirstMessage_Throws()
    {
        await using CurlWebSocket socket = Create();
        await using IAsyncEnumerator<CurlWebSocketMessage> messages = socket.ReadMessagesAsync().GetAsyncEnumerator();

        Assert.ThrowsExactly<InvalidOperationException>(() => messages.Current);
    }

    [TestMethod]
    public async Task ReadMessagesAsync_StoppedEarly_ReceivesNoFurther()
    {
        await using CurlWebSocket socket = Create();
        Fake.Queue("a"u8.ToArray(), CurlWebSocketFlags.Text).Queue("b"u8.ToArray(), CurlWebSocketFlags.Text);
        string? first = null;

        await foreach (CurlWebSocketMessage message in socket.ReadMessagesAsync())
        {
            first = message.Text;
            break;
        }

        Assert.AreEqual("a", first);
        Assert.AreEqual(1, Fake.ReceiveCount);
    }

    [TestMethod]
    public async Task ReadMessagesAsync_ConnectionClosedWithoutCloseFrame_ThrowsCurlException()
    {
        await using CurlWebSocket socket = Create();
        Fake.QueueResult(CurlCode.RecvError);

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(async () =>
        {
            await foreach (CurlWebSocketMessage message in socket.ReadMessagesAsync())
            {
                Assert.Fail($"Unexpected message {message.Type}.");
            }
        });

        Assert.AreEqual(CurlCode.RecvError, failure.Code);
    }

    [TestMethod]
    public async Task SendTextAsync_Text_SendsOneUtf8TextFrame()
    {
        await using CurlWebSocket socket = Create();

        await socket.SendTextAsync("hé");

        Assert.HasCount(1, Fake.Sends);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("hé"), Fake.Sends[0].Data);
        Assert.AreEqual(CurlWebSocketFlags.Text, Fake.Sends[0].Flags);
        Assert.AreEqual(0L, Fake.Sends[0].FragmentSize);
    }

    [TestMethod]
    public async Task SendTextAsync_Null_Throws()
    {
        await using CurlWebSocket socket = Create();

        Assert.ThrowsExactly<ArgumentNullException>(() => _ = socket.SendTextAsync(null!));
    }

    [TestMethod]
    [DataRow(CurlWebSocketMessageType.Text, CurlWebSocketFlags.Text)]
    [DataRow(CurlWebSocketMessageType.Binary, CurlWebSocketFlags.Binary)]
    [DataRow(CurlWebSocketMessageType.Close, CurlWebSocketFlags.Close)]
    [DataRow(CurlWebSocketMessageType.Ping, CurlWebSocketFlags.Ping)]
    [DataRow(CurlWebSocketMessageType.Pong, CurlWebSocketFlags.Pong)]
    public async Task SendAsync_EachType_SendsItsBit(CurlWebSocketMessageType type, CurlWebSocketFlags expected)
    {
        await using CurlWebSocket socket = Create();

        await socket.SendAsync(new byte[] { 5 }, type);

        Assert.AreEqual(expected, Fake.Sends[0].Flags);
    }

    [TestMethod]
    public async Task SendAsync_UnknownType_Throws()
    {
        await using CurlWebSocket socket = Create();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = socket.SendAsync(new byte[] { 5 }, (CurlWebSocketMessageType)99));
    }

    [TestMethod]
    public async Task PingAsync_NoPayload_SendsOneEmptyPing()
    {
        await using CurlWebSocket socket = Create();

        await socket.PingAsync();

        Assert.HasCount(1, Fake.Sends);
        Assert.IsEmpty(Fake.Sends[0].Data);
        Assert.AreEqual(CurlWebSocketFlags.Ping, Fake.Sends[0].Flags);
    }

    [TestMethod]
    public async Task PongAsync_Payload_SendsPongWithIt()
    {
        await using CurlWebSocket socket = Create();

        await socket.PongAsync(new byte[] { 4, 2 });

        CollectionAssert.AreEqual(new byte[] { 4, 2 }, Fake.Sends[0].Data);
        Assert.AreEqual(CurlWebSocketFlags.Pong, Fake.Sends[0].Flags);
    }

    [TestMethod]
    public async Task CloseAsync_StatusAndReason_SendsBigEndianStatusThenReason()
    {
        await using CurlWebSocket socket = Create();

        await socket.CloseAsync(1001, "bye");

        CollectionAssert.AreEqual(new byte[] { 0x03, 0xE9, (byte)'b', (byte)'y', (byte)'e' }, Fake.Sends[0].Data);
        Assert.AreEqual(CurlWebSocketFlags.Close, Fake.Sends[0].Flags);
    }

    [TestMethod]
    public async Task CloseAsync_Defaults_SendsNormalClosure()
    {
        await using CurlWebSocket socket = Create();

        await socket.CloseAsync();

        CollectionAssert.AreEqual(new byte[] { 0x03, 0xE8 }, Fake.Sends[0].Data);
    }

    [TestMethod]
    public async Task CloseAsync_ReasonTooLongOrNull_Throws()
    {
        await using CurlWebSocket socket = Create();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => _ = socket.CloseAsync(1000, new string('r', CurlWebSocket.MaxCloseReasonBytes + 1)));
        Assert.ThrowsExactly<ArgumentNullException>(() => _ = socket.CloseAsync(1000, null!));
        Assert.IsEmpty(Fake.Sends);
    }

    [TestMethod]
    public async Task SendRawAsync_Bytes_SendsWithNoBits()
    {
        await using CurlWebSocket socket = Create();

        await socket.SendRawAsync(new byte[] { 0x81, 0x00 });

        Assert.AreEqual(CurlWebSocketFlags.None, Fake.Sends[0].Flags);
    }

    [TestMethod]
    public async Task SendFrameAsync_PartialSendAndAgain_SendsTheRestAfterWaiting()
    {
        await using CurlWebSocket socket = Create();
        Fake.SendResults.Enqueue(new CurlWebSocketSendResult(CurlCode.Ok, 2));
        Fake.SendResults.Enqueue(new CurlWebSocketSendResult(CurlCode.Again, 0));

        await socket.SendFrameAsync(new byte[] { 1, 2, 3, 4, 5 }, CurlWebSocketFlags.Binary);

        Assert.HasCount(3, Fake.Sends);
        CollectionAssert.AreEqual(new byte[] { 3, 4, 5 }, Fake.Sends[2].Data);
        Assert.AreEqual(CurlWebSocketFlags.Binary, Fake.Sends[2].Flags);
        Assert.AreEqual(1, _waits);
    }

    [TestMethod]
    public async Task SendFrameAsync_EngineFails_ThrowsCurlException()
    {
        await using CurlWebSocket socket = Create();
        Fake.SendResults.Enqueue(new CurlWebSocketSendResult(CurlCode.SendError, 0));

        CurlException failure = await Assert.ThrowsExactlyAsync<CurlException>(async () => await socket.SendFrameAsync(new byte[] { 1 }, CurlWebSocketFlags.Binary));

        Assert.AreEqual(CurlCode.SendError, failure.Code);
    }

    [TestMethod]
    public async Task SendFrameAsync_Cancelled_ThrowsOperationCanceledBeforeSending()
    {
        await using CurlWebSocket socket = Create();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await socket.SendFrameAsync(new byte[] { 1 }, CurlWebSocketFlags.Binary, new CancellationToken(canceled: true)));

        Assert.IsEmpty(Fake.Sends);
    }

    [TestMethod]
    public async Task CurrentFrame_GivesTheEnginesFrame()
    {
        await using CurlWebSocket socket = Create();
        CurlWebSocketFrame frame = new(0, CurlWebSocketFlags.Text, 2, 3, 4);
        Fake.CurrentFrame = frame;

        Assert.AreEqual(frame, socket.CurrentFrame);
    }

    [TestMethod]
    public async Task DisposeAsync_Twice_DisposesHandleOnceAndRefusesLaterCalls()
    {
        CurlWebSocket socket = Create();

        await socket.DisposeAsync();
        await socket.DisposeAsync();

        Assert.AreEqual(1, _transfer.DisposeCount);
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await socket.ReceiveFrameAsync(new byte[1]));
        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(async () => await socket.SendFrameAsync(new byte[1], CurlWebSocketFlags.Binary));
    }

    private CurlWebSocket Create() => new(_transfer, _ =>
    {
        _waits++;
        return Task.CompletedTask;
    });
}
