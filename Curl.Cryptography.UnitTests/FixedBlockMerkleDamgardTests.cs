using System.Security.Cryptography;

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

    [TestMethod]
    public void Sha1_EveryLengthUpTo300_MatchesTheBcl()
    {
        for (int length = 0; length <= LongestMessage; length++)
        {
            CollectionAssert.AreEqual(SHA1.HashData(Message.AsSpan(0, length)), Hash<Sha1>(length), $"length {length}");
        }
    }

    [TestMethod]
    public void Sha256_EveryLengthUpTo300_MatchesTheBcl()
    {
        for (int length = 0; length <= LongestMessage; length++)
        {
            CollectionAssert.AreEqual(SHA256.HashData(Message.AsSpan(0, length)), Hash<Sha256>(length), $"length {length}");
        }
    }

    [TestMethod]
    public void Sha384_EveryLengthUpTo300_MatchesTheBcl()
    {
        for (int length = 0; length <= LongestMessage; length++)
        {
            CollectionAssert.AreEqual(SHA384.HashData(Message.AsSpan(0, length)), Hash<Sha384>(length), $"length {length}");
        }
    }

    [TestMethod]
    public void Hash_ASecretLengthInsideALongerBuffer_HashesOnlyThatMany()
    {
        byte[] header = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13];
        byte[] digest = new byte[Sha256.HashSize];
        for (int length = 0; length <= LongestMessage; length += 7)
        {
            FixedBlockMerkleDamgard<Sha256>.Hash([], header, Message, length, 0, digest);

            CollectionAssert.AreEqual(SHA256.HashData([.. header, .. Message.AsSpan(0, length)]), digest, $"length {length}");
        }
    }

    [TestMethod]
    public void Hash_PrefixBlocks_AreHashedFirst()
    {
        byte[] prefix = Enumerable.Repeat((byte)0x5a, 2 * Sha384.BlockSize).ToArray();
        byte[] digest = new byte[Sha384.HashSize];

        FixedBlockMerkleDamgard<Sha384>.Hash(prefix, [], Message, 200, 150, digest);

        CollectionAssert.AreEqual(SHA384.HashData([.. prefix, .. Message.AsSpan(0, 200)]), digest);
    }

    private static byte[] Hash<TCompression>(int length)
        where TCompression : struct, IBigEndianCompressionFunction
    {
        byte[] digest = new byte[TCompression.HashSize];
        FixedBlockMerkleDamgard<TCompression>.Hash([], [], Message.AsSpan(0, length), length, length, digest);
        return digest;
    }
}
