using System.Security.Cryptography;
using System.Text;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class SshMacTests
{
    /// <summary>RFC 4231 test case 2's data after its first four bytes, "what", which the sequence number carries.</summary>
    private static readonly byte[] RestOfTestCase2 = Encoding.ASCII.GetBytes(" do ya want for nothing?");

    /// <summary>"what" as a big-endian <c>uint32</c>.</summary>
    private const uint What = 0x77686174;

    [TestMethod]
    [DataRow("SHA256", "5BDCC146BF60754E6A042426089575C75A003F089D2739839DEC58B964EC3843", DisplayName = "RFC 4231 test case 2, hmac-sha2-256")]
    [DataRow("SHA512", "164B7A7BFCF819E2E395FBE73B56E0A387BD64222E831FD610270CD7EA2505549758BF75C05A994A6D034F65F8F0E6FDCAEAB1A34D4A6B4B636E070A38BCE737", DisplayName = "RFC 4231 test case 2, hmac-sha2-512")]
    public void Compute_Rfc4231Vector_HashesTheSequenceNumberThenThePacket(string hash, string expected)
    {
        using SshMac mac = new(new HashAlgorithmName(hash), Encoding.ASCII.GetBytes("Jefe"), expected.Length / 2, isEncryptThenMac: false);

        byte[] result = mac.Compute(What, RestOfTestCase2.AsSpan(0, 5), RestOfTestCase2.AsSpan(5));

        Assert.AreEqual(expected, Convert.ToHexString(result));
        Assert.AreEqual(expected, Convert.ToHexString(mac.Compute(What, RestOfTestCase2, [])), "the hash resets after each packet");
    }

    [TestMethod]
    [DataRow("SHA1", 20, "EFFCDF6AE5EB2FA2D27416D5F184DF9C259A7C79", DisplayName = "RFC 2202 test case 2, hmac-sha1")]
    [DataRow("SHA1", 12, "EFFCDF6AE5EB2FA2D27416D5", DisplayName = "RFC 2202 test case 2, hmac-sha1-96: its first 12 bytes")]
    [DataRow("MD5", 16, "750C783E6AB0B503EAA86E310A5DB738", DisplayName = "RFC 2202 test case 2, hmac-md5")]
    [DataRow("MD5", 12, "750C783E6AB0B503EAA86E31", DisplayName = "RFC 2202 test case 2, hmac-md5-96: its first 12 bytes")]
    [DataRow("RIPEMD160", 20, "DDA6C0213A485A9E24F4742064A7F033B43C4069", DisplayName = "RFC 2286 test case 2, hmac-ripemd160")]
    public void Compute_LegacyHmacVector_HashesTheSequenceNumberThenThePacket(string hash, int length, string expected)
    {
        byte[] key = Encoding.ASCII.GetBytes("Jefe");
        ISshHmac hmac = hash == "RIPEMD160" ? new Ripemd160SshHmac(key) : new BclSshHmac(new HashAlgorithmName(hash), key);
        using SshMac mac = new(hmac, length, isEncryptThenMac: false);

        Assert.AreEqual(expected, Convert.ToHexString(mac.Compute(What, RestOfTestCase2.AsSpan(0, 9), RestOfTestCase2.AsSpan(9))));
        Assert.AreEqual(expected, Convert.ToHexString(mac.Compute(What, RestOfTestCase2, [])), "the hash resets after each packet");
        mac.Verify(What, RestOfTestCase2, [], Convert.FromHexString(expected));
    }

    [TestMethod]
    public void Compute_ShorterLength_SendsTheFirstBytes()
    {
        using SshMac mac = new(HashAlgorithmName.SHA256, Encoding.ASCII.GetBytes("Jefe"), 12, isEncryptThenMac: true);

        Assert.AreEqual("5BDCC146BF60754E6A042426", Convert.ToHexString(mac.Compute(What, RestOfTestCase2, [])));
        Assert.AreEqual(12, mac.Length);
        Assert.IsTrue(mac.IsEncryptThenMac);
    }

    [TestMethod]
    public void Verify_MatchingMac_Returns()
    {
        using SshMac mac = new(HashAlgorithmName.SHA256, Encoding.ASCII.GetBytes("Jefe"), 32, isEncryptThenMac: false);

        mac.Verify(What, RestOfTestCase2, [], Convert.FromHexString("5BDCC146BF60754E6A042426089575C75A003F089D2739839DEC58B964EC3843"));
    }

    [TestMethod]
    [DataRow(What, 0, DisplayName = "last MAC byte flipped")]
    [DataRow(What + 1, -1, DisplayName = "another sequence number")]
    public void Verify_MacDoesNotMatch_ThrowsWithMinus4(uint sequenceNumber, int flippedByte)
    {
        using SshMac mac = new(HashAlgorithmName.SHA256, Encoding.ASCII.GetBytes("Jefe"), 32, isEncryptThenMac: false);
        byte[] received = Convert.FromHexString("5BDCC146BF60754E6A042426089575C75A003F089D2739839DEC58B964EC3843");
        if (flippedByte == 0)
        {
            received[^1] ^= 1;
        }

        SshPacketAuthenticationException failure = Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => mac.Verify(sequenceNumber, RestOfTestCase2, [], received));

        Assert.AreEqual(-4, failure.Libssh2ErrorCode);
    }
}
