using System.Buffers.Binary;
using Curl.Protocol.Ssh.Fakes;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshPacketReaderTests
{
    [TestMethod]
    public async Task ReadAsync_ReturnsThePayloadOfEachPacketAndCountsSequenceNumbers()
    {
        byte[] bytes = new SshServerScript().Packet(2, 1, 2, 3).Packet(4).Bytes;
        SshPacketReader reader = new(new SshConnectionReader(ScriptedConnection.InChunks(bytes, 3)));

        CollectionAssert.AreEqual(new byte[] { 2, 1, 2, 3 }, await reader.ReadAsync(CancellationToken.None));
        Assert.AreEqual(1u, reader.SequenceNumber);
        CollectionAssert.AreEqual(new byte[] { 4 }, await reader.ReadAsync(CancellationToken.None));
        Assert.AreEqual(2u, reader.SequenceNumber);
    }

    [TestMethod]
    public async Task ReadAsync_ReadsWhatTheWriterWrote()
    {
        ScriptedConnection written = new();
        byte[] payload = [20, .. Enumerable.Range(0, 300).Select(value => (byte)value)];
        await new SshPacketWriter(written, new RepeatingRandomSource(1)).WriteAsync(payload, CancellationToken.None);

        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(written.Written)));

        CollectionAssert.AreEqual(payload, await reader.ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_PacketOfTheMaximumSize_IsRead()
    {
        byte[] payload = new byte[SshPacketReader.MaximumPacketSize - 4 - 1 - 11];
        payload[0] = 2;
        byte[] bytes = new SshServerScript().Packet(payload).Bytes;
        Assert.AreEqual(SshPacketReader.MaximumPacketSize, bytes.Length);
        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(bytes)));

        Assert.AreEqual(payload.Length, (await reader.ReadAsync(CancellationToken.None)).Length);
    }

    [TestMethod]
    [DataRow(0x00100000u, (byte)4, DisplayName = "1 MiB, measured: curl fails the key exchange")]
    [DataRow((uint)(SshPacketReader.MaximumPacketSize + 4), (byte)4, DisplayName = "one block over the maximum")]
    [DataRow(0u, (byte)0, DisplayName = "zero length")]
    [DataRow(13u, (byte)4, DisplayName = "not a multiple of the block size")]
    [DataRow(12u, (byte)2, DisplayName = "padding under four, measured: curl fails the key exchange")]
    [DataRow(12u, (byte)11, DisplayName = "no payload")]
    [DataRow(12u, (byte)200, DisplayName = "padding longer than the packet")]
    public async Task ReadAsync_BrokenFraming_ThrowsInvalidData(uint packetLength, byte paddingLength)
    {
        byte[] bytes = new SshServerScript().RawPacket(packetLength, paddingLength, new byte[64]).Bytes;
        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(bytes)));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(async () => await reader.ReadAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task ReadAsync_PeerClosesInsideThePacket_ThrowsEndOfStream()
    {
        byte[] bytes = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, 12);
        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(bytes)));

        await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await reader.ReadAsync(CancellationToken.None));
    }
}
