using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;
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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void Seal_MacThenEncrypt_MacsThePlaintextThenEncryptsTheWholePacket()
    {
        using CipherAndMacPacketProtection protection = Create(encryptThenMac: false);
        ArrangeProtection(encryptThenMac: false, 7);

        byte[] sealedPacket = protection.Seal(7, Packet);

        Diagnostics.ActBytes("sealed", sealedPacket);
        byte[] expectedCiphertext = Ctr(Packet);
        byte[] expectedMac = HMACSHA256.HashData(MacKey, (byte[])[.. UInt32(7), .. Packet]);
        Diagnostics.AssertBytes("sealed: aes128-ctr of the packet, then hmac-sha2-256 of the plaintext", expectedCiphertext.Concat(expectedMac).ToArray(), sealedPacket);
        Diagnostics.Assert("block size, pads length field, length block, tag", "16, True, 16, 32", $"{protection.BlockSize}, {protection.PadsPacketLengthField}, {protection.LengthBlockLength}, {protection.TagLength}");
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
        ArrangeProtection(encryptThenMac: true, 7);

        byte[] sealedPacket = protection.Seal(7, Packet);

        Diagnostics.ActBytes("sealed", sealedPacket);
        byte[] expectedCiphertext = [.. Packet[..4], .. Ctr(Packet[4..])];
        byte[] expectedMac = HMACSHA256.HashData(MacKey, (byte[])[.. UInt32(7), .. expectedCiphertext]);
        Diagnostics.AssertBytes("sealed: length in the clear, aes128-ctr of the rest, then hmac-sha2-256 of the ciphertext", expectedCiphertext.Concat(expectedMac).ToArray(), sealedPacket);
        Diagnostics.Assert("pads length field, length block", "False, 4", $"{protection.PadsPacketLengthField}, {protection.LengthBlockLength}");
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
        ArrangeProtection(encryptThenMac, 3);
        Diagnostics.Bytes("sealed packet", sealedPacket);

        uint packetLength = receiver.DecryptPacketLength(3, lengthBlock);
        byte[] opened = receiver.Open(3, lengthBlock, sealedPacket[receiver.LengthBlockLength..]);

        Diagnostics.Act("decrypted packet length", packetLength);
        Diagnostics.ActBytes("opened", opened);
        Diagnostics.Assert("packet length", 28u, packetLength);
        Diagnostics.AssertBytes("opened", Packet[4..], opened);
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
        ArrangeProtection(encryptThenMac, 3);
        Diagnostics.Arrange("altered byte", alteredByte == 0 ? "last" : alteredByte);
        Diagnostics.Bytes("sealed packet, altered", sealedPacket);

        SshPacketAuthenticationException failure = Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => receiver.Open(3, lengthBlock, sealedPacket[receiver.LengthBlockLength..]));

        Diagnostics.ActAndAssertThrown(nameof(SshPacketAuthenticationException), failure);
        Diagnostics.Assert("libssh2 error code", -4, failure.Libssh2ErrorCode);
        Assert.AreEqual(-4, failure.Libssh2ErrorCode);
    }

    [TestMethod]
    public void Open_WrongSequenceNumber_ThrowsWithMinus4()
    {
        using CipherAndMacPacketProtection sender = Create(encryptThenMac: true);
        using CipherAndMacPacketProtection receiver = Create(encryptThenMac: true);
        byte[] sealedPacket = sender.Seal(3, Packet);
        ArrangeProtection(encryptThenMac: true, 3);
        Diagnostics.Arrange("opened at", "sequence 4");

        var failure = Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => receiver.Open(4, sealedPacket[..4], sealedPacket[4..]));

        Diagnostics.ActAndAssertThrown(nameof(SshPacketAuthenticationException), failure);
    }

    private void ArrangeProtection(bool encryptThenMac, uint sequenceNumber)
    {
        Diagnostics.Arrange("algorithms", encryptThenMac ? "aes128-ctr, hmac-sha2-256-etm@openssh.com" : "aes128-ctr, hmac-sha2-256");
        Diagnostics.Arrange("sealed at sequence", sequenceNumber);
        Diagnostics.ArrangePacket("packet", Packet);
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
