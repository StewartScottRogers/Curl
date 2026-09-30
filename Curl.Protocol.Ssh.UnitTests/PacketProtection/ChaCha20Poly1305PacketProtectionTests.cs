using Curl.Cryptography;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class ChaCha20Poly1305PacketProtectionTests
{
    /// <summary>The 64-byte derived key: K_2 is bytes 1 to 32, K_1 bytes 33 to 64.</summary>
    private static readonly byte[] Key = [.. Enumerable.Range(1, 64).Select(value => (byte)value)];

    /// <summary>A 32-byte packet: length 28, padding length 10, payload 17 bytes, padding 10 bytes.</summary>
    private static readonly byte[] Packet = [0, 0, 0, 28, 10, .. Enumerable.Range(1, 17).Select(value => (byte)value), .. Enumerable.Repeat((byte)0xEE, 10)];

    [TestMethod]
    public void Seal_FixedKeyAndSequenceNumber_IsTheCiphertextAndTagPinnedFromThePrimitives()
    {
        using ChaCha20Poly1305PacketProtection protection = new(Key);

        byte[] sealedPacket = protection.Seal(7, Packet);

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

        CollectionAssert.AreEqual(protection.Seal(3, Packet), protection.Seal(3, Packet), "nothing advances between packets but the sequence number");
        CollectionAssert.AreNotEqual(protection.Seal(3, Packet), protection.Seal(4, Packet));
        CollectionAssert.AreEqual(ExpectedSeal(uint.MaxValue), protection.Seal(uint.MaxValue, Packet));
    }

    [TestMethod]
    public void Open_WhatSealWrote_ReturnsThePacketAfterItsLength()
    {
        using ChaCha20Poly1305PacketProtection sender = new(Key);
        using ChaCha20Poly1305PacketProtection receiver = new(Key);

        foreach (uint sequenceNumber in new uint[] { 0, 1, 2, uint.MaxValue })
        {
            byte[] sealedPacket = sender.Seal(sequenceNumber, Packet);
            byte[] lengthBlock = sealedPacket[..4];

            Assert.AreEqual(28u, receiver.DecryptPacketLength(sequenceNumber, lengthBlock));
            CollectionAssert.AreEqual(sealedPacket[..4], lengthBlock, "the length is decrypted into a copy, since the tag covers it as sent");
            CollectionAssert.AreEqual(Packet[4..], receiver.Open(sequenceNumber, lengthBlock, sealedPacket[4..]));
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

        SshPacketAuthenticationException failure = Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => receiver.Open(0, sealedPacket[..4], sealedPacket[4..]));

        Assert.AreEqual(-12, failure.Libssh2ErrorCode);
    }

    [TestMethod]
    public void Open_OtherSequenceNumber_Throws()
    {
        using ChaCha20Poly1305PacketProtection protection = new(Key);
        byte[] sealedPacket = protection.Seal(1, Packet);

        Assert.ThrowsExactly<SshPacketAuthenticationException>(() => protection.Open(2, sealedPacket[..4], sealedPacket[4..]));
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
