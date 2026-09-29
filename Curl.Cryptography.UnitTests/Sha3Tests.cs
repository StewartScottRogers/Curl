namespace Curl.Cryptography;

/// <summary>Pins <see cref="Sha3" /> to NIST's FIPS 202 example values and CAVP vectors.</summary>
[TestClass]
public sealed class Sha3Tests
{
    // SHA3AllBytes1-28-16 (CAVS 19.0), sha-3bytetestvectors.zip from
    // https://csrc.nist.gov/projects/cryptographic-algorithm-validation-program/secure-hashing,
    // SHA3_256ShortMsg.rsp and SHA3_512ShortMsg.rsp copied unchanged as .txt: every message
    // of 0 to 136 (SHA3-256's rate) or 72 (SHA3-512's rate) bytes, both sides of the rate.
    [TestMethod]
    public void HashData256_CavpShortMessages_MatchEveryDigest()
    {
        IReadOnlyList<(byte[] Message, byte[] Expected)> vectors = CavpShortMessageVectors.Read("sha3-256-short-msg-cavp.txt");
        byte[] digest = new byte[Sha3.Sha3_256HashSize];

        foreach ((byte[] message, byte[] expected) in vectors)
        {
            Sha3.HashData256(message, digest);
            CollectionAssert.AreEqual(expected, digest, Convert.ToHexString(message));
        }

        Assert.AreEqual(137, vectors.Count);
    }

    [TestMethod]
    public void HashData512_CavpShortMessages_MatchEveryDigest()
    {
        IReadOnlyList<(byte[] Message, byte[] Expected)> vectors = CavpShortMessageVectors.Read("sha3-512-short-msg-cavp.txt");
        byte[] digest = new byte[Sha3.Sha3_512HashSize];

        foreach ((byte[] message, byte[] expected) in vectors)
        {
            Sha3.HashData512(message, digest);
            CollectionAssert.AreEqual(expected, digest, Convert.ToHexString(message));
        }

        Assert.AreEqual(73, vectors.Count);
    }

    // NIST "SHA-3 Examples" (Cryptographic Standards and Guidelines, Examples with
    // Intermediate Values): SHA3-256_1600.pdf and SHA3-512_1600.pdf, the 1600-bit message
    // of 200 bytes 0xA3, which crosses a rate boundary.
    [TestMethod]
    public void HashData256_NistExample1600BitMessage_MatchesPublishedDigest()
    {
        byte[] digest = new byte[Sha3.Sha3_256HashSize];

        Sha3.HashData256(RepeatedA3(), digest);

        Assert.AreEqual("79F38ADEC5C20307A98EF76E8324AFBFD46CFD81B22E3973C65FA1BD9DE31787", Convert.ToHexString(digest));
    }

    [TestMethod]
    public void HashData512_NistExample1600BitMessage_MatchesPublishedDigest()
    {
        byte[] digest = new byte[Sha3.Sha3_512HashSize];

        Sha3.HashData512(RepeatedA3(), digest);

        Assert.AreEqual(
            "E76DFAD22084A8B1467FCF2FFA58361BEC7628EDF5F3FDC0E4805DC48CAEECA8"
            + "1B7C13C30ADF52A3659584739A2DF46BE589C51CA1A4A8416DF6545A1CE8BA00",
            Convert.ToHexString(digest));
    }

    [TestMethod]
    public void HashData256_DestinationNot32Bytes_ThrowsArgumentException()
    {
        byte[] destination = new byte[31];

        Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData256([], destination));
    }

    [TestMethod]
    public void HashData512_DestinationNot64Bytes_ThrowsArgumentException()
    {
        byte[] destination = new byte[32];

        Assert.ThrowsExactly<ArgumentException>(() => Sha3.HashData512([], destination));
    }

    internal static byte[] RepeatedA3()
    {
        byte[] message = new byte[200];
        Array.Fill(message, (byte)0xA3);
        return message;
    }
}
