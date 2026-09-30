using System.Security.Cryptography;
using Curl.Cryptography;
using Curl.Protocol.Ssh.Fakes;
using Curl.Protocol.Ssh.KeyExchange;
using Curl.Protocol.Ssh.Transport;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class SshPacketProtectionsTests
{
    private static readonly SshKeyDerivation Keys = new(HashAlgorithmName.SHA256, [0x42, 0x17, 0x99], [.. Enumerable.Repeat((byte)0x55, 32)], [.. Enumerable.Repeat((byte)0x66, 32)]);

    private static readonly int[] PayloadLengths = [1, 11, 15, 16, 17, 100, 255, 4096, 32000];

    [TestMethod]
    [DataRow("chacha20-poly1305@openssh.com", null)]
    [DataRow("aes256-gcm@openssh.com", null)]
    [DataRow("aes128-gcm@openssh.com", null)]
    [DataRow("aes256-ctr", "hmac-sha2-256")]
    [DataRow("aes256-ctr", "hmac-sha2-256-etm@openssh.com")]
    [DataRow("aes256-ctr", "hmac-sha2-512")]
    [DataRow("aes256-ctr", "hmac-sha2-512-etm@openssh.com")]
    [DataRow("aes192-ctr", "hmac-sha2-256")]
    [DataRow("aes192-ctr", "hmac-sha2-256-etm@openssh.com")]
    [DataRow("aes192-ctr", "hmac-sha2-512")]
    [DataRow("aes192-ctr", "hmac-sha2-512-etm@openssh.com")]
    [DataRow("aes128-ctr", "hmac-sha2-256")]
    [DataRow("aes128-ctr", "hmac-sha2-256-etm@openssh.com")]
    [DataRow("aes128-ctr", "hmac-sha2-512")]
    [DataRow("aes128-ctr", "hmac-sha2-512-etm@openssh.com")]
    [DataRow("aes256-cbc", "hmac-sha2-256")]
    [DataRow("rijndael-cbc@lysator.liu.se", "hmac-sha2-256")]
    [DataRow("aes192-cbc", "hmac-sha2-256")]
    [DataRow("aes128-cbc", "hmac-sha2-512-etm@openssh.com")]
    [DataRow("3des-cbc", "hmac-sha2-256")]
    [DataRow("blowfish-cbc", "hmac-sha2-256")]
    [DataRow("cast128-cbc", "hmac-sha2-512")]
    [DataRow("arcfour", "hmac-sha2-256")]
    [DataRow("arcfour128", "hmac-sha2-256-etm@openssh.com")]
    [DataRow("aes128-ctr", "hmac-sha1")]
    [DataRow("aes128-ctr", "hmac-sha1-etm@openssh.com")]
    [DataRow("aes128-ctr", "hmac-sha1-96")]
    [DataRow("aes128-ctr", "hmac-md5")]
    [DataRow("aes128-ctr", "hmac-md5-etm@openssh.com")]
    [DataRow("aes128-ctr", "hmac-md5-96")]
    [DataRow("aes128-ctr", "hmac-ripemd160")]
    [DataRow("aes128-ctr", "hmac-ripemd160@openssh.com")]
    [DataRow("3des-cbc", "hmac-md5-96")]
    [DataRow("arcfour", "hmac-sha1-96")]
    public async Task ForClientToServer_EachPair_RoundTripsPacketsOfSeveralLengthsInOrder(string cipher, string? mac)
    {
        ScriptedConnection connection = new();
        SshPacketWriter writer = new(connection, new RepeatingRandomSource(0xA5));
        writer.ChangeProtection(SshPacketProtections.ForClientToServer(SshTestAlgorithms.With(cipher, mac), Keys));
        byte[][] payloads = [.. PayloadLengths.Select(length => Enumerable.Range(0, length).Select(value => (byte)(value * 7)).ToArray())];
        foreach (byte[] payload in payloads)
        {
            await writer.WriteAsync(payload, CancellationToken.None);
        }

        SshPacketReader reader = new(new SshConnectionReader(ScriptedConnection.InChunks(connection.Written, 1000)));
        reader.ChangeProtection(SshPacketProtections.ForClientToServer(SshTestAlgorithms.With(cipher, mac), Keys));

        foreach (byte[] payload in payloads)
        {
            CollectionAssert.AreEqual(payload, await reader.ReadAsync(CancellationToken.None), $"{payload.Length}-byte payload");
        }

        Assert.AreEqual((uint)payloads.Length, reader.SequenceNumber);
        Assert.IsFalse(connection.Written.AsSpan().IndexOf(payloads[^1].AsSpan(0, 64)) >= 0, "the payload is not sent in the clear");
    }

    [TestMethod]
    [DataRow("chacha20-poly1305@openssh.com", null)]
    [DataRow("aes256-gcm@openssh.com", null)]
    [DataRow("aes256-ctr", "hmac-sha2-256")]
    [DataRow("aes256-ctr", "hmac-sha2-512-etm@openssh.com")]
    public void ForServerToClient_UsesTheOtherDirectionsKeys(string cipher, string? mac)
    {
        using ISshPacketProtection clientToServer = SshPacketProtections.ForClientToServer(SshTestAlgorithms.With(cipher, mac), Keys);
        using ISshPacketProtection serverToClient = SshPacketProtections.ForServerToClient(SshTestAlgorithms.With(cipher, mac), Keys);
        byte[] packet = [0, 0, 0, 28, 10, .. new byte[17], .. Enumerable.Repeat((byte)0xEE, 10)];

        byte[] sealedPacket = clientToServer.Seal(0, packet);
        byte[] lengthBlock = sealedPacket[..serverToClient.LengthBlockLength];
        serverToClient.DecryptPacketLength(0, lengthBlock);

        Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => serverToClient.Open(0, lengthBlock, sealedPacket[serverToClient.LengthBlockLength..]));
    }

    [TestMethod]
    [DataRow("aes256-ctr", 32, "hmac-sha2-512", 64, 'C', 'A', 'E', true)]
    [DataRow("aes192-ctr", 24, "hmac-sha2-256-etm@openssh.com", 32, 'D', 'B', 'F', false)]
    public void Create_CtrPair_TakesTheKeyIvAndIntegrityKeyLengthsItNeeds(
        string cipher,
        int keyLength,
        string mac,
        int macLength,
        char encryption,
        char iv,
        char integrity,
        bool clientToServer)
    {
        using ISshPacketProtection protection = clientToServer
            ? SshPacketProtections.ForClientToServer(SshTestAlgorithms.With(cipher, mac), Keys)
            : SshPacketProtections.ForServerToClient(SshTestAlgorithms.With(cipher, mac), Keys);
        using CipherAndMacPacketProtection expected = new(
            new AesCtrSshCipher(Keys.DeriveKey((SshKeyPurpose)encryption, keyLength), Keys.DeriveKey((SshKeyPurpose)iv, 16)),
            new SshMac(macLength == 64 ? HashAlgorithmName.SHA512 : HashAlgorithmName.SHA256, Keys.DeriveKey((SshKeyPurpose)integrity, macLength), macLength, mac.EndsWith("-etm@openssh.com", StringComparison.Ordinal)));
        byte[] packet = [0, 0, 0, 28, 10, .. new byte[17], .. Enumerable.Repeat((byte)0xEE, 10)];

        CollectionAssert.AreEqual(expected.Seal(5, packet), protection.Seal(5, packet));
    }

    [TestMethod]
    [DataRow("aes256-gcm@openssh.com", 32)]
    [DataRow("aes128-gcm@openssh.com", 16)]
    public void Create_GcmCipher_TakesTheKeyAndATwelveByteIvAndNoMac(string cipher, int keyLength)
    {
        using ISshPacketProtection protection = SshPacketProtections.ForServerToClient(SshTestAlgorithms.With(cipher, null), Keys);
        using AesGcmPacketProtection expected = new(Keys.DeriveKey(SshKeyPurpose.EncryptionKeyServerToClient, keyLength), Keys.DeriveKey(SshKeyPurpose.InitialIvServerToClient, 12));
        byte[] packet = [0, 0, 0, 28, 10, .. new byte[17], .. Enumerable.Repeat((byte)0xEE, 10)];

        CollectionAssert.AreEqual(expected.Seal(0, packet), protection.Seal(0, packet));
    }

    [TestMethod]
    [DataRow(true, 'C')]
    [DataRow(false, 'D')]
    public void Create_ChaCha20Poly1305_TakesASixtyFourByteKeyAndNoIvOrMac(bool clientToServer, char encryption)
    {
        using ISshPacketProtection protection = clientToServer
            ? SshPacketProtections.ForClientToServer(SshTestAlgorithms.With("chacha20-poly1305@openssh.com", null), Keys)
            : SshPacketProtections.ForServerToClient(SshTestAlgorithms.With("chacha20-poly1305@openssh.com", null), Keys);
        using ChaCha20Poly1305PacketProtection expected = new(Keys.DeriveKey((SshKeyPurpose)encryption, 64));
        byte[] packet = [0, 0, 0, 28, 10, .. new byte[17], .. Enumerable.Repeat((byte)0xEE, 10)];

        CollectionAssert.AreEqual(expected.Seal(9, packet), protection.Seal(9, packet));
    }

    [TestMethod]
    [DataRow("serpent256-cbc", null, DisplayName = "cipher not implemented")]
    [DataRow("aes128-ctr", "hmac-sha2-384", DisplayName = "MAC not implemented")]
    public void Create_NameNotImplemented_ThrowsNotSupported(string cipher, string? mac)
    {
        Assert.ThrowsExactly<NotSupportedException>(
            () => SshPacketProtections.ForClientToServer(SshTestAlgorithms.With(cipher, mac), Keys));
    }

    [TestMethod]
    [DataRow("aes256-cbc", 32, 16)]
    [DataRow("rijndael-cbc@lysator.liu.se", 32, 16)]
    [DataRow("aes192-cbc", 24, 16)]
    [DataRow("aes128-cbc", 16, 16)]
    [DataRow("3des-cbc", 24, 8)]
    [DataRow("blowfish-cbc", 16, 8)]
    [DataRow("cast128-cbc", 16, 8)]
    [DataRow("arcfour", 16, 0)]
    [DataRow("arcfour128", 16, Rc4.Rfc4345DiscardLength)]
    public void Create_LegacyCipher_TakesTheKeyAndIvLengthsItNeeds(string cipher, int keyLength, int ivLengthOrDiscard)
    {
        using ISshPacketProtection protection = SshPacketProtections.ForClientToServer(SshTestAlgorithms.With(cipher, "hmac-sha2-256"), Keys);
        byte[] key = Keys.DeriveKey(SshKeyPurpose.EncryptionKeyClientToServer, keyLength);
        ISshCipher expectedCipher = cipher switch
        {
            "3des-cbc" => CbcSshCipher.ForTripleDes(key, Keys.DeriveKey(SshKeyPurpose.InitialIvClientToServer, 8)),
            "blowfish-cbc" => CbcSshCipher.ForBlowfish(key, Keys.DeriveKey(SshKeyPurpose.InitialIvClientToServer, 8)),
            "cast128-cbc" => CbcSshCipher.ForCast128(key, Keys.DeriveKey(SshKeyPurpose.InitialIvClientToServer, 8)),
            "arcfour" or "arcfour128" => new Rc4SshCipher(key, ivLengthOrDiscard),
            _ => CbcSshCipher.ForAes(key, Keys.DeriveKey(SshKeyPurpose.InitialIvClientToServer, ivLengthOrDiscard)),
        };
        using CipherAndMacPacketProtection expected = new(
            expectedCipher,
            new SshMac(HashAlgorithmName.SHA256, Keys.DeriveKey(SshKeyPurpose.IntegrityKeyClientToServer, 32), 32, isEncryptThenMac: false));
        byte[] packet = [0, 0, 0, 44, 10, .. new byte[33], .. Enumerable.Repeat((byte)0xEE, 10)];

        CollectionAssert.AreEqual(expected.Seal(3, packet), protection.Seal(3, packet));
        Assert.AreEqual(Math.Max(8, expectedCipher.BlockSize), protection.BlockSize);
    }

    [TestMethod]
    [DataRow("hmac-sha1", 20, 20)]
    [DataRow("hmac-sha1-96", 20, 12)]
    [DataRow("hmac-md5", 16, 16)]
    [DataRow("hmac-md5-96", 16, 12)]
    [DataRow("hmac-ripemd160", 20, 20)]
    [DataRow("hmac-ripemd160@openssh.com", 20, 20)]
    public void Create_LegacyMac_SendsTheFirstBytesOfItsHmacUnderTheIntegrityKey(string mac, int keyLength, int macLength)
    {
        using ISshPacketProtection protection = SshPacketProtections.ForServerToClient(SshTestAlgorithms.With("aes128-ctr", mac), Keys);
        byte[] integrityKey = Keys.DeriveKey(SshKeyPurpose.IntegrityKeyServerToClient, keyLength);
        byte[] packet = [0, 0, 0, 28, 10, .. new byte[17], .. Enumerable.Repeat((byte)0xEE, 10)];
        byte[] macInput = [0, 0, 0, 4, .. packet];
        byte[] fullHmac = mac switch
        {
            "hmac-sha1" or "hmac-sha1-96" => HMACSHA1.HashData(integrityKey, macInput),
            "hmac-md5" or "hmac-md5-96" => HMACMD5.HashData(integrityKey, macInput),
            _ => RipemdHmac(integrityKey, macInput),
        };

        byte[] sealedPacket = protection.Seal(4, packet);

        Assert.AreEqual(macLength, protection.TagLength);
        CollectionAssert.AreEqual(fullHmac[..macLength], sealedPacket[packet.Length..]);
    }

    [TestMethod]
    public void Names_AreEveryCipherAndMacOfTheFullPreset()
    {
        CollectionAssert.AreEquivalent(
            new[]
            {
                "chacha20-poly1305@openssh.com", "aes256-gcm@openssh.com", "aes128-gcm@openssh.com", "aes256-ctr", "aes192-ctr", "aes128-ctr",
                "aes256-cbc", "rijndael-cbc@lysator.liu.se", "aes192-cbc", "aes128-cbc", "blowfish-cbc", "arcfour128", "arcfour", "cast128-cbc", "3des-cbc",
                "hmac-sha2-256", "hmac-sha2-256-etm@openssh.com", "hmac-sha2-512", "hmac-sha2-512-etm@openssh.com", "hmac-sha1",
                "hmac-sha1-etm@openssh.com", "hmac-sha1-96", "hmac-md5", "hmac-md5-etm@openssh.com", "hmac-md5-96", "hmac-ripemd160",
                "hmac-ripemd160@openssh.com",
            },
            SshPacketProtections.Names.ToArray());
    }

    private static byte[] RipemdHmac(byte[] key, byte[] message)
    {
        byte[] result = new byte[HmacRipemd160.HashSize];
        HmacRipemd160.HashData(key, message, result);
        return result;
    }
}
