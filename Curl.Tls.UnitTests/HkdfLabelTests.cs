using System.Security.Cryptography;

namespace Curl.Tls;

/// <summary>
/// HKDF-Expand-Label: the info blocks RFC 8448 and RFC 9001 print, QUIC's initial keys
/// from RFC 9001 appendix A.1, and the limits of the structure.
/// </summary>
[TestClass]
public sealed class HkdfLabelTests
{
    // RFC 9001 section 5.2: the version 1 initial salt; appendix A: the client's
    // Destination Connection ID.
    private const string QuicInitialSalt = "38762cf7f55934b34d179ae6a4c80cadccbb7f0a";
    private const string QuicDestinationConnectionId = "8394c8f03e515708";
    private const string QuicInitialSecret = "7db5df06e7a69e432496adedb00851923595221596ae2ae9fb8115c1e9ed0a44";

    // RFC 8448 section 3: the key and iv info of every traffic key derivation, and the
    // info of the Finished key.
    [TestMethod]
    [DataRow("key", 16, "001009746c73313320 6b657900")]
    [DataRow("iv", 12, "000c0874 6c733133206976 00")]
    [DataRow("finished", 32, "00200e746c7331332066696e697368656400")]
    public void EncodeMatchesTheInfoRfc8448Prints(string label, int length, string expected) =>
        Assert.AreEqual(expected.Replace(" ", string.Empty, StringComparison.Ordinal), Convert.ToHexStringLower(HkdfLabel.Encode(label, [], length)));

    // RFC 9001 appendix A.1: the labels QUIC gives HKDF-Expand-Label.
    [TestMethod]
    [DataRow("client in", 32, "00200f746c73313320636c69656e7420696e00")]
    [DataRow("server in", 32, "00200f746c7331332073657276657220696e00")]
    [DataRow("quic key", 16, "00100e746c7331332071756963206b657900")]
    [DataRow("quic iv", 12, "000c0d746c733133207175696320697600")]
    [DataRow("quic hp", 16, "00100d746c733133207175696320687000")]
    public void EncodeMatchesTheInfoRfc9001Prints(string label, int length, string expected) =>
        Assert.AreEqual(expected, Convert.ToHexStringLower(HkdfLabel.Encode(label, [], length)));

    // RFC 9001 appendix A.1: initial_secret, then the client's secret, key, iv and hp.
    [TestMethod]
    public void ExpandDerivesQuicClientInitialKeys()
    {
        byte[] initialSecret = HKDF.Extract(HashAlgorithmName.SHA256, Hex(QuicDestinationConnectionId), Hex(QuicInitialSalt));
        byte[] clientSecret = HkdfLabel.Expand(HashAlgorithmName.SHA256, initialSecret, "client in", [], 32);

        AssertHex(QuicInitialSecret, initialSecret);
        AssertHex("c00cf151ca5be075ed0ebfb5c80323c42d6b7db67881289af4008f1f6c357aea", clientSecret);
        AssertHex("1f369613dd76d5467730efcbe3b1a22d", HkdfLabel.Expand(HashAlgorithmName.SHA256, clientSecret, "quic key", [], 16));
        AssertHex("fa044b2f42a3fd3b46fb255c", HkdfLabel.Expand(HashAlgorithmName.SHA256, clientSecret, "quic iv", [], 12));
        AssertHex("9f50449e04a0e810283a1e9933adedd2", HkdfLabel.Expand(HashAlgorithmName.SHA256, clientSecret, "quic hp", [], 16));
    }

    // RFC 9001 appendix A.1: the server's secret, key, iv and hp.
    [TestMethod]
    public void ExpandDerivesQuicServerInitialKeys()
    {
        byte[] serverSecret = HkdfLabel.Expand(HashAlgorithmName.SHA256, Hex(QuicInitialSecret), "server in", [], 32);

        AssertHex("3c199828fd139efd216c155ad844cc81fb82fa8d7446fa7d78be803acdda951b", serverSecret);
        AssertHex("cf3a5331653c364c88f0f379b6067e37", HkdfLabel.Expand(HashAlgorithmName.SHA256, serverSecret, "quic key", [], 16));
        AssertHex("0ac1493ca1905853b0bba03e", HkdfLabel.Expand(HashAlgorithmName.SHA256, serverSecret, "quic iv", [], 12));
        AssertHex("c206b8d9b9f0f37644430b490eeaa314", HkdfLabel.Expand(HashAlgorithmName.SHA256, serverSecret, "quic hp", [], 16));
    }

    [TestMethod]
    public void EncodeCarriesTheContextAfterTheLabel() =>
        Assert.AreEqual("000109746c733133206b657902abcd", Convert.ToHexStringLower(HkdfLabel.Encode("key", [0xab, 0xcd], 1)));

    [TestMethod]
    public void EncodeRejectsALabelLongerThan255BytesWithItsPrefix() =>
        Assert.ThrowsExactly<ArgumentException>(() => HkdfLabel.Encode(new string('a', 250), [], 32));

    [TestMethod]
    public void EncodeRejectsAContextLongerThan255Bytes() =>
        Assert.ThrowsExactly<ArgumentException>(() => HkdfLabel.Encode("key", new byte[256], 32));

    [TestMethod]
    public void EncodeRejectsANegativeLength() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => HkdfLabel.Encode("key", [], -1));

    [TestMethod]
    public void EncodeRejectsALengthPastTwoBytes() =>
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => HkdfLabel.Encode("key", [], 65536));

    [TestMethod]
    public void EncodeRejectsANullLabel() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => HkdfLabel.Encode(null!, [], 32));

    [TestMethod]
    public void ExpandRejectsANullSecret() =>
        Assert.ThrowsExactly<ArgumentNullException>(() => HkdfLabel.Expand(HashAlgorithmName.SHA256, null!, "key", [], 16));

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    private static void AssertHex(string expected, byte[] actual) => Assert.AreEqual(expected, Convert.ToHexStringLower(actual));
}
