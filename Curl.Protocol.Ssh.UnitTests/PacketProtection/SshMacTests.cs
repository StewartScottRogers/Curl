using System.Security.Cryptography;
using System.Text;
using Curl.Protocol.Ssh.Keys;
using Curl.Testing;

namespace Curl.Protocol.Ssh.PacketProtection;

[TestClass]
public sealed class SshMacTests
{
    /// <summary>RFC 4231 test case 2's data after its first four bytes, "what", which the sequence number carries.</summary>
    private static readonly byte[] RestOfTestCase2 = Encoding.ASCII.GetBytes(" do ya want for nothing?");

    /// <summary>"what" as a big-endian <c>uint32</c>.</summary>
    private const uint What = 0x77686174;

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("SHA256", "5BDCC146BF60754E6A042426089575C75A003F089D2739839DEC58B964EC3843", DisplayName = "RFC 4231 test case 2, hmac-sha2-256")]
    [DataRow("SHA512", "164B7A7BFCF819E2E395FBE73B56E0A387BD64222E831FD610270CD7EA2505549758BF75C05A994A6D034F65F8F0E6FDCAEAB1A34D4A6B4B636E070A38BCE737", DisplayName = "RFC 4231 test case 2, hmac-sha2-512")]
    public void Compute_Rfc4231Vector_HashesTheSequenceNumberThenThePacket(string hash, string expected)
    {
        using SshMac mac = new(new HashAlgorithmName(hash), Encoding.ASCII.GetBytes("Jefe"), expected.Length / 2, isEncryptThenMac: false);
        ArrangeTestCase2(hash, expected.Length / 2);

        byte[] result = mac.Compute(What, RestOfTestCase2.AsSpan(0, 5), RestOfTestCase2.AsSpan(5));

        Diagnostics.ActBytes("MAC over a 5-byte then a 19-byte span", result);
        byte[] again = mac.Compute(What, RestOfTestCase2, []);
        Diagnostics.ActBytes("MAC over one span, next packet", again);
        Diagnostics.AssertHex("MAC", expected, result);
        Diagnostics.AssertHex("MAC after the reset", expected, again);
        Assert.AreEqual(expected, Convert.ToHexString(result));
        Assert.AreEqual(expected, Convert.ToHexString(again), "the hash resets after each packet");
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
        ArrangeTestCase2(hash, length);
        Diagnostics.Arrange("HMAC", hmac.GetType().Name);

        byte[] split = mac.Compute(What, RestOfTestCase2.AsSpan(0, 9), RestOfTestCase2.AsSpan(9));
        byte[] whole = mac.Compute(What, RestOfTestCase2, []);

        Diagnostics.ActBytes("MAC over a 9-byte then a 15-byte span", split);
        Diagnostics.ActBytes("MAC over one span, next packet", whole);
        Diagnostics.AssertHex("MAC", expected, split);
        Diagnostics.AssertHex("MAC after the reset", expected, whole);
        Assert.AreEqual(expected, Convert.ToHexString(split));
        Assert.AreEqual(expected, Convert.ToHexString(whole), "the hash resets after each packet");
        mac.Verify(What, RestOfTestCase2, [], Convert.FromHexString(expected));
        Diagnostics.Assert("Verify of the expected MAC", "returns", "returned");
    }

    [TestMethod]
    public void Compute_ShorterLength_SendsTheFirstBytes()
    {
        using SshMac mac = new(HashAlgorithmName.SHA256, Encoding.ASCII.GetBytes("Jefe"), 12, isEncryptThenMac: true);
        ArrangeTestCase2("SHA256", 12);
        Diagnostics.Arrange("encrypt-then-MAC", true);

        byte[] result = mac.Compute(What, RestOfTestCase2, []);

        Diagnostics.ActBytes("MAC", result);
        Diagnostics.AssertHex("MAC", "5BDCC146BF60754E6A042426", result);
        Diagnostics.Assert("length, encrypt-then-MAC", "12, True", $"{mac.Length}, {mac.IsEncryptThenMac}");
        Assert.AreEqual("5BDCC146BF60754E6A042426", Convert.ToHexString(result));
        Assert.AreEqual(12, mac.Length);
        Assert.IsTrue(mac.IsEncryptThenMac);
    }

    [TestMethod]
    public void Verify_MatchingMac_Returns()
    {
        using SshMac mac = new(HashAlgorithmName.SHA256, Encoding.ASCII.GetBytes("Jefe"), 32, isEncryptThenMac: false);
        const string Received = "5BDCC146BF60754E6A042426089575C75A003F089D2739839DEC58B964EC3843";
        ArrangeTestCase2("SHA256", 32);
        Diagnostics.Arrange("received MAC", Received);

        mac.Verify(What, RestOfTestCase2, [], Convert.FromHexString(Received));

        Diagnostics.Act("Verify", "returned");
        Diagnostics.Assert("Verify", "returns without throwing", "returned");
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

        Diagnostics.Arrange("sequence number", $"0x{sequenceNumber:X8}");
        Diagnostics.Arrange("received MAC", Convert.ToHexString(received));

        SshPacketAuthenticationException failure = Assert.ThrowsExactly<SshPacketAuthenticationException>(
            () => mac.Verify(sequenceNumber, RestOfTestCase2, [], received));

        Diagnostics.ActAndAssertThrown(nameof(SshPacketAuthenticationException), failure);
        Diagnostics.Assert("libssh2 error code", -4, failure.Libssh2ErrorCode);
        Assert.AreEqual(-4, failure.Libssh2ErrorCode);
    }

    private void ArrangeTestCase2(string hash, int length)
    {
        Diagnostics.Arrange("hash, MAC length", $"{hash}, {length}");
        Diagnostics.Arrange("key", "\"Jefe\"");
        Diagnostics.Arrange("sequence number", $"0x{What:X8} (\"what\")");
        Diagnostics.Bytes("packet", RestOfTestCase2);
    }
}
