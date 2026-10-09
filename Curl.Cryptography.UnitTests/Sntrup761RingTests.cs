using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Sntrup761Ring" />'s core decryption to the NTRU Prime round-3 reference
/// where the known-answer vectors never reach: the fixed weight-w fallback it returns for a
/// ciphertext whose decryption does not have weight w (AF-0119).
/// </summary>
[TestClass]
public sealed class Sntrup761RingTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    // Round 3 Decrypt: when r = e / g does not have weight w, the result is
    // (1, ..., 1, 0, ..., 0) with exactly w ones. An all-zero ciphertext decrypts to the
    // zero polynomial, weight 0, so it takes the fallback whatever f and 1/g are.
    [TestMethod]
    public void Decrypt_DecryptionWithoutWeightW_GivesWOnesThenZeros()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        short[] ciphertext = new short[Sntrup761Ring.P];
        short[] f = new short[Sntrup761Ring.P];
        short[] gReciprocal = new short[Sntrup761Ring.P];
        f[0] = 1;
        gReciprocal[0] = 1;
        short[] result = new short[Sntrup761Ring.P];
        diagnostics.Arrange("ciphertext", "all zero, decrypting to weight 0");

        Sntrup761Ring.Decrypt(result, ciphertext, f, gReciprocal);

        int lastOne = Array.LastIndexOf(result, (short)1);
        diagnostics.Act("index of the last 1", lastOne);
        diagnostics.Assert("index of the last 1", Sntrup761Ring.W - 1, lastOne);
        short[] expected = [.. Enumerable.Repeat((short)1, Sntrup761Ring.W), .. new short[Sntrup761Ring.P - Sntrup761Ring.W]];
        CollectionAssert.AreEqual(expected, result);
    }
}
