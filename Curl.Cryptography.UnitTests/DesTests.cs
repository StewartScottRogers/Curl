using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Des" /> to published known answers (NIST SP 500-20's variable plaintext
/// and variable key tests, Grabbe's worked example), to the BCL's <see cref="DES" /> for
/// keys it accepts, to the weak keys it refuses, and checks its caller mistakes and
/// disposal (ADR-0156).
/// </summary>
[TestClass]
public sealed class DesTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    // J. Orlin Grabbe, "The DES Algorithm Illustrated": the worked example.
    [DataRow("133457799BBCDFF1", "0123456789ABCDEF", "85E813540F0AB405")]
    // NIST SP 500-20 table A.1 (variable plaintext, weak key 0101010101010101): rounds 1 and 2.
    [DataRow("0101010101010101", "8000000000000000", "95F8A5E5DD31D900")]
    [DataRow("0101010101010101", "4000000000000000", "DD7F121CA5015619")]
    // NIST SP 500-20 table A.2 (variable key, plaintext zero): the first entry.
    [DataRow("8001010101010101", "0000000000000000", "95A8D72813DAA94D")]
    // The all-zero key: parity bits are ignored, so it is the weak key 0101010101010101.
    [DataRow("0000000000000000", "0000000000000000", "8CA64DE9C1B123A7")]
    public void EncryptBlock_KnownAnswer_GivesThePublishedCiphertext(string key, string plaintext, string ciphertext)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Des des = new(Convert.FromHexString(key));
        byte[] encrypted = new byte[Des.BlockSize];
        byte[] decrypted = new byte[Des.BlockSize];
        diagnostics.Arrange("vector source", "NIST SP 500-20 tables A.1 and A.2, or Grabbe's \"The DES Algorithm Illustrated\"");
        diagnostics.Bytes("key", Convert.FromHexString(key));
        diagnostics.Bytes("plaintext", Convert.FromHexString(plaintext));
        diagnostics.Bytes("ciphertext", Convert.FromHexString(ciphertext));

        des.EncryptBlock(Convert.FromHexString(plaintext), encrypted);
        des.DecryptBlock(encrypted, decrypted);
        diagnostics.Act("encrypted", Convert.ToHexString(encrypted));
        diagnostics.Act("decrypted", Convert.ToHexString(decrypted));

        diagnostics.Diff("encrypted", Convert.FromHexString(ciphertext), encrypted);
        diagnostics.Diff("decrypted", Convert.FromHexString(plaintext), decrypted);
        Assert.AreEqual(ciphertext, Convert.ToHexString(encrypted));
        Assert.AreEqual(plaintext, Convert.ToHexString(decrypted));
    }

    [TestMethod]
    public void EncryptBlock_KeysTheBclAccepts_MatchesTheBclsDes()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Random random = new(684);
        using DES bclDes = DES.Create();
        diagnostics.Arrange("vector source", "the BCL's DES in ECB mode, 64 seeded random keys and blocks (seed 684)");
        int compared = 0;
        using (diagnostics.Phase("compare with the BCL"))
        {
            for (int trial = 0; trial < 64; trial++)
            {
                byte[] key = new byte[Des.KeySize];
                byte[] block = new byte[Des.BlockSize];
                random.NextBytes(key);
                random.NextBytes(block);
                if (DES.IsWeakKey(key) || DES.IsSemiWeakKey(key))
                {
                    continue;
                }

                bclDes.Key = key;
                byte[] expected = bclDes.EncryptEcb(block, PaddingMode.None);
                using Des des = new(key);
                byte[] actual = new byte[Des.BlockSize];
                des.EncryptBlock(block, actual);
                compared++;

                if (!expected.AsSpan().SequenceEqual(actual))
                {
                    diagnostics.Bytes("key", key);
                    diagnostics.Bytes("block", block);
                    diagnostics.Diff("ciphertext", expected, actual);
                }

                CollectionAssert.AreEqual(expected, actual, $"key {Convert.ToHexString(key)}");
            }
        }

        diagnostics.Act("blocks compared", compared);
        diagnostics.Assert("every block matches the BCL", true, true);
    }

    [TestMethod]
    public void EncryptBlock_WeakKey_IsItsOwnInverse()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        // FIPS 74 section 3.6: under a weak key, encryption and decryption are the same.
        using Des des = new(Convert.FromHexString("FEFEFEFEFEFEFEFE"));
        byte[] block = Convert.FromHexString("0123456789ABCDEF");
        byte[] twice = new byte[Des.BlockSize];
        diagnostics.Arrange("vector source", "FIPS 74 section 3.6, weak key");
        diagnostics.Bytes("key", Convert.FromHexString("FEFEFEFEFEFEFEFE"));
        diagnostics.Bytes("block", block);

        des.EncryptBlock(block, twice);
        des.EncryptBlock(twice, twice);
        diagnostics.Act("encrypted twice", Convert.ToHexString(twice));

        diagnostics.Diff("encrypted twice", block, twice);
        CollectionAssert.AreEqual(block, twice);
    }

    [TestMethod]
    [DataRow(7)]
    [DataRow(9)]
    public void Constructor_KeyNotEightBytes_Throws(int length)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("key length", length);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => new Des(new byte[length]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(7, 8)]
    [DataRow(8, 9)]
    public void EncryptBlock_SpanNotOneBlock_Throws(int sourceLength, int destinationLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        using Des des = new(new byte[Des.KeySize]);
        diagnostics.Arrange("source length", sourceLength);
        diagnostics.Arrange("destination length", destinationLength);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => des.EncryptBlock(new byte[sourceLength], new byte[destinationLength]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void DecryptBlock_AfterDispose_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Des des = new(new byte[Des.KeySize]);
        des.Dispose();
        diagnostics.Arrange("disposed", true);

        ObjectDisposedException exception = Assert.ThrowsExactly<ObjectDisposedException>(() => des.DecryptBlock(new byte[Des.BlockSize], new byte[Des.BlockSize]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ObjectDisposedException), exception.GetType().Name);
    }
}
