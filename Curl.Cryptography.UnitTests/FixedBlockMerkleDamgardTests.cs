using System.Security.Cryptography;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins the hand-built SHA-1, SHA-256 and SHA-384 compression functions, through
/// <see cref="FixedBlockMerkleDamgard{TCompression}" />, to the BCL's digests for every
/// message length from 0 to 300, and checks that a secret length inside a longer buffer
/// hashes as the shorter message does (ADR-0118, BL-795).
/// </summary>
[TestClass]
public sealed class FixedBlockMerkleDamgardTests
{
    private const int LongestMessage = 300;

    private static readonly byte[] Message = Enumerable.Range(0, LongestMessage + 1).Select(value => (byte)((value * 7) + 3)).ToArray();

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Sha1_EveryLengthUpTo300_MatchesTheBcl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "the BCL's SHA1.HashData over every message length from 0 to 300");
        diagnostics.Bytes("message", Message);
        int compared = 0;
        using (diagnostics.Phase("compare with the BCL"))
        {
            for (int length = 0; length <= LongestMessage; length++)
            {
                byte[] expected = SHA1.HashData(Message.AsSpan(0, length));
                byte[] actual = Hash<Sha1>(length);
                compared++;
                WriteDifference(diagnostics, length, expected, actual);
                CollectionAssert.AreEqual(expected, actual, $"length {length}");
            }
        }

        diagnostics.Act("lengths compared", compared);
        diagnostics.Assert("every digest matches the BCL", true, true);
    }

    [TestMethod]
    public void Sha256_EveryLengthUpTo300_MatchesTheBcl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "the BCL's SHA256.HashData over every message length from 0 to 300");
        diagnostics.Bytes("message", Message);
        int compared = 0;
        using (diagnostics.Phase("compare with the BCL"))
        {
            for (int length = 0; length <= LongestMessage; length++)
            {
                byte[] expected = SHA256.HashData(Message.AsSpan(0, length));
                byte[] actual = Hash<Sha256>(length);
                compared++;
                WriteDifference(diagnostics, length, expected, actual);
                CollectionAssert.AreEqual(expected, actual, $"length {length}");
            }
        }

        diagnostics.Act("lengths compared", compared);
        diagnostics.Assert("every digest matches the BCL", true, true);
    }

    [TestMethod]
    public void Sha384_EveryLengthUpTo300_MatchesTheBcl()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("vector source", "the BCL's SHA384.HashData over every message length from 0 to 300");
        diagnostics.Bytes("message", Message);
        int compared = 0;
        using (diagnostics.Phase("compare with the BCL"))
        {
            for (int length = 0; length <= LongestMessage; length++)
            {
                byte[] expected = SHA384.HashData(Message.AsSpan(0, length));
                byte[] actual = Hash<Sha384>(length);
                compared++;
                WriteDifference(diagnostics, length, expected, actual);
                CollectionAssert.AreEqual(expected, actual, $"length {length}");
            }
        }

        diagnostics.Act("lengths compared", compared);
        diagnostics.Assert("every digest matches the BCL", true, true);
    }

    [TestMethod]
    public void Hash_ASecretLengthInsideALongerBuffer_HashesOnlyThatMany()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] header = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13];
        byte[] digest = new byte[Sha256.HashSize];
        diagnostics.Arrange("vector source", "the BCL's SHA256.HashData over header || message[..length], every seventh length up to 300");
        diagnostics.Bytes("header", header);
        diagnostics.Bytes("message buffer", Message);
        int compared = 0;
        using (diagnostics.Phase("compare with the BCL"))
        {
            for (int length = 0; length <= LongestMessage; length += 7)
            {
                FixedBlockMerkleDamgard<Sha256>.Hash([], header, Message, length, 0, digest);
                byte[] expected = SHA256.HashData([.. header, .. Message.AsSpan(0, length)]);
                compared++;
                WriteDifference(diagnostics, length, expected, digest);

                CollectionAssert.AreEqual(expected, digest, $"length {length}");
            }
        }

        diagnostics.Act("lengths compared", compared);
        diagnostics.Assert("every digest matches the BCL", true, true);
    }

    [TestMethod]
    public void Hash_PrefixBlocks_AreHashedFirst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] prefix = Enumerable.Repeat((byte)0x5a, 2 * Sha384.BlockSize).ToArray();
        byte[] digest = new byte[Sha384.HashSize];
        diagnostics.Arrange("vector source", "the BCL's SHA384.HashData over prefix || message[..200]");
        diagnostics.Bytes("prefix", prefix);
        diagnostics.Arrange("data length", 200);
        diagnostics.Arrange("minimum data length", 150);

        FixedBlockMerkleDamgard<Sha384>.Hash(prefix, [], Message, 200, 150, digest);
        diagnostics.Act("digest", Convert.ToHexString(digest));

        byte[] expected = SHA384.HashData([.. prefix, .. Message.AsSpan(0, 200)]);
        diagnostics.Diff("digest", expected, digest);
        CollectionAssert.AreEqual(expected, digest);
    }

    private static byte[] Hash<TCompression>(int length)
        where TCompression : struct, IBigEndianCompressionFunction
    {
        byte[] digest = new byte[TCompression.HashSize];
        FixedBlockMerkleDamgard<TCompression>.Hash([], [], Message.AsSpan(0, length), length, length, digest);
        return digest;
    }

    private static void WriteDifference(TestDiagnostics diagnostics, int length, byte[] expected, byte[] actual)
    {
        if (!expected.AsSpan().SequenceEqual(actual))
        {
            diagnostics.Arrange("failing length", length);
            diagnostics.Diff("digest", expected, actual);
        }
    }
}
