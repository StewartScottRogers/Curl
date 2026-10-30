using System.Net;
using System.Net.Sockets;
using System.Text;
using Curl.Extension.WireGuard.IpStack;

namespace Curl.Extension.WireGuard.UnitTests.IpStack;

[TestClass]
public sealed class TcpConnectionTests
{
    private const uint ClientNext = ScriptedTcpPeer.ClientInitialSequenceNumber + 1;
    private const uint ServerNext = ScriptedTcpPeer.ServerInitialSequenceNumber + 1;
    private const TcpFlags PushAcknowledgement = TcpFlags.Psh | TcpFlags.Ack;

    [TestMethod]
    public async Task ConnectAsync_SynAcknowledged_SendsSynWithMaximumSegmentSizeThenAck()
    {
        ScriptedTcpPeer peer = new();
        var connecting = TcpConnection.ConnectAsync(peer.Channel, peer.Parameters, peer.Clock, CancellationToken.None);

        var synchronize = await peer.ReceiveAsync();
        peer.Send(TcpFlags.Syn | TcpFlags.Ack, ScriptedTcpPeer.ServerInitialSequenceNumber, ClientNext, maximumSegmentSize: 1400);
        var acknowledgement = await peer.ReceiveAsync();
        var connection = await connecting;

        Assert.AreEqual(TcpFlags.Syn, synchronize.Flags);
        Assert.AreEqual(ScriptedTcpPeer.ClientInitialSequenceNumber, synchronize.SequenceNumber);
        Assert.AreEqual(ScriptedTcpPeer.ClientMaximumSegmentSize, synchronize.MaximumSegmentSize);
        Assert.AreEqual((ushort)TcpConnection.ReceiveBufferCapacity, synchronize.Window);
        Assert.AreEqual((ushort)50000, synchronize.SourcePort);
        Assert.AreEqual((ushort)80, synchronize.DestinationPort);
        Assert.AreEqual(TcpFlags.Ack, acknowledgement.Flags);
        Assert.AreEqual(ClientNext, acknowledgement.SequenceNumber);
        Assert.AreEqual(ServerNext, acknowledgement.AcknowledgementNumber);
        Assert.IsNull(acknowledgement.MaximumSegmentSize);
    }

    [TestMethod]
    public async Task ConnectAsync_IPv6_ExchangesIPv6Packets()
    {
        ScriptedTcpPeer peer = new(new IPEndPoint(IPAddress.Parse("fd00::2"), 50001), new IPEndPoint(IPAddress.Parse("fd00::1"), 443));

        var connection = await peer.ConnectAsync();
        await connection.WriteAsync("hi"u8.ToArray(), CancellationToken.None);
        var data = await peer.ReceiveAsync();

        CollectionAssert.AreEqual("hi"u8.ToArray(), data.Payload);
        Assert.AreEqual(PushAcknowledgement, data.Flags);
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 2);
        await CloseAsync(peer, connection, ClientNext + 2);
    }

    [TestMethod]
    public async Task ConnectAsync_UnacceptableSegmentsBeforeTheSynAck_IgnoresThem()
    {
        ScriptedTcpPeer peer = new();
        var connecting = TcpConnection.ConnectAsync(peer.Channel, peer.Parameters, peer.Clock, CancellationToken.None);
        await peer.ReceiveAsync();

        peer.Send(TcpFlags.Ack, ScriptedTcpPeer.ServerInitialSequenceNumber, ClientNext);
        peer.Send(TcpFlags.Syn | TcpFlags.Ack, ScriptedTcpPeer.ServerInitialSequenceNumber, ClientNext + 5);
        peer.Send(TcpFlags.Rst, 0, 0);
        peer.Send(TcpFlags.Rst | TcpFlags.Ack, 0, ClientNext + 1);
        peer.Send(TcpFlags.Syn | TcpFlags.Ack, ScriptedTcpPeer.ServerInitialSequenceNumber, ClientNext);
        var acknowledgement = await peer.ReceiveAsync();
        var connection = await connecting;

        Assert.AreEqual(ServerNext, acknowledgement.AcknowledgementNumber);
    }

    [TestMethod]
    public async Task ConnectAsync_ResetAcknowledgingTheSyn_ThrowsConnectionReset()
    {
        ScriptedTcpPeer peer = new();
        var connecting = TcpConnection.ConnectAsync(peer.Channel, peer.Parameters, peer.Clock, CancellationToken.None);
        await peer.ReceiveAsync();

        peer.Send(TcpFlags.Rst | TcpFlags.Ack, 0, ClientNext);
        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => connecting);

        Assert.AreEqual(SocketError.ConnectionReset, ((SocketException)exception.InnerException!).SocketErrorCode);
    }

    [TestMethod]
    public async Task ConnectAsync_SynNeverAnswered_RetransmitsSixTimesWithBackOffThenTimesOut()
    {
        ScriptedTcpPeer peer = new();
        var connecting = TcpConnection.ConnectAsync(peer.Channel, peer.Parameters, peer.Clock, CancellationToken.None);
        await peer.ReceiveAsync();

        peer.Clock.Advance(TimeSpan.FromMilliseconds(999));
        foreach (var seconds in new[] { 0.001, 2, 4, 8, 16, 32 })
        {
            peer.Clock.Advance(TimeSpan.FromSeconds(seconds));
            var retransmitted = await peer.ReceiveAsync();
            Assert.AreEqual(TcpFlags.Syn, retransmitted.Flags);
            Assert.AreEqual(ScriptedTcpPeer.ClientInitialSequenceNumber, retransmitted.SequenceNumber);
        }

        peer.Clock.Advance(TimeSpan.FromSeconds(60));
        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => connecting);

        Assert.AreEqual(SocketError.TimedOut, ((SocketException)exception.InnerException!).SocketErrorCode);
    }

    [TestMethod]
    public async Task ConnectAsync_Cancelled_Throws()
    {
        ScriptedTcpPeer peer = new();
        using CancellationTokenSource cancellation = new();
        var connecting = TcpConnection.ConnectAsync(peer.Channel, peer.Parameters, peer.Clock, cancellation.Token);
        await peer.ReceiveAsync();

        await cancellation.CancelAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => connecting);
    }

    [TestMethod]
    public async Task ConnectAsync_NullArgument_Throws()
    {
        ScriptedTcpPeer peer = new();

        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => TcpConnection.ConnectAsync(null!, peer.Parameters, peer.Clock, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => TcpConnection.ConnectAsync(peer.Channel, null!, peer.Clock, CancellationToken.None));
        await Assert.ThrowsExactlyAsync<ArgumentNullException>(() => TcpConnection.ConnectAsync(peer.Channel, peer.Parameters, null!, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_DataAcrossSegments_GivesItInOrderAndAcknowledgesEach()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        peer.Send(PushAcknowledgement, ServerNext, ClientNext, "hel"u8.ToArray());
        var first = await peer.ReceiveAsync();
        peer.Send(PushAcknowledgement, ServerNext + 3, ClientNext, "lo"u8.ToArray());
        var second = await peer.ReceiveAsync();
        var text = await ReadAllAsync(connection, 5);

        Assert.AreEqual("hello", text);
        Assert.AreEqual(ServerNext + 3, first.AcknowledgementNumber);
        Assert.AreEqual((ushort)(TcpConnection.ReceiveBufferCapacity - 3), first.Window);
        Assert.AreEqual(ServerNext + 5, second.AcknowledgementNumber);
    }

    [TestMethod]
    public async Task ReadAsync_SegmentOutOfOrder_DropsItAndAcknowledgesWhatCameInOrder()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        peer.Send(PushAcknowledgement, ServerNext + 3, ClientNext, "lo"u8.ToArray());
        var duplicate = await peer.ReceiveAsync();
        peer.Send(PushAcknowledgement, ServerNext, ClientNext, "hel"u8.ToArray());
        await peer.ReceiveAsync();
        peer.Send(PushAcknowledgement, ServerNext + 3, ClientNext, "lo"u8.ToArray());
        await peer.ReceiveAsync();

        Assert.AreEqual(ServerNext, duplicate.AcknowledgementNumber);
        Assert.AreEqual("hello", await ReadAllAsync(connection, 5));
    }

    [TestMethod]
    public async Task ReadAsync_PeerSendsFin_GivesTheDataThenZeroAndAcknowledgesTheFin()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        peer.Send(TcpFlags.Fin | PushAcknowledgement, ServerNext, ClientNext, "bye"u8.ToArray());
        var acknowledgement = await peer.ReceiveAsync();
        var text = await ReadAllAsync(connection, 3);
        var end = await connection.ReadAsync(new byte[4], CancellationToken.None);

        Assert.AreEqual("bye", text);
        Assert.AreEqual(0, end);
        Assert.AreEqual(ServerNext + 4, acknowledgement.AcknowledgementNumber);
        await CloseAsync(peer, connection, ClientNext, ServerNext + 4);
    }

    [TestMethod]
    public async Task ReadAsync_EmptyBuffer_GivesZeroAtOnce()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        var count = await connection.ReadAsync(Memory<byte>.Empty, CancellationToken.None);

        Assert.AreEqual(0, count);
    }

    [TestMethod]
    public async Task ReadAsync_PeerResets_ThrowsConnectionResetAndIgnoresLaterSegments()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();
        var reading = connection.ReadAsync(new byte[8], CancellationToken.None).AsTask();

        peer.Send(TcpFlags.Rst, ServerNext + 9, 0);
        peer.Send(TcpFlags.Rst, ServerNext, 0);
        peer.Send(PushAcknowledgement, ServerNext, ClientNext, "late"u8.ToArray());
        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => reading);

        Assert.AreEqual(SocketError.ConnectionReset, ((SocketException)exception.InnerException!).SocketErrorCode);
        await Assert.ThrowsExactlyAsync<IOException>(() => connection.WriteAsync("x"u8.ToArray(), CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_ChannelFails_ThrowsIOException()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        peer.Channel.Close(new InvalidOperationException("tunnel down"));
        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[1], CancellationToken.None).AsTask());

        Assert.AreEqual("tunnel down", exception.Message);
    }

    [TestMethod]
    [DataRow("not IP")]
    [DataRow("not TCP")]
    [DataRow("another source")]
    [DataRow("another destination")]
    [DataRow("wrong TCP checksum")]
    [DataRow("another source port")]
    [DataRow("another destination port")]
    public async Task ReadAsync_MalformedOrMisaddressedPacket_DropsIt(string kind)
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        peer.Channel.Deliver(Unwanted(peer, kind));
        peer.Send(PushAcknowledgement, ServerNext, ClientNext, "ok"u8.ToArray());
        var acknowledgement = await peer.ReceiveAsync();

        Assert.AreEqual(ServerNext + 2, acknowledgement.AcknowledgementNumber);
        Assert.AreEqual("ok", await ReadAllAsync(connection, 2));
    }

    [TestMethod]
    public async Task ReadAsync_ReceiveBufferFills_TakesOnlyWhatFitsThenUpdatesTheWindowOnceRead()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        peer.Send(PushAcknowledgement, ServerNext, ClientNext, new byte[40000]);
        await peer.ReceiveAsync();
        peer.Send(TcpFlags.Fin | PushAcknowledgement, ServerNext + 40000, ClientNext, new byte[30000]);
        var full = await peer.ReceiveAsync();
        await connection.ReadAsync(new byte[1000], CancellationToken.None);
        await connection.ReadAsync(new byte[39000], CancellationToken.None);
        var update = await peer.ReceiveAsync();

        Assert.AreEqual(ServerNext + (uint)TcpConnection.ReceiveBufferCapacity, full.AcknowledgementNumber);
        Assert.AreEqual((ushort)0, full.Window);
        Assert.AreEqual((ushort)40000, update.Window);
        Assert.AreEqual(full.AcknowledgementNumber, update.AcknowledgementNumber);
    }

    [TestMethod]
    public async Task WriteAsync_LongerThanTheMaximumSegmentSize_SplitsIntoSegments()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync(maximumSegmentSize: null);

        await connection.WriteAsync(new byte[1000], CancellationToken.None);
        var first = await peer.ReceiveAsync();
        var second = await peer.ReceiveAsync();

        Assert.HasCount(TcpConnection.DefaultMaximumSegmentSize, first.Payload);
        Assert.HasCount(1000 - TcpConnection.DefaultMaximumSegmentSize, second.Payload);
        Assert.AreEqual(ClientNext + TcpConnection.DefaultMaximumSegmentSize, second.SequenceNumber);
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 1000);
        await CloseAsync(peer, connection, ClientNext + 1000);
    }

    [TestMethod]
    public async Task WriteAsync_PeerWindowFull_WaitsForTheAcknowledgement()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync(window: 4);

        var writing = connection.WriteAsync("abcdefgh"u8.ToArray(), CancellationToken.None);
        var first = await peer.ReceiveAsync();
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 4, window: 4);
        var second = await peer.ReceiveAsync();
        await writing;

        Assert.AreEqual("abcd", Encoding.ASCII.GetString(first.Payload));
        Assert.AreEqual("efgh", Encoding.ASCII.GetString(second.Payload));
        Assert.AreEqual(ClientNext + 4, second.SequenceNumber);
    }

    [TestMethod]
    public async Task WriteAsync_SegmentLost_RetransmitsItAfterTheTimeoutAndAgainAfterTwice()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        await connection.WriteAsync("data"u8.ToArray(), CancellationToken.None);
        var original = await peer.ReceiveAsync();
        peer.Clock.Advance(TimeSpan.FromSeconds(1));
        var first = await peer.ReceiveAsync();
        peer.Clock.Advance(TimeSpan.FromSeconds(2));
        var second = await peer.ReceiveAsync();
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 4);

        Assert.AreEqual(original, first with { Payload = original.Payload });
        CollectionAssert.AreEqual(original.Payload, second.Payload);
        Assert.AreEqual(ClientNext, second.SequenceNumber);
        await CloseAsync(peer, connection, ClientNext + 4);
    }

    [TestMethod]
    public async Task WriteAsync_PartlyAcknowledged_KeepsTheRestOutstandingAndRetransmitsIt()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();
        peer.Clock.Advance(TimeSpan.FromSeconds(3));

        await connection.WriteAsync("abcd"u8.ToArray(), CancellationToken.None);
        await peer.ReceiveAsync();
        await connection.WriteAsync("efgh"u8.ToArray(), CancellationToken.None);
        await peer.ReceiveAsync();
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 2);
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 1);
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 99);
        peer.Send(TcpFlags.None, ServerNext, ClientNext + 8);
        peer.Send(TcpFlags.Ack, ServerNext, ClientNext + 2);
        peer.Send(PushAcknowledgement, ServerNext, ClientNext + 2, "z"u8.ToArray());
        await peer.ReceiveAsync();
        peer.Clock.Advance(TimeSpan.FromSeconds(1));
        var retransmitted = await peer.ReceiveAsync();

        Assert.AreEqual("abcd", Encoding.ASCII.GetString(retransmitted.Payload));
        Assert.AreEqual(ServerNext + 1, retransmitted.AcknowledgementNumber);
        peer.Send(TcpFlags.Ack, ServerNext + 1, ClientNext + 8);
        await CloseAsync(peer, connection, ClientNext + 8, ServerNext + 1);
    }

    [TestMethod]
    public async Task WriteAsync_NeverAcknowledged_TimesOutAfterFifteenRetransmissions()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();
        await connection.WriteAsync("x"u8.ToArray(), CancellationToken.None);
        await peer.ReceiveAsync();

        for (var index = 0; index < TcpConnection.MaximumDataRetransmissions; index++)
        {
            peer.Clock.Advance(TimeSpan.FromSeconds(60));
            await peer.ReceiveAsync();
        }

        peer.Clock.Advance(TimeSpan.FromSeconds(60));
        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[1], CancellationToken.None).AsTask());

        Assert.AreEqual(SocketError.TimedOut, ((SocketException)exception.InnerException!).SocketErrorCode);
    }

    [TestMethod]
    public async Task WriteAsync_RetransmissionSendFails_FailsTheConnection()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();
        await connection.WriteAsync("x"u8.ToArray(), CancellationToken.None);
        await peer.ReceiveAsync();

        peer.Channel.FailingSendNumber = 4;
        peer.Clock.Advance(TimeSpan.FromSeconds(1));
        var exception = await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[1], CancellationToken.None).AsTask());

        Assert.AreEqual("The scripted send failed.", exception.Message);
    }

    [TestMethod]
    public async Task RetransmissionTimer_FiresWithNothingOutstanding_SendsNothing()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        peer.Clock.FireEveryTimer();
        peer.Send(PushAcknowledgement, ServerNext, ClientNext, "a"u8.ToArray());
        var next = await peer.ReceiveAsync();

        Assert.AreEqual(TcpFlags.Ack, next.Flags);
        Assert.AreEqual(ServerNext + 1, next.AcknowledgementNumber);
    }

    [TestMethod]
    public async Task DisposeAsync_Established_SendsFinAndWaitsForItsAcknowledgement()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();

        await CloseAsync(peer, connection, ClientNext);
        await connection.DisposeAsync();

        await Assert.ThrowsExactlyAsync<ObjectDisposedException>(() => connection.WriteAsync("x"u8.ToArray(), CancellationToken.None));
    }

    [TestMethod]
    public async Task DisposeAsync_AfterReset_SendsNothing()
    {
        ScriptedTcpPeer peer = new();
        var connection = await peer.ConnectAsync();
        peer.Send(TcpFlags.Rst, ServerNext, 0);
        await Assert.ThrowsExactlyAsync<IOException>(() => connection.ReadAsync(new byte[1], CancellationToken.None).AsTask());
        var sendsBefore = peer.Channel.SendCount;

        await connection.DisposeAsync();

        Assert.AreEqual(sendsBefore, peer.Channel.SendCount);
    }

    private static async Task CloseAsync(ScriptedTcpPeer peer, TcpConnection connection, uint finishSequence, uint acknowledgement = ServerNext)
    {
        var closing = connection.DisposeAsync().AsTask();
        var finish = await peer.ReceiveAsync();
        Assert.AreEqual(TcpFlags.Fin | TcpFlags.Ack, finish.Flags);
        Assert.AreEqual(finishSequence, finish.SequenceNumber);
        Assert.AreEqual(acknowledgement, finish.AcknowledgementNumber);
        peer.Send(TcpFlags.Ack, acknowledgement, finishSequence + 1);
        await closing;
    }

    private static async Task<string> ReadAllAsync(TcpConnection connection, int length)
    {
        List<byte> bytes = [];
        var buffer = new byte[2];
        while (bytes.Count < length)
        {
            var count = await connection.ReadAsync(buffer, CancellationToken.None);
            bytes.AddRange(buffer.AsSpan(0, count));
        }

        return Encoding.ASCII.GetString([.. bytes]);
    }

    private static byte[] Unwanted(ScriptedTcpPeer peer, string kind)
    {
        var segment = peer.Segment(PushAcknowledgement, ServerNext, ClientNext, "no"u8.ToArray());
        var server = peer.Server.Address;
        var client = peer.Client.Address;
        var other = IPAddress.Parse("10.0.0.9");
        return kind switch
        {
            "not IP" => [0x45, 0x00],
            "not TCP" => IpPacketCodec.Build(new IpPacket(server, client, 17, TcpSegmentCodec.Build(segment, server, client))),
            "another source" => IpPacketCodec.Build(new IpPacket(other, client, 6, TcpSegmentCodec.Build(segment, other, client))),
            "another destination" => IpPacketCodec.Build(new IpPacket(server, other, 6, TcpSegmentCodec.Build(segment, server, other))),
            "wrong TCP checksum" => IpPacketCodec.Build(new IpPacket(server, client, 6, TcpSegmentCodec.Build(segment, server, other))),
            "another source port" => peer.Packet(segment with { SourcePort = 81 }),
            _ => peer.Packet(segment with { DestinationPort = 50001 }),
        };
    }
}
