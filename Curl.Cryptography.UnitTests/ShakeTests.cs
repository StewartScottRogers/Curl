using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>Pins <see cref="Shake" /> to NIST's FIPS 202 example values and CAVP vectors.</summary>
[TestClass]
public sealed class ShakeTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // SHAKE3AllBytesGT (CAVS 19.0), shakebytetestvectors.zip from
    // https://csrc.nist.gov/projects/cryptographic-algorithm-validation-program/secure-hashing,
    // SHAKE128ShortMsg.rsp and SHAKE256ShortMsg.rsp copied unchanged as .txt: every message
    // of 0 to 336 bytes (twice SHAKE128's rate) or 0 to 272 bytes (twice SHAKE256's rate), with
    // 128-bit and 256-bit outputs.
    [TestMethod]
    public void HashData128_CavpShortMessages_MatchEveryOutput()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<(byte[] Message, byte[] Expected)> vectors = CavpShortMessageVectors.Read("shake128-short-msg-cavp.txt");
        diagnostics.Arrange("source", "NIST CAVP SHAKE128ShortMsg.rsp (CAVS 19.0, FIPS 202)");
        diagnostics.Arrange("vectors", vectors.Count);

        int matched = 0;
        foreach ((byte[] message, byte[] expected) in vectors)
        {
            byte[] output = new byte[expected.Length];
            Shake.HashData128(message, output);
            if (!expected.AsSpan().SequenceEqual(output))
            {
                diagnostics.Bytes("failing message", message);
                diagnostics.Diff("output", expected, output);
            }
            else
            {
                matched++;
            }

            CollectionAssert.AreEqual(expected, output, Convert.ToHexString(message));
        }

        diagnostics.Act("matching outputs", matched);
        diagnostics.Assert("vector count", 337, vectors.Count);
        Assert.AreEqual(337, vectors.Count);
    }

    [TestMethod]
    public void HashData256_CavpShortMessages_MatchEveryOutput()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        IReadOnlyList<(byte[] Message, byte[] Expected)> vectors = CavpShortMessageVectors.Read("shake256-short-msg-cavp.txt");
        diagnostics.Arrange("source", "NIST CAVP SHAKE256ShortMsg.rsp (CAVS 19.0, FIPS 202)");
        diagnostics.Arrange("vectors", vectors.Count);

        int matched = 0;
        foreach ((byte[] message, byte[] expected) in vectors)
        {
            byte[] output = new byte[expected.Length];
            Shake.HashData256(message, output);
            if (!expected.AsSpan().SequenceEqual(output))
            {
                diagnostics.Bytes("failing message", message);
                diagnostics.Diff("output", expected, output);
            }
            else
            {
                matched++;
            }

            CollectionAssert.AreEqual(expected, output, Convert.ToHexString(message));
        }

        diagnostics.Act("matching outputs", matched);
        diagnostics.Assert("vector count", 273, vectors.Count);
        Assert.AreEqual(273, vectors.Count);
    }

    // NIST "SHA-3 Examples": SHAKE128_Msg1600.pdf and SHAKE256_Msg1600.pdf, the 1600-bit
    // message of 200 bytes 0xA3; the first 64 bytes of the published 4096-bit output.
    [TestMethod]
    public void HashData128_NistExample1600BitMessage_MatchesPublishedOutput()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] output = new byte[64];
        const string Expected =
            "131AB8D2B594946B9C81333F9BB6E0CE75C3B93104FA3469D3917457385DA037"
            + "CF232EF7164A6D1EB448C8908186AD852D3F85A5CF28DA1AB6FE343817197846";
        diagnostics.Arrange("source", "NIST SHA-3 Examples, SHAKE128_Msg1600.pdf (FIPS 202), first 64 output bytes");
        diagnostics.Bytes("message", Sha3Tests.RepeatedA3());

        Shake.HashData128(Sha3Tests.RepeatedA3(), output);
        diagnostics.Act("output", Convert.ToHexString(output));

        diagnostics.Diff("output", Expected, Convert.ToHexString(output));
        Assert.AreEqual(
            "131AB8D2B594946B9C81333F9BB6E0CE75C3B93104FA3469D3917457385DA037"
            + "CF232EF7164A6D1EB448C8908186AD852D3F85A5CF28DA1AB6FE343817197846",
            Convert.ToHexString(output));
    }

    [TestMethod]
    public void HashData256_NistExample1600BitMessage_MatchesPublishedOutput()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] output = new byte[64];
        const string Expected =
            "CD8A920ED141AA0407A22D59288652E9D9F1A7EE0C1E7C1CA699424DA84A904D"
            + "2D700CAAE7396ECE96604440577DA4F3AA22AEB8857F961C4CD8E06F0AE6610B";
        diagnostics.Arrange("source", "NIST SHA-3 Examples, SHAKE256_Msg1600.pdf (FIPS 202), first 64 output bytes");
        diagnostics.Bytes("message", Sha3Tests.RepeatedA3());

        Shake.HashData256(Sha3Tests.RepeatedA3(), output);
        diagnostics.Act("output", Convert.ToHexString(output));

        diagnostics.Diff("output", Expected, Convert.ToHexString(output));
        Assert.AreEqual(
            "CD8A920ED141AA0407A22D59288652E9D9F1A7EE0C1E7C1CA699424DA84A904D"
            + "2D700CAAE7396ECE96604440577DA4F3AA22AEB8857F961C4CD8E06F0AE6610B",
            Convert.ToHexString(output));
    }

    [TestMethod]
    public void Read_InPiecesAcrossTheRate_MatchesOneShotOutput()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] expected = new byte[500];
        Shake.HashData128(Sha3Tests.RepeatedA3(), expected);
        using Shake shake = Shake.Create128();
        byte[] actual = new byte[500];
        diagnostics.Arrange("source", "200 bytes 0xA3 (NIST SHAKE128_Msg1600.pdf message), one-shot output as reference");
        diagnostics.Arrange("appends", "7 bytes, then 193 bytes");
        diagnostics.Arrange("reads", "1 byte, 200 bytes, then 299 bytes");

        shake.AppendData(Sha3Tests.RepeatedA3().AsSpan(0, 7));
        shake.AppendData(Sha3Tests.RepeatedA3().AsSpan(7));
        shake.Read(actual.AsSpan(0, 1));
        shake.Read(actual.AsSpan(1, 200));
        shake.Read(actual.AsSpan(201));
        diagnostics.Act("output", Convert.ToHexString(actual));

        diagnostics.Diff("output", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Reset_AfterRead_StartsAFreshComputation()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] expected = new byte[32];
        Shake.HashData256([1, 2, 3], expected);
        using Shake shake = Shake.Create256();
        byte[] actual = new byte[32];
        shake.AppendData([9]);
        shake.Read(actual);
        diagnostics.Arrange("before reset", "SHAKE256 of 09, 32 bytes read");
        diagnostics.Bytes("message after reset", [1, 2, 3]);

        shake.Reset();
        shake.AppendData([1, 2, 3]);
        shake.Read(actual);
        diagnostics.Act("output", Convert.ToHexString(actual));

        diagnostics.Diff("output", expected, actual);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void AppendData_AfterRead_ThrowsInvalidOperationException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Shake shake = Shake.Create128();
        shake.Read(new byte[1]);
        diagnostics.Arrange("state", "SHAKE128 after reading 1 byte");

        InvalidOperationException exception = Assert.ThrowsExactly<InvalidOperationException>(() => shake.AppendData([1]));
        diagnostics.Act("exception", exception.Message);

        diagnostics.Assert("exception type", nameof(InvalidOperationException), exception.GetType().Name);
    }

    [TestMethod]
    public void EveryMember_AfterDispose_ThrowsObjectDisposedException()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Shake shake = Shake.Create256();
        shake.Dispose();
        diagnostics.Arrange("state", "SHAKE256 disposed");

        ObjectDisposedException append = Assert.ThrowsExactly<ObjectDisposedException>(() => shake.AppendData([1]));
        ObjectDisposedException read = Assert.ThrowsExactly<ObjectDisposedException>(() => shake.Read(new byte[1]));
        ObjectDisposedException reset = Assert.ThrowsExactly<ObjectDisposedException>(shake.Reset);
        diagnostics.Act("AppendData exception", append.GetType().Name);
        diagnostics.Act("Read exception", read.GetType().Name);
        diagnostics.Act("Reset exception", reset.GetType().Name);

        diagnostics.Assert("exception type", nameof(ObjectDisposedException), reset.GetType().Name);
    }
}
