using System.Buffers.Binary;
using System.Net;
using Curl.Protocol.Abstractions;
using Curl.Ssh.Fakes;
using Curl.Ssh.Negotiation;
using Curl.Ssh.Transport;
using static Curl.Ssh.Fakes.SshTestEncoding;

namespace Curl.Ssh.Connection;

/// <summary>
/// Pins <see cref="SshDirectTcpipChannel" /> against a scripted peer, byte for byte, by the
/// sections of RFC 4254 each test names: 7.2 (opening a <c>direct-tcpip</c> channel), 5.1
/// (the open's answer), 5.2 (data within the peer's window and maximum packet size) and 5.3
/// (EOF and close).
/// </summary>
[TestClass]
public sealed class SshDirectTcpipChannelTests
{
    private const uint ServerChannel = 7;

    private static readonly byte[] ServerChannelBytes = UInt32(ServerChannel);

    [TestMethod]
    public async Task OpenAsync_Rfc4254Section7_2_SendsTheTargetAndOriginatorAfterTheChannelFields()
    {
        Peer peer = Connect(new SshServerScript().Packet(Confirmation(window: 100, maximumPacketSize: 100)));

        bool opened = await peer.Channel.OpenAsync("127.0.0.1", 50000, CancellationToken.None);

        Assert.IsTrue(opened);
        List<byte[]> written = Payloads(peer);
        Assert.HasCount(1, written);
        CollectionAssert.AreEqual(
            Join([SshConnectionMessageNumber.ChannelOpen], Name("direct-tcpip"), UInt32(0), UInt32(2097152), UInt32(32768), Name("target.example"), UInt32(443), Name("127.0.0.1"), UInt32(50000)),
            written[0]);
    }

    [TestMethod]
    public async Task OpenAsync_Rfc4254Section5_1_ServerRefuses_ReturnsFalseAndKeepsTheReasonCode()
    {
        Peer peer = Connect(new SshServerScript().Packet(Join([SshConnectionMessageNumber.ChannelOpenFailure], UInt32(0), UInt32(2), Name("connect failed"), Name(string.Empty))));

        bool opened = await peer.Channel.OpenAsync("127.0.0.1", 50000, CancellationToken.None);

        Assert.IsFalse(opened);
        Assert.AreEqual(2u, peer.Channel.OpenFailureReasonCode);
    }

    [TestMethod]
    public async Task WriteAsync_Rfc4254Section5_2_SplitsByThePeersMaximumPacketSizeAndWaitsForItsWindow()
    {
        Peer peer = Connect(new SshServerScript()
            .Packet(Confirmation(window: 6, maximumPacketSize: 4))
            .Packet(Join([SshConnectionMessageNumber.ChannelWindowAdjust], UInt32(0), UInt32(100))));
        byte[] data = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];
        await peer.Channel.OpenAsync("127.0.0.1", 50000, CancellationToken.None);

        await peer.Channel.WriteAsync(data, CancellationToken.None);
        await peer.Channel.FlushAsync(CancellationToken.None);

        List<byte[]> written = Payloads(peer);
        Assert.HasCount(4, written, "4 bytes, then the 2 left in the window, then after the adjustment 4 more");
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelData], ServerChannelBytes, String(data[..4])), written[1]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelData], ServerChannelBytes, String(data[4..6])), written[2]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelData], ServerChannelBytes, String(data[6..])), written[3]);
    }

    [TestMethod]
    public async Task ReadAsync_Rfc4254Section5_2And5_3_ReadsTheDataThenZeroAtEof()
    {
        Peer peer = Connect(new SshServerScript()
            .Packet(Confirmation(window: 100, maximumPacketSize: 100))
            .Packet(Join([SshConnectionMessageNumber.ChannelData], UInt32(0), String([5, 6, 7])))
            .Packet([SshConnectionMessageNumber.ChannelEof, .. UInt32(0)]));
        await peer.Channel.OpenAsync("127.0.0.1", 50000, CancellationToken.None);
        byte[] buffer = new byte[8];

        int read = await peer.Channel.ReadAsync(buffer, CancellationToken.None);
        int atEof = await peer.Channel.ReadAsync(buffer, CancellationToken.None);

        Assert.AreEqual(3, read);
        CollectionAssert.AreEqual(new byte[] { 5, 6, 7 }, buffer[..3]);
        Assert.AreEqual(0, atEof);
    }

    [TestMethod]
    public async Task DisposeAsync_Rfc4254Section5_3_SendsEofAndCloseThenWaitsForTheServersCloseOnce()
    {
        Peer peer = Connect(new SshServerScript()
            .Packet(Confirmation(window: 100, maximumPacketSize: 100))
            .Packet([SshConnectionMessageNumber.ChannelEof, .. UInt32(0)])
            .Packet([SshConnectionMessageNumber.ChannelClose, .. UInt32(0)]));
        await peer.Channel.OpenAsync("127.0.0.1", 50000, CancellationToken.None);

        await peer.Channel.DisposeAsync();
        await peer.Channel.DisposeAsync();

        List<byte[]> written = Payloads(peer);
        Assert.HasCount(3, written);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelEof], ServerChannelBytes), written[1]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelClose], ServerChannelBytes), written[2]);
    }

    [TestMethod]
    public async Task DisposeAsync_ServerNeverClosesFirst_SendsEofAndCloseTogetherAndEndsOnTheServersClose()
    {
        Peer peer = Connect(new SshServerScript()
            .Packet(Confirmation(window: 100, maximumPacketSize: 100))
            .Packet([SshConnectionMessageNumber.ChannelClose, .. UInt32(0)]));
        await peer.Channel.OpenAsync("127.0.0.1", 50000, CancellationToken.None);

        await peer.Channel.DisposeAsync();

        List<byte[]> written = Payloads(peer);
        Assert.HasCount(3, written);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelEof], ServerChannelBytes), written[1]);
        CollectionAssert.AreEqual(Join([SshConnectionMessageNumber.ChannelClose], ServerChannelBytes), written[2]);
    }

    [TestMethod]
    public async Task WriteAsync_Rfc4254Section5_3_PeerSentEofWhileWindowIsUsedUp_ThrowsEndOfStream()
    {
        Peer peer = Connect(new SshServerScript()
            .Packet(Confirmation(window: 2, maximumPacketSize: 100))
            .Packet([SshConnectionMessageNumber.ChannelEof, .. UInt32(0)]));
        await peer.Channel.OpenAsync("127.0.0.1", 50000, CancellationToken.None);

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await peer.Channel.WriteAsync(new byte[] { 1, 2, 3, 4 }, CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsyncAndWriteAsync_Concurrent_NeverRunTwoPacketReadsOrWritesAtOnce()
    {
        SshServerScript script = new SshServerScript()
            .Packet(Confirmation(window: 4, maximumPacketSize: 100))
            .Packet(Join([SshConnectionMessageNumber.ChannelWindowAdjust], UInt32(0), UInt32(100)))
            .Packet(Join([SshConnectionMessageNumber.ChannelData], UInt32(0), String([9])));
        OverlapProbeConnection connection = new(new ScriptedConnection(script.Bytes));
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, SshAlgorithmCatalogue.Implemented, new RepeatingRandomSource(0x33), new TestEphemeralKeys());
        SshDirectTcpipChannel channel = new(transport, "target.example", 443);
        await channel.OpenAsync("127.0.0.1", 50000, CancellationToken.None);
        byte[] buffer = new byte[8];

        Task<int> read = channel.ReadAsync(buffer, CancellationToken.None).AsTask();
        Task write = channel.WriteAsync(new byte[10], CancellationToken.None).AsTask();
        await Task.WhenAll(read, write);

        Assert.AreEqual(1, read.Result);
        Assert.AreEqual(1, connection.MaximumConcurrentReads);
        Assert.AreEqual(1, connection.MaximumConcurrentWrites);
    }

    [TestMethod]
    public void Properties_NewChannel_AreSecureAndNameTheTarget()
    {
        Peer peer = Connect(new SshServerScript());

        Assert.IsTrue(peer.Channel.IsSecure);
        Assert.AreEqual(new DnsEndPoint("target.example", 443), peer.Channel.RemoteEndPoint);
    }

    private static byte[] Confirmation(uint window, uint maximumPacketSize) =>
        Join([SshConnectionMessageNumber.ChannelOpenConfirmation], UInt32(0), ServerChannelBytes, UInt32(window), UInt32(maximumPacketSize));

    private static Peer Connect(SshServerScript script)
    {
        ScriptedConnection connection = new(script.Bytes);
        SshTransport transport = new(connection, SshAlgorithmPreferences.Full, SshAlgorithmCatalogue.Implemented, new RepeatingRandomSource(0x33), new TestEphemeralKeys());
        return new Peer(new SshDirectTcpipChannel(transport, "target.example", 443), connection);
    }

    // The payloads of the client's unencrypted packets: no identification line is written
    // before the transport's first packet here.
    private static List<byte[]> Payloads(Peer peer)
    {
        byte[] written = peer.Connection.Written;
        List<byte[]> payloads = [];
        int position = 0;
        while (position < written.Length)
        {
            int packetLength = (int)BinaryPrimitives.ReadUInt32BigEndian(written.AsSpan(position));
            int paddingLength = written[position + 4];
            payloads.Add(written.AsSpan(position + 5, packetLength - 1 - paddingLength).ToArray());
            position += 4 + packetLength;
        }

        return payloads;
    }

    private sealed record Peer(SshDirectTcpipChannel Channel, ScriptedConnection Connection);

    // Yields inside every read and write so that two of them running at once overlap, and records the most seen.
    private sealed class OverlapProbeConnection(ScriptedConnection inner) : IConnection
    {
        private int reads;

        private int writes;

        public int MaximumConcurrentReads { get; private set; }

        public int MaximumConcurrentWrites { get; private set; }

        public bool IsSecure => false;

        public EndPoint? RemoteEndPoint => null;

        public async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            MaximumConcurrentReads = Math.Max(MaximumConcurrentReads, Interlocked.Increment(ref reads));
            await Task.Yield();
            try
            {
                return await inner.ReadAsync(buffer, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref reads);
            }
        }

        public async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken)
        {
            MaximumConcurrentWrites = Math.Max(MaximumConcurrentWrites, Interlocked.Increment(ref writes));
            await Task.Yield();
            try
            {
                await inner.WriteAsync(buffer, cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref writes);
            }
        }

        public ValueTask FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }
}