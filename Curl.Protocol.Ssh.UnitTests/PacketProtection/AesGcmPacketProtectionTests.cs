using System.Security.Cryptography;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class AesGcmPacketProtectionTests
{
    private static readonly byte[] Key = [.. Enumerable.Range(1, 32).Select(value => (byte)value)];

    /// <summary>A 32-byte packet: length 28, padding length 10, payload 17 bytes, padding 10 bytes.</summary>
    private static readonly byte[] Packet = [0, 0, 0, 28, 10, .. Enumerable.Range(1, 17).Select(value => (byte)value), .. Enumerable.Repeat((byte)0xEE, 10)];

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    public void AesGcm_Sp80038dTestCase4_IsThePrimitiveThePacketsUse()
    {
        byte[] plaintext = Convert.FromHexString("D9313225F88406E5A55909C5AFF5269A86A7A9531534F7DA2E4C303D8A318A721C3C0C95956809532FCF0E2449A6B525B16AEDF5AA0DE657BA637B39");
        byte[] ciphertext = new byte[plaintext.Length];
        byte[] tag = new byte[16];
        using AesGcm aesGcm = new(Convert.FromHexString("FEFFE9928665731C6D6A8F9467308308"), 16);
        Diagnostics.Arrange("key", "FEFFE9928665731C6D6A8F9467308308");
        Diagnostics.Arrange("nonce", "CAFEBABEFACEDBADDECAF888");
        Diagnostics.Arrange("associated data", "FEEDFACEDEADBEEFFEEDFACEDEADBEEFABADDAD2");
        Diagnostics.Bytes("plaintext", plaintext);

        aesGcm.Encrypt(Convert.FromHexString("CAFEBABEFACEDBADDECAF888"), plaintext, ciphertext, tag, Convert.FromHexString("FEEDFACEDEADBEEFFEEDFACEDEADBEEFABADDAD2"));

        Diagnostics.ActBytes("ciphertext", ciphertext);
        Diagnostics.ActBytes("tag", tag);
        Diagnostics.AssertHex("ciphertext", "42831EC2217774244B7221B784D0D49CE3AA212F2C02A4E035C17E2329ACA12E21D514B25466931C7D8F6A5AAC84AA051BA30B396A0AAC973D58E091", ciphertext);
        Diagnostics.AssertHex("tag", "5BC94FBC3221A5DB94FAE95AE7121A47", tag);
        Assert.AreEqual("42831EC2217774244B7221B784D0D49CE3AA212F2C02A4E035C17E2329ACA12E21D514B25466931C7D8F6A5AAC84AA051BA30B396A0AAC973D58E091", Convert.ToHexString(ciphertext));
        Assert.AreEqual("5BC94FBC3221A5DB94FAE95AE7121A47", Convert.ToHexString(tag));
    }

    [TestMethod]
    public void Seal_EncryptsAfterTheLengthWithTheLengthAsAssociatedData_AndAdvancesTheInvocationCounter()
    {
        byte[] iv = Convert.FromHexString("0A0B0C0D00000000000000FF");
        using AesGcmPacketProtection protection = new(Key, iv);
        Diagnostics.Arrange("iv", Convert.ToHexString(iv));
        Diagnostics.ArrangePacket("packet", Packet);

        byte[] first = protection.Seal(0, Packet);
        byte[] second = protection.Seal(1, Packet);

        Diagnostics.ActBytes("sealed, sequence 0", first);
        Diagnostics.ActBytes("sealed, sequence 1", second);
        Diagnostics.AssertBytes("sealed, sequence 0", ExpectedSeal(iv), first);
        Diagnostics.AssertBytes("sealed, sequence 1, invocation counter carried", ExpectedSeal(Convert.FromHexString("0A0B0C0D0000000000000100")), second);
        Diagnostics.Assert("block size, pads length field, length block, tag", "16, False, 4, 16", $"{protection.BlockSize}, {protection.PadsPacketLengthField}, {protection.LengthBlockLength}, {protection.TagLength}");
        CollectionAssert.AreEqual(ExpectedSeal(iv), first);
        CollectionAssert.AreEqual(ExpectedSeal(Convert.FromHexString("0A0B0C0D0000000000000100")), second, "the invocation counter carries");
        Assert.AreEqual(16, protection.BlockSize);
        Assert.IsFalse(protection.PadsPacketLengthField);
        Assert.AreEqual(4, protection.LengthBlockLength);
        Assert.AreEqual(16, protection.TagLength);
    }

    [TestMethod]
    public void Seal_InvocationCounterAtItsLargest_WrapsWithoutTouchingTheFixedField()
    {
        using AesGcmPacketProtection protection = new(Key, Convert.FromHexString("0A0B0C0DFFFFFFFFFFFFFFFF"));
        Diagnostics.Arrange("iv", "0A0B0C0DFFFFFFFFFFFFFFFF");
        Diagnostics.ArrangePacket("packet", Packet);

        protection.Seal(0, Packet);
        byte[] second = protection.Seal(1, Packet);

        Diagnostics.ActBytes("sealed, sequence 1", second);
        Diagnostics.AssertBytes("sealed with nonce 0A0B0C0D0000000000000000", ExpectedSeal(Convert.FromHexString("0A0B0C0D0000000000000000")), second);
        CollectionAssert.AreEqual(ExpectedSeal(Convert.FromHexString("0A0B0C0D0000000000000000")), second);
    }

    [TestMethod]
    public void Open_WhatSealWrote_ReturnsThePacketAfterItsLength()
    {
        byte[] iv = Convert.FromHexString("0A0B0C0D0000000000000001");
        using AesGcmPacketProtection sender = new(Key, iv);
        using AesGcmPacketProtection receiver = new(Key, iv);
        Diagnostics.Arrange("iv", Convert.ToHexString(iv));
        Diagnostics.ArrangePacket("packet", Packet);

        foreach (uint sequenceNumber in new uint[] { 0, 1, 2 })
        {
            byte[] sealedPacket = sender.Seal(sequenceNumber, Packet);
            byte[] lengthBlock = sealedPacket[..4];

            uint length = receiver.DecryptPacketLength(sequenceNumber, lengthBlock);
            byte[] opened = receiver.Open(sequenceNumber, lengthBlock, sealedPacket[4..]);
            Diagnostics.ActBytes($"opened, sequence {sequenceNumber}", opened);
            Diagnostics.Assert($"packet length, sequence {sequenceNumber}", 28u, length);
            Diagnostics.AssertBytes($"opened, sequence {sequenceNumber}", Packet[4..], opened);
            Assert.AreEqual(28u, length);
            CollectionAssert.AreEqual(Packet[4..], opened);
        }
    }

    [TestMethod]
    [DataRow(-1, DisplayName = "tag altered")]
    [DataRow(10, DisplayName = "ciphertext altered")]
    [DataRow(3, DisplayName = "length altered")]
    public void Open_AlteredPacket_ThrowsWithMinus12(int alteredByte)
    {
        byte[] iv = Convert.FromHexString("0A0B0C0D0000000000000001");
        using AesGcmPacketProtection sender = new(Key, iv);
        using AesGcmPacketProtection receiver = new(Key, iv);
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

    private static byte[] ExpectedSeal(byte[] nonce)
    {
        using AesGcm aesGcm = new(Key, 16);
        byte[] ciphertext = new byte[Packet.Length - 4];
        byte[] tag = new byte[16];
        aesGcm.Encrypt(nonce, Packet.AsSpan(4), ciphertext, tag, Packet.AsSpan(0, 4));
        return [.. Packet[..4], .. ciphertext, .. tag];
    }
}
