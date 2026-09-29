using System.Security.Cryptography;
using Curl.Cryptography;
using static Curl.Protocol.Ssh.Fakes.SshTestEncoding;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class CipherAndMacPacketProtectionTests
{
    private static readonly byte[] Key = [.. Enumerable.Range(1, 16).Select(value => (byte)value)];

    private static readonly byte[] Counter = [.. Enumerable.Range(100, 16).Select(value => (byte)value)];

    private static readonly byte[] MacKey = [.. Enumerable.Range(200, 32).Select(value => (byte)value)];

    /// <summary>A 32-byte packet: length 28, padding length 6, payload 21 bytes, padding 6 bytes.</summary>
    private static readonly byte[] Packet = [0, 0, 0, 28, 6, .. Enumerable.Range(1, 21).Select(value => (byte)value), 0xEE, 0xEE, 0xEE, 0xEE, 0xEE, 0xEE];

    [TestMethod]
    public void Seal_MacThenEncrypt_MacsThePlaintextThenEncryptsTheWholePacket()
    {
        using CipherAndMacPacketProtection protection = Create(encryptThenMac: false);

        byte[] sealedPacket = protection.Seal(7, Packet);

        byte[] expectedCiphertext = Ctr(Packet);
        byte[] expectedMac = HMACSHA256.HashData(MacKey, (byte[])[.. UInt32(7), .. Packet]);
        CollectionAssert.AreEqual(expectedCiphertext.Concat(expectedMac).ToArray(), sealedPacket);
        Assert.AreEqual(16, protection.BlockSize);
        Assert.IsTrue(protection.PadsPacketLengthField);
        Assert.AreEqual(16, protection.LengthBlockLength);
        Assert.AreEqual(32, protection.TagLength);
    }

    [TestMethod]
    public void Seal_EncryptThenMac_LeavesTheLengthInTheClearAndMacsTheCiphertext()
    {
        using CipherAndMacPacketProtection protection = Create(encryptThenMac: true);

        byte[] sealedPacket = protection.Seal(7, Packet);

        byte[] expectedCiphertext = [.. Packet[..4], .. Ctr(Packet[4..])];
        byte[] expectedMac = HMACSHA256.HashData(MacKey, (byte[])[.. UInt32(7), .. expectedCiphertext]);
        CollectionAssert.AreEqual(expectedCiphertext.Concat(expectedMac).ToArray(), sealedPacket);
        Assert.IsFalse(protection.PadsPacketLengthField);
        Assert.AreEqual(4, protection.LengthBlockLength);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "MAC-then-encrypt")]
    [DataRow(true, DisplayName = "encrypt-then-MAC")]
    public void Open_WhatSealWrote_ReturnsThePacketAfterItsLength(bool encryptThenMac)
    {
        using CipherAndMacPacketProtection sender = Create(encryptThenMac);
        using CipherAndMacPacketProtection receiver = Create(encryptThenMac);
        byte[] sealedPacket = sender.Seal(3, Packet);
        byte[] lengthBlock = sealedPacket[..receiver.LengthBlockLength];

        uint packetLength = receiver.DecryptPacketLength(3, lengthBlock);
        byte[] opened = receiver.Open(3, lengthBlock, sealedPacket[receiver.LengthBlockLength..]);

        Assert.AreEqual(28u, packetLength);
        CollectionAssert.AreEqual(Packet[4..], opened);
    }

    [TestMethod]
    [DataRow(false, 0, DisplayName = "MAC-then-encrypt, MAC altered")]
    [DataRow(false, 20, DisplayName = "MAC-then-encrypt, ciphertext altered")]
    [DataRow(true, 0, DisplayName = "encrypt-then-MAC, MAC altered")]
    [DataRow(true, 20, DisplayName = "encrypt-then-MAC, ciphertext altered")]
    [DataRow(true, 3, DisplayName = "encrypt-then-MAC, length altered")]
    public void Open_AlteredPacket_ThrowsWithMinus4(bool encryptThenMac, int alteredByte)
    {
        using CipherAndMacPacketProtection sender = Create(encryptThenMac);
        using CipherAndMacPacketProtection receiver = Create(encryptThenMac);
        byte[] sealedPacket = sender.Seal(3, Packet);
        sealedPacket[alteredByte == 0 ? sealedPacket.Length - 1 : alteredByte] ^= 0x80;
        byte[] lengthBlock = sealedPacket[..receiver.LengthBlockLength];
        receiver.DecryptPacketLength(3, lengthBlock);

        SshPacketAuthenticationException failure = Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => receiver.Open(3, lengthBlock, sealedPacket[receiver.LengthBlockLength..]));

        Assert.AreEqual(-4, failure.Libssh2ErrorCode);
    }

    [TestMethod]
    public void Open_WrongSequenceNumber_ThrowsWithMinus4()
    {
        using CipherAndMacPacketProtection sender = Create(encryptThenMac: true);
        using CipherAndMacPacketProtection receiver = Create(encryptThenMac: true);
        byte[] sealedPacket = sender.Seal(3, Packet);

        Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => receiver.Open(4, sealedPacket[..4], sealedPacket[4..]));
    }

    private static CipherAndMacPacketProtection Create(bool encryptThenMac) =>
        new(new AesCtrSshCipher(Key, Counter), new SshMac(HashAlgorithmName.SHA256, MacKey, 32, encryptThenMac));

    private static byte[] Ctr(byte[] plaintext)
    {
        using AesCtr ctr = new(Key, Counter);
        byte[] output = new byte[plaintext.Length];
        ctr.ApplyKeyStream(plaintext, output);
        return output;
    }
}
