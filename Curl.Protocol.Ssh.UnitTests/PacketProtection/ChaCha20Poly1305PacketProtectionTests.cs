using Curl.Cryptography;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class ChaCha20Poly1305PacketProtectionTests
{
    /// <summary>The 64-byte derived key: K_2 is bytes 1 to 32, K_1 bytes 33 to 64.</summary>
    private static readonly byte[] Key = [.. Enumerable.Range(1, 64).Select(value => (byte)value)];

    /// <summary>A 32-byte packet: length 28, padding length 10, payload 17 bytes, padding 10 bytes.</summary>
    private static readonly byte[] Packet = [0, 0, 0, 28, 10, .. Enumerable.Range(1, 17).Select(value => (byte)value), .. Enumerable.Repeat((byte)0xEE, 10)];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Seal_FixedKeyAndSequenceNumber_IsTheCiphertextAndTagPinnedFromThePrimitives()
    {
        using ChaCha20Poly1305PacketProtection protection = new(Key);
        Diagnostics.Arrange("key", "bytes 1 to 64");
        Diagnostics.Arrange("sequence number", 7);
        Diagnostics.ArrangePacket("packet", Packet);

        byte[] sealedPacket = protection.Seal(7, Packet);

        Diagnostics.ActBytes("sealed", sealedPacket);
        Diagnostics.AssertBytes("sealed against PROTOCOL.chacha20poly1305", ExpectedSeal(7), sealedPacket);
        Diagnostics.Assert("sealed length", Packet.Length + 16, sealedPacket.Length);
        Diagnostics.Assert("block size, pads length field, length block, tag", "8, False, 4, 16", $"{protection.BlockSize}, {protection.PadsPacketLengthField}, {protection.LengthBlockLength}, {protection.TagLength}");
        CollectionAssert.AreEqual(ExpectedSeal(7), sealedPacket);
        Assert.AreEqual(Packet.Length + 16, sealedPacket.Length);
        Assert.AreEqual(8, protection.BlockSize);
        Assert.IsFalse(protection.PadsPacketLengthField);
        Assert.AreEqual(4, protection.LengthBlockLength);
        Assert.AreEqual(16, protection.TagLength);
    }

    [TestMethod]
    public void Seal_TheSequenceNumberIsTheNonce()
    {
        using ChaCha20Poly1305PacketProtection protection = new(Key);
        Diagnostics.Arrange("sequence numbers", "3, 3, 4 and uint.MaxValue");
        Diagnostics.ArrangePacket("packet", Packet);

        byte[] sealedAt3 = protection.Seal(3, Packet);
        byte[] sealedAt3Again = protection.Seal(3, Packet);
        byte[] sealedAt4 = protection.Seal(4, Packet);
        byte[] sealedAtMax = protection.Seal(uint.MaxValue, Packet);

        Diagnostics.ActBytes("sealed, sequence 3", sealedAt3);
        Diagnostics.ActBytes("sealed, sequence 4", sealedAt4);
        Diagnostics.AssertBytes("sealed at 3, twice", sealedAt3, sealedAt3Again);
        Diagnostics.Assert("sealed at 3 differs from sealed at 4", true, !sealedAt3.SequenceEqual(sealedAt4));
        Diagnostics.AssertBytes("sealed, sequence uint.MaxValue", ExpectedSeal(uint.MaxValue), sealedAtMax);
        CollectionAssert.AreEqual(sealedAt3, sealedAt3Again, "nothing advances between packets but the sequence number");
        CollectionAssert.AreNotEqual(sealedAt3, sealedAt4);
        CollectionAssert.AreEqual(ExpectedSeal(uint.MaxValue), sealedAtMax);
    }

    [TestMethod]
    public void Open_WhatSealWrote_ReturnsThePacketAfterItsLength()
    {
        using ChaCha20Poly1305PacketProtection sender = new(Key);
        using ChaCha20Poly1305PacketProtection receiver = new(Key);
        Diagnostics.Arrange("sequence numbers", "0, 1, 2 and uint.MaxValue");
        Diagnostics.ArrangePacket("packet", Packet);

        foreach (uint sequenceNumber in new uint[] { 0, 1, 2, uint.MaxValue })
        {
            byte[] sealedPacket = sender.Seal(sequenceNumber, Packet);
            byte[] lengthBlock = sealedPacket[..4];

            uint length = receiver.DecryptPacketLength(sequenceNumber, lengthBlock);
            Diagnostics.Assert($"packet length, sequence {sequenceNumber}", 28u, length);
            Assert.AreEqual(28u, length);
            Diagnostics.AssertBytes($"length block after decrypting, sequence {sequenceNumber}", sealedPacket[..4], lengthBlock);
            CollectionAssert.AreEqual(sealedPacket[..4], lengthBlock, "the length is decrypted into a copy, since the tag covers it as sent");
            byte[] opened = receiver.Open(sequenceNumber, lengthBlock, sealedPacket[4..]);
            Diagnostics.ActBytes($"opened, sequence {sequenceNumber}", opened);
            Diagnostics.AssertBytes($"opened, sequence {sequenceNumber}", Packet[4..], opened);
            CollectionAssert.AreEqual(Packet[4..], opened);
        }
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "tag altered")]
    [DataRow(10, DisplayName = "ciphertext altered")]
    [DataRow(3, DisplayName = "length altered")]
    public void Open_AlteredPacket_ThrowsWithMinus12(int alteredByte)
    {
        using ChaCha20Poly1305PacketProtection sender = new(Key);
        using ChaCha20Poly1305PacketProtection receiver = new(Key);
        byte[] sealedPacket = sender.Seal(0, Packet);
        sealedPacket[alteredByte < 0 ? sealedPacket.Length - 1 : alteredByte] ^= 0x01;
        Diagnostics.Arrange("altered byte", alteredByte < 0 ? "last" : alteredByte);
        Diagnostics.Bytes("sealed packet, altered", sealedPacket);

        SshPacketAuthenticationException failure = Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => receiver.Open(0, sealedPacket[..4], sealedPacket[4..]));

        Diagnostics.ActAndAssertThrown(nameof(SshPacketAuthenticationException), failure);
        Diagnostics.Assert("libssh2 error code", -12, failure.Libssh2ErrorCode);
        Assert.AreEqual(-12, failure.Libssh2ErrorCode);
    }

    [TestMethod]
    public void Open_OtherSequenceNumber_Throws()
    {
        using ChaCha20Poly1305PacketProtection protection = new(Key);
        byte[] sealedPacket = protection.Seal(1, Packet);
        Diagnostics.Arrange("sealed at, opened at", "sequence 1, sequence 2");
        Diagnostics.Bytes("sealed packet", sealedPacket);

        var failure = Assert.ThrowsExactly<SshPacketAuthenticationException>(() => protection.Open(2, sealedPacket[..4], sealedPacket[4..]));

        Diagnostics.ActAndAssertThrown(nameof(SshPacketAuthenticationException), failure);
    }

    /// <summary>
    /// Builds the sealed packet straight from OpenSSH's <c>PROTOCOL.chacha20poly1305</c>:
    /// the length under K_1 from block 0, the Poly1305 key from block 0 under K_2, the rest
    /// under K_2 from block 1, and the tag over both ciphertexts.
    /// </summary>
    private static byte[] ExpectedSeal(uint sequenceNumber)
    {
        byte[] k2 = Key[..32];
        byte[] k1 = Key[32..];
        byte[] nonce = [0, 0, 0, 0, (byte)(sequenceNumber >> 24), (byte)(sequenceNumber >> 16), (byte)(sequenceNumber >> 8), (byte)sequenceNumber];
        byte[] lengthStream = new byte[64];
        ChaCha20.ComputeBlock(k1, nonce, 0, lengthStream);
        byte[] encryptedLength = [.. Packet[..4].Select((value, index) => (byte)(value ^ lengthStream[index]))];
        byte[] block0 = new byte[64];
        ChaCha20.ComputeBlock(k2, nonce, 0, block0);
        byte[] rest = Packet[4..];
        byte[] payloadStream = new byte[64];
        ChaCha20.ComputeBlock(k2, nonce, 1, payloadStream);
        byte[] encryptedRest = [.. rest.Select((value, index) => (byte)(value ^ payloadStream[index]))];
        byte[] tag = new byte[16];
        Poly1305.ComputeTag(block0[..32], [.. encryptedLength, .. encryptedRest], tag);
        return [.. encryptedLength, .. encryptedRest, .. tag];
    }
}
