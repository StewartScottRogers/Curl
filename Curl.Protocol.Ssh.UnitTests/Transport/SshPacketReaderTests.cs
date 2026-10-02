using System.Buffers.Binary;
using System.Security.Cryptography;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.PacketProtection;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshPacketReaderTests
{
    private static readonly SshKeyDerivation ReaderKeys = new(HashAlgorithmName.SHA256, [7], [.. new byte[32]], [.. new byte[32]]);

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
    [DataRow(0x00100000u, -41, DisplayName = "1 MiB, measured: curl fails the key exchange")]
    [DataRow((uint)(SshPacketReader.MaximumPacketSize - 4 + 8), -41, DisplayName = "one block over the maximum")]
    [DataRow(uint.MaxValue, -41, DisplayName = "the largest length")]
    [DataRow(0u, -12, DisplayName = "zero length")]
    public async Task ReadAsync_UnprotectedLengthZeroOrOverTheMaximum_ThrowsWithLibssh2sCode(uint packetLength, int expectedCode)
    {
        byte[] bytes = new SshServerScript().RawPacket(packetLength, 4, new byte[64]).Bytes;
        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(bytes)));

        SshPacketLengthException failure = await Assert.ThrowsExactlyAsync<SshPacketLengthException>(
            async () => await reader.ReadAsync(CancellationToken.None));

        Assert.AreEqual(expectedCode, failure.Libssh2ErrorCode);
    }

    // Measured 2026-10-01 (BL-1081) under aes128-ctr with hmac-sha2-256: libssh2 counts the
    // MAC or tag in its 40000-byte maximum, reading a packet_length of 39964 and refusing
    // 39980 with -41.
    [TestMethod]
    [DataRow(39968u, false, DisplayName = "4 + 39968 + a 16-byte tag, under the maximum: read on")]
    [DataRow(39984u, true, DisplayName = "4 + 39984 + a 16-byte tag, over the maximum: -41")]
    public async Task ReadAsync_LengthNearTheMaximum_CountsTheTag(uint packetLength, bool refused)
    {
        byte[] bytes = [.. SshTestEncoding.UInt32(packetLength), .. new byte[128]];
        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(bytes)));
        reader.ChangeProtection(SshPacketProtections.ForServerToClient(SshTestAlgorithms.With("aes128-gcm@openssh.com", null), ReaderKeys));

        Exception failure = await Assert.ThrowsAsync<Exception>(async () => await reader.ReadAsync(CancellationToken.None));

        Assert.AreEqual(refused ? typeof(SshPacketLengthException) : typeof(EndOfStreamException), failure.GetType());
    }

    [TestMethod]
    [DataRow("aes128-ctr", "hmac-sha2-256", DisplayName = "aes128-ctr, as measured")]
    [DataRow("aes128-gcm@openssh.com", null, DisplayName = "AES-GCM, as measured")]
    [DataRow("chacha20-poly1305@openssh.com", null, DisplayName = "ChaCha20-Poly1305, as measured")]
    public async Task ReadAsync_ProtectedLengthOverTheMaximum_ThrowsWithMinus41(string cipher, string? mac)
    {
        byte[] bytes = new SshServerScript()
            .Protect(SshPacketProtections.ForServerToClient(SshTestAlgorithms.With(cipher, mac), ReaderKeys), resetSequenceNumber: true)
            .Packet([6, 1, 2, 3], sealedPacket => sealedPacket[0] ^= 0x80)
            .Bytes;
        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(bytes)));
        reader.ChangeProtection(SshPacketProtections.ForServerToClient(SshTestAlgorithms.With(cipher, mac), ReaderKeys));

        SshPacketLengthException failure = await Assert.ThrowsExactlyAsync<SshPacketLengthException>(
            async () => await reader.ReadAsync(CancellationToken.None));

        Assert.AreEqual(-41, failure.Libssh2ErrorCode);
    }

    [TestMethod]
    [DataRow("aes128-ctr", "hmac-sha2-256", -4, DisplayName = "MAC-then-encrypt")]
    [DataRow("aes128-ctr", "hmac-sha2-256-etm@openssh.com", -4, DisplayName = "encrypt-then-MAC")]
    [DataRow("3des-cbc", "hmac-sha1-96", -4, DisplayName = "3des-cbc, hmac-sha1-96")]
    [DataRow("aes128-ctr", "hmac-md5-96", -4, DisplayName = "hmac-md5-96")]
    [DataRow("arcfour128", "hmac-ripemd160", -4, DisplayName = "arcfour128, hmac-ripemd160")]
    [DataRow("blowfish-cbc", "hmac-sha1-etm@openssh.com", -4, DisplayName = "blowfish-cbc, hmac-sha1-etm")]
    [DataRow("aes128-ctr", "hmac-md5-etm@openssh.com", -4, DisplayName = "hmac-md5-etm")]
    [DataRow("aes128-gcm@openssh.com", null, -12, DisplayName = "AES-GCM")]
    [DataRow("chacha20-poly1305@openssh.com", null, -12, DisplayName = "ChaCha20-Poly1305")]
    public async Task ReadAsync_ProtectedPacketAltered_ThrowsWithLibssh2sCode(string cipher, string? mac, int expectedCode)
    {
        byte[] bytes = new SshServerScript()
            .Protect(SshPacketProtections.ForServerToClient(SshTestAlgorithms.With(cipher, mac), ReaderKeys), resetSequenceNumber: true)
            .Packet([6, 1, 2, 3], sealedPacket => sealedPacket[^1] ^= 1)
            .Bytes;
        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(bytes)));
        reader.ChangeProtection(SshPacketProtections.ForServerToClient(SshTestAlgorithms.With(cipher, mac), ReaderKeys));

        SshPacketAuthenticationException failure = await Assert.ThrowsExactlyAsync<SshPacketAuthenticationException>(
            async () => await reader.ReadAsync(CancellationToken.None));

        Assert.AreEqual(expectedCode, failure.Libssh2ErrorCode);
        Assert.AreEqual(0u, reader.SequenceNumber, "a packet that fails its check is not counted");
    }

    [TestMethod]
    [DataRow(20u, DisplayName = "not a multiple of 16 without the length field")]
    [DataRow(28u, DisplayName = "a multiple of 16 only with the length field")]
    public async Task ReadAsync_EncryptThenMacFraming_RefusesLengthsOffTheBlockSize(uint packetLength)
    {
        byte[] bytes = [.. SshTestEncoding.UInt32(packetLength), .. new byte[128]];
        SshPacketReader reader = new(new SshConnectionReader(new ScriptedConnection(bytes)));
        reader.ChangeProtection(SshPacketProtections.ForServerToClient(SshTestAlgorithms.With("aes128-gcm@openssh.com", null), ReaderKeys));

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
