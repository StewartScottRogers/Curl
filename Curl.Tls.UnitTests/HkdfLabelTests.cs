using System.Security.Cryptography;
using Curl.Testing;

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

    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    // RFC 8448 section 3: the key and iv info of every traffic key derivation, and the
    // info of the Finished key.
    [TestMethod]
    [DataRow("key", 16, "001009746c73313320 6b657900")]
    [DataRow("iv", 12, "000c0874 6c733133206976 00")]
    [DataRow("finished", 32, "00200e746c7331332066696e697368656400")]
    public void EncodeMatchesTheInfoRfc8448Prints(string label, int length, string expected) =>
        Assert.AreEqual(expected.Replace(" ", string.Empty, StringComparison.Ordinal), EncodeToHex(label, [], length, expected.Replace(" ", string.Empty, StringComparison.Ordinal)));

    // RFC 9001 appendix A.1: the labels QUIC gives HKDF-Expand-Label.
    [TestMethod]
    [DataRow("client in", 32, "00200f746c73313320636c69656e7420696e00")]
    [DataRow("server in", 32, "00200f746c7331332073657276657220696e00")]
    [DataRow("quic key", 16, "00100e746c7331332071756963206b657900")]
    [DataRow("quic iv", 12, "000c0d746c733133207175696320697600")]
    [DataRow("quic hp", 16, "00100d746c733133207175696320687000")]
    public void EncodeMatchesTheInfoRfc9001Prints(string label, int length, string expected) =>
        Assert.AreEqual(expected, EncodeToHex(label, [], length, expected));

    // RFC 9001 appendix A.1: initial_secret, then the client's secret, key, iv and hp.
    [TestMethod]
    public void ExpandDerivesQuicClientInitialKeys()
    {
        Diagnostics.Arrange("initial salt", QuicInitialSalt);
        Diagnostics.Arrange("destination connection ID", QuicDestinationConnectionId);
        byte[] initialSecret = HKDF.Extract(HashAlgorithmName.SHA256, Hex(QuicDestinationConnectionId), Hex(QuicInitialSalt));
        byte[] clientSecret = HkdfLabel.Expand(HashAlgorithmName.SHA256, initialSecret, "client in", [], 32);

        AssertHex("initial secret", QuicInitialSecret, initialSecret);
        AssertHex("client secret", "c00cf151ca5be075ed0ebfb5c80323c42d6b7db67881289af4008f1f6c357aea", clientSecret);
        AssertHex("client key", "1f369613dd76d5467730efcbe3b1a22d", HkdfLabel.Expand(HashAlgorithmName.SHA256, clientSecret, "quic key", [], 16));
        AssertHex("client iv", "fa044b2f42a3fd3b46fb255c", HkdfLabel.Expand(HashAlgorithmName.SHA256, clientSecret, "quic iv", [], 12));
        AssertHex("client hp", "9f50449e04a0e810283a1e9933adedd2", HkdfLabel.Expand(HashAlgorithmName.SHA256, clientSecret, "quic hp", [], 16));
    }

    // RFC 9001 appendix A.1: the server's secret, key, iv and hp.
    [TestMethod]
    public void ExpandDerivesQuicServerInitialKeys()
    {
        Diagnostics.Arrange("initial secret", QuicInitialSecret);
        byte[] serverSecret = HkdfLabel.Expand(HashAlgorithmName.SHA256, Hex(QuicInitialSecret), "server in", [], 32);

        AssertHex("server secret", "3c199828fd139efd216c155ad844cc81fb82fa8d7446fa7d78be803acdda951b", serverSecret);
        AssertHex("server key", "cf3a5331653c364c88f0f379b6067e37", HkdfLabel.Expand(HashAlgorithmName.SHA256, serverSecret, "quic key", [], 16));
        AssertHex("server iv", "0ac1493ca1905853b0bba03e", HkdfLabel.Expand(HashAlgorithmName.SHA256, serverSecret, "quic iv", [], 12));
        AssertHex("server hp", "c206b8d9b9f0f37644430b490eeaa314", HkdfLabel.Expand(HashAlgorithmName.SHA256, serverSecret, "quic hp", [], 16));
    }

    [TestMethod]
    public void EncodeCarriesTheContextAfterTheLabel() =>
        Assert.AreEqual("000109746c733133206b657902abcd", EncodeToHex("key", [0xab, 0xcd], 1, "000109746c733133206b657902abcd"));

    [TestMethod]
    public void EncodeRejectsALabelLongerThan255BytesWithItsPrefix()
    {
        Diagnostics.Arrange("inputs", "label of 250 bytes, context empty, length 32");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => HkdfLabel.Encode(new string('a', 250), [], 32));

        WriteRejection(exception, nameof(ArgumentException));
    }

    [TestMethod]
    public void EncodeRejectsAContextLongerThan255Bytes()
    {
        Diagnostics.Arrange("inputs", "label key, context of 256 bytes, length 32");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => HkdfLabel.Encode("key", new byte[256], 32));

        WriteRejection(exception, nameof(ArgumentException));
    }

    [TestMethod]
    public void EncodeRejectsANegativeLength()
    {
        Diagnostics.Arrange("inputs", "label key, context empty, length -1");

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => HkdfLabel.Encode("key", [], -1));

        WriteRejection(exception, nameof(ArgumentOutOfRangeException));
    }

    [TestMethod]
    public void EncodeRejectsALengthPastTwoBytes()
    {
        Diagnostics.Arrange("inputs", "label key, context empty, length 65536");

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => HkdfLabel.Encode("key", [], 65536));

        WriteRejection(exception, nameof(ArgumentOutOfRangeException));
    }

    [TestMethod]
    public void EncodeRejectsANullLabel()
    {
        Diagnostics.Arrange("inputs", "label null, context empty, length 32");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => HkdfLabel.Encode(null!, [], 32));

        WriteRejection(exception, nameof(ArgumentNullException));
    }

    [TestMethod]
    public void ExpandRejectsANullSecret()
    {
        Diagnostics.Arrange("inputs", "SHA-256, secret null, label key, context empty, length 16");

        ArgumentNullException exception = Assert.ThrowsExactly<ArgumentNullException>(() => HkdfLabel.Expand(HashAlgorithmName.SHA256, null!, "key", [], 16));

        WriteRejection(exception, nameof(ArgumentNullException));
    }

    private static byte[] Hex(string hex) => Convert.FromHexString(hex);

    /// <summary>
    /// Encodes the HkdfLabel and returns it as lower-case hex, writing the inputs, the encoded bytes and
    /// their difference from <paramref name="expectedHex"/> as diagnostics.
    /// </summary>
    private string EncodeToHex(string label, byte[] context, int length, string expectedHex)
    {
        Diagnostics.Arrange("label", label);
        Diagnostics.Arrange("context", Convert.ToHexStringLower(context));
        Diagnostics.Arrange("length", length);

        byte[] encoded = HkdfLabel.Encode(label, context, length);

        Diagnostics.Bytes("HkdfLabel", encoded);
        Diagnostics.Act("HkdfLabel", Convert.ToHexStringLower(encoded));
        Diagnostics.Diff("HkdfLabel", Hex(expectedHex), encoded);
        return Convert.ToHexStringLower(encoded);
    }

    private void AssertHex(string label, string expected, byte[] actual)
    {
        Diagnostics.Act(label, Convert.ToHexStringLower(actual));
        Diagnostics.Diff(label, Hex(expected), actual);
        Assert.AreEqual(expected, Convert.ToHexStringLower(actual));
    }

    private void WriteRejection(Exception exception, string expectedType)
    {
        Diagnostics.Act("exception", exception.Message);
        Diagnostics.Assert("exception type", expectedType, exception.GetType().Name);
    }
}
