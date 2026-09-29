namespace Curl.Cryptography;

/// <summary>Pins <see cref="Shake" /> to NIST's FIPS 202 example values and CAVP vectors.</summary>
[TestClass]
public sealed class ShakeTests
{
    // SHAKE3AllBytesGT (CAVS 19.0), shakebytetestvectors.zip from
    // https://csrc.nist.gov/projects/cryptographic-algorithm-validation-program/secure-hashing,
    // SHAKE128ShortMsg.rsp and SHAKE256ShortMsg.rsp copied unchanged as .txt: every message
    // of 0 to 336 bytes (twice SHAKE128's rate) or 0 to 272 bytes (twice SHAKE256's rate), with
    // 128-bit and 256-bit outputs.
    [TestMethod]
    public void HashData128_CavpShortMessages_MatchEveryOutput()
    {
        IReadOnlyList<(byte[] Message, byte[] Expected)> vectors = CavpShortMessageVectors.Read("shake128-short-msg-cavp.txt");

        foreach ((byte[] message, byte[] expected) in vectors)
        {
            byte[] output = new byte[expected.Length];
            Shake.HashData128(message, output);
            CollectionAssert.AreEqual(expected, output, Convert.ToHexString(message));
        }

        Assert.AreEqual(337, vectors.Count);
    }

    [TestMethod]
    public void HashData256_CavpShortMessages_MatchEveryOutput()
    {
        IReadOnlyList<(byte[] Message, byte[] Expected)> vectors = CavpShortMessageVectors.Read("shake256-short-msg-cavp.txt");

        foreach ((byte[] message, byte[] expected) in vectors)
        {
            byte[] output = new byte[expected.Length];
            Shake.HashData256(message, output);
            CollectionAssert.AreEqual(expected, output, Convert.ToHexString(message));
        }

        Assert.AreEqual(273, vectors.Count);
    }

    // NIST "SHA-3 Examples": SHAKE128_Msg1600.pdf and SHAKE256_Msg1600.pdf, the 1600-bit
    // message of 200 bytes 0xA3; the first 64 bytes of the published 4096-bit output.
    [TestMethod]
    public void HashData128_NistExample1600BitMessage_MatchesPublishedOutput()
    {
        byte[] output = new byte[64];

        Shake.HashData128(Sha3Tests.RepeatedA3(), output);

        Assert.AreEqual(
            "131AB8D2B594946B9C81333F9BB6E0CE75C3B93104FA3469D3917457385DA037"
            + "CF232EF7164A6D1EB448C8908186AD852D3F85A5CF28DA1AB6FE343817197846",
            Convert.ToHexString(output));
    }

    [TestMethod]
    public void HashData256_NistExample1600BitMessage_MatchesPublishedOutput()
    {
        byte[] output = new byte[64];

        Shake.HashData256(Sha3Tests.RepeatedA3(), output);

        Assert.AreEqual(
            "CD8A920ED141AA0407A22D59288652E9D9F1A7EE0C1E7C1CA699424DA84A904D"
            + "2D700CAAE7396ECE96604440577DA4F3AA22AEB8857F961C4CD8E06F0AE6610B",
            Convert.ToHexString(output));
    }

    [TestMethod]
    public void Read_InPiecesAcrossTheRate_MatchesOneShotOutput()
    {
        byte[] expected = new byte[500];
        Shake.HashData128(Sha3Tests.RepeatedA3(), expected);
        using Shake shake = Shake.Create128();
        byte[] actual = new byte[500];

        shake.AppendData(Sha3Tests.RepeatedA3().AsSpan(0, 7));
        shake.AppendData(Sha3Tests.RepeatedA3().AsSpan(7));
        shake.Read(actual.AsSpan(0, 1));
        shake.Read(actual.AsSpan(1, 200));
        shake.Read(actual.AsSpan(201));

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Reset_AfterRead_StartsAFreshComputation()
    {
        byte[] expected = new byte[32];
        Shake.HashData256([1, 2, 3], expected);
        using Shake shake = Shake.Create256();
        byte[] actual = new byte[32];
        shake.AppendData([9]);
        shake.Read(actual);

        shake.Reset();
        shake.AppendData([1, 2, 3]);
        shake.Read(actual);

        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void AppendData_AfterRead_ThrowsInvalidOperationException()
    {
        using Shake shake = Shake.Create128();
        shake.Read(new byte[1]);

        Assert.ThrowsExactly<InvalidOperationException>(() => shake.AppendData([1]));
    }

    [TestMethod]
    public void EveryMember_AfterDispose_ThrowsObjectDisposedException()
    {
        Shake shake = Shake.Create256();
        shake.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => shake.AppendData([1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => shake.Read(new byte[1]));
        Assert.ThrowsExactly<ObjectDisposedException>(shake.Reset);
    }
}
