using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>Pins <see cref="Sha3" /> to NIST's FIPS 202 example values, and SHA3-256 and SHA3-512 to CAVP vectors.</summary>
[TestClass]
public sealed class Sha3Tests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // SHA3AllBytes1-28-16 (CAVS 19.0), sha-3bytetestvectors.zip from
    // https://csrc.nist.gov/projects/cryptographic-algorithm-validation-program/secure-hashing,
    // SHA3_256ShortMsg.rsp and SHA3_512ShortMsg.rsp copied unchanged as .txt: every message
    // of 0 to 136 (SHA3-256's rate) or 72 (SHA3-512's rate) bytes, both sides of the rate.
    [TestMethod]
    public void HashData256_CavpShortMessages_MatchEveryDigest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<(byte[] Message, byte[] Expected)> vectors = CavpShortMessageVectors.Read("sha3-256-short-msg-cavp.txt");
        byte[] digest = new byte[Sha3.Sha3_256HashSize];
        diagnostics.Arrange("source", "NIST CAVP SHA3_256ShortMsg.rsp (CAVS 19.0, FIPS 202)");
        diagnostics.Arrange("vectors", vectors.Count);

        int matched = 0;
        foreach ((byte[] message, byte[] expected) in vectors)
        {
            Sha3.HashData256(message, digest);
            if (!expected.AsSpan().SequenceEqual(digest))
            {
                diagnostics.Bytes("failing message", message);
                diagnostics.Diff("digest", expected, digest);
            }
            else
            {
                matched++;
            }

            CollectionAssert.AreEqual(expected, digest, Convert.ToHexString(message));
        }

        diagnostics.Act("matching digests", matched);
        diagnostics.Assert("vector count", 137, vectors.Count);
        Assert.AreEqual(137, vectors.Count);
    }

    [TestMethod]
    public void HashData512_CavpShortMessages_MatchEveryDigest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<(byte[] Message, byte[] Expected)> vectors = CavpShortMessageVectors.Read("sha3-512-short-msg-cavp.txt");
        byte[] digest = new byte[Sha3.Sha3_512HashSize];
        diagnostics.Arrange("source", "NIST CAVP SHA3_512ShortMsg.rsp (CAVS 19.0, FIPS 202)");
        diagnostics.Arrange("vectors", vectors.Count);

        int matched = 0;
        foreach ((byte[] message, byte[] expected) in vectors)
        {
            Sha3.HashData512(message, digest);
            if (!expected.AsSpan().SequenceEqual(digest))
            {
                diagnostics.Bytes("failing message", message);
                diagnostics.Diff("digest", expected, digest);
            }
            else
            {
                matched++;
            }

            CollectionAssert.AreEqual(expected, digest, Convert.ToHexString(message));
        }

        diagnostics.Act("matching digests", matched);
        diagnostics.Assert("vector count", 73, vectors.Count);
        Assert.AreEqual(73, vectors.Count);
    }

    // NIST "SHA-3 Examples" (Cryptographic Standards and Guidelines, Examples with
    // Intermediate Values): SHA3-256_1600.pdf and SHA3-512_1600.pdf, the 1600-bit message
    // of 200 bytes 0xA3, which crosses a rate boundary.
    [TestMethod]
    public void HashData256_NistExample1600BitMessage_MatchesPublishedDigest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] digest = new byte[Sha3.Sha3_256HashSize];
        diagnostics.Arrange("source", "NIST SHA-3 Examples, SHA3-256_1600.pdf (FIPS 202)");
        diagnostics.Bytes("message", RepeatedA3());

        Sha3.HashData256(RepeatedA3(), digest);
        diagnostics.Act("digest", Convert.ToHexString(digest));

        diagnostics.Diff("digest", "79F38ADEC5C20307A98EF76E8324AFBFD46CFD81B22E3973C65FA1BD9DE31787", Convert.ToHexString(digest));
        Assert.AreEqual("79F38ADEC5C20307A98EF76E8324AFBFD46CFD81B22E3973C65FA1BD9DE31787", Convert.ToHexString(digest));
    }

    [TestMethod]
    public void HashData512_NistExample1600BitMessage_MatchesPublishedDigest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] digest = new byte[Sha3.Sha3_512HashSize];
        const string Expected =
            "E76DFAD22084A8B1467FCF2FFA58361BEC7628EDF5F3FDC0E4805DC48CAEECA8"
            + "1B7C13C30ADF52A3659584739A2DF46BE589C51CA1A4A8416DF6545A1CE8BA00";
        diagnostics.Arrange("source", "NIST SHA-3 Examples, SHA3-512_1600.pdf (FIPS 202)");
        diagnostics.Bytes("message", RepeatedA3());

        Sha3.HashData512(RepeatedA3(), digest);
        diagnostics.Act("digest", Convert.ToHexString(digest));

        diagnostics.Diff("digest", Expected, Convert.ToHexString(digest));
        Assert.AreEqual(
            "E76DFAD22084A8B1467FCF2FFA58361BEC7628EDF5F3FDC0E4805DC48CAEECA8"
            + "1B7C13C30ADF52A3659584739A2DF46BE589C51CA1A4A8416DF6545A1CE8BA00",
            Convert.ToHexString(digest));
    }

    // NIST "SHA-3 Examples": SHA3-224_Msg0.pdf, SHA3-224_1600.pdf, SHA3-384_Msg0.pdf and
    // SHA3-384_1600.pdf (the 1600-bit message crosses both rates), and the "abc" example;
    // OpenSSL 3.5.5's openssl dgst gives the same digests.
    [TestMethod]
    [DataRow("", "6B4E03423667DBB73B6E15454F0EB1ABD4597F9A1B078E3F5B5A6BC7", DisplayName = "Empty")]
    [DataRow("616263", "E642824C3F8CF24AD09234EE7D3C766FC9A3A5168D0C94AD73B46FDF", DisplayName = "abc")]
    public void HashData224_NistExample_MatchesPublishedDigest(string messageHex, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] digest = new byte[Sha3.Sha3_224HashSize];
        diagnostics.Arrange("source", "NIST SHA-3 Examples, SHA3-224 (FIPS 202)");
        diagnostics.Bytes("message", Convert.FromHexString(messageHex));

        Sha3.HashData224(Convert.FromHexString(messageHex), digest);
        diagnostics.Act("digest", Convert.ToHexString(digest));

        diagnostics.Diff("digest", expected, Convert.ToHexString(digest));
        Assert.AreEqual(expected, Convert.ToHexString(digest));
    }

    [TestMethod]
    public void HashData224_NistExample1600BitMessage_MatchesPublishedDigest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] digest = new byte[Sha3.Sha3_224HashSize];
        diagnostics.Arrange("source", "NIST SHA-3 Examples, SHA3-224_1600.pdf (FIPS 202)");
        diagnostics.Bytes("message", RepeatedA3());

        Sha3.HashData224(RepeatedA3(), digest);
        diagnostics.Act("digest", Convert.ToHexString(digest));

        diagnostics.Diff("digest", "9376816ABA503F72F96CE7EB65AC095DEEE3BE4BF9BBC2A1CB7E11E0", Convert.ToHexString(digest));
        Assert.AreEqual("9376816ABA503F72F96CE7EB65AC095DEEE3BE4BF9BBC2A1CB7E11E0", Convert.ToHexString(digest));
    }

    [TestMethod]
    [DataRow("", "0C63A75B845E4F7D01107D852E4C2485C51A50AAAA94FC61995E71BBEE983A2AC3713831264ADB47FB6BD1E058D5F004", DisplayName = "Empty")]
    [DataRow("616263", "EC01498288516FC926459F58E2C6AD8DF9B473CB0FC08C2596DA7CF0E49BE4B298D88CEA927AC7F539F1EDF228376D25", DisplayName = "abc")]
    public void HashData384_NistExample_MatchesPublishedDigest(string messageHex, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] digest = new byte[Sha3.Sha3_384HashSize];
        diagnostics.Arrange("source", "NIST SHA-3 Examples, SHA3-384 (FIPS 202)");
        diagnostics.Bytes("message", Convert.FromHexString(messageHex));

        Sha3.HashData384(Convert.FromHexString(messageHex), digest);
        diagnostics.Act("digest", Convert.ToHexString(digest));

        diagnostics.Diff("digest", expected, Convert.ToHexString(digest));
        Assert.AreEqual(expected, Convert.ToHexString(digest));
    }

    [TestMethod]
    public void HashData384_NistExample1600BitMessage_MatchesPublishedDigest()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] digest = new byte[Sha3.Sha3_384HashSize];
        const string Expected = "1881DE2CA7E41EF95DC4732B8F5F002B189CC1E42B74168ED1732649CE1DBCDD76197A31FD55EE989F2D7050DD473E8F";
        diagnostics.Arrange("source", "NIST SHA-3 Examples, SHA3-384_1600.pdf (FIPS 202)");
        diagnostics.Bytes("message", RepeatedA3());

        Sha3.HashData384(RepeatedA3(), digest);
        diagnostics.Act("digest", Convert.ToHexString(digest));

        diagnostics.Diff("digest", Expected, Convert.ToHexString(digest));
        Assert.AreEqual(
            "1881DE2CA7E41EF95DC4732B8F5F002B189CC1E42B74168ED1732649CE1DBCDD76197A31FD55EE989F2D7050DD473E8F",
            Convert.ToHexString(digest));
    }

    [TestMethod]
    public void HashData224_DestinationNot28Bytes_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] destination = new byte[32];
        diagnostics.Arrange("destination length", destination.Length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData224([], destination));
        diagnostics.Act("exception", exception.Message);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void HashData384_DestinationNot48Bytes_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] destination = new byte[64];
        diagnostics.Arrange("destination length", destination.Length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData384([], destination));
        diagnostics.Act("exception", exception.Message);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void HashData256_DestinationNot32Bytes_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] destination = new byte[31];
        diagnostics.Arrange("destination length", destination.Length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData256([], destination));
        diagnostics.Act("exception", exception.Message);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void HashData512_DestinationNot64Bytes_ThrowsArgumentException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] destination = new byte[32];
        diagnostics.Arrange("destination length", destination.Length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData512([], destination));
        diagnostics.Act("exception", exception.Message);

        diagnostics.Assert("exception type", nameof(ArgumentException), exception.GetType().Name);
    }

    internal static byte[] RepeatedA3()
    {
        byte[] message = new byte[200];
        Array.Fill(message, (byte)0xA3);
        return message;
    }
}
