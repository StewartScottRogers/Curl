using System.Security.Cryptography;
using System.Text;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="DeterministicDsaNonce" /> to the k values of RFC 6979 appendix A.2.1
/// (DSA, 1024 bits, q = 996F967F6C8E388D9E28D01E205FBA957A5698B1): the first candidate
/// below q is the published k.
/// </summary>
[TestClass]
public sealed class DeterministicDsaNonceTests
{
    private static readonly byte[] Subprime = Convert.FromHexString("996F967F6C8E388D9E28D01E205FBA957A5698B1");

    private static readonly byte[] PrivateKey = Convert.FromHexString("411602CB19A6CCC34494D79D98EF1E7ED5AF25F7");

    [TestMethod]
    [DataRow("SHA1", "sample", "7BDB6B0FF756E1BB5D53583EF979082F9AD5BD5B")]
    [DataRow("SHA224", "sample", "562097C06782D60C3037BA7BE104774344687649")]
    [DataRow("SHA256", "sample", "519BA0546D0C39202A7D34D7DFA5E760B318BCFB")]
    [DataRow("SHA384", "sample", "95897CD7BBB944AA932DBC579C1C09EB6FCFC595")]
    [DataRow("SHA512", "sample", "09ECE7CA27D0F5A4DD4E556C9DF1D21D28104F8B")]
    [DataRow("SHA1", "test", "5C842DF4F9E344EE09F056838B42C7A17F4A6433")]
    [DataRow("SHA256", "test", "5A67592E8128E03A417B0484410FB72C0B630E1A")]
    public void NextCandidate_Rfc6979Dsa1024_FirstCandidateBelowQIsPublishedK(string hashName, string message, string expected)
    {
        byte[] hash = Hash(hashName, Encoding.ASCII.GetBytes(message));
        byte[] reducedHash = ReduceModSubprime(hash);
        using DeterministicDsaNonce nonces = new(new HashAlgorithmName(hashName), PrivateKey, reducedHash);
        byte[] candidate = new byte[Subprime.Length];

        do
        {
            nonces.NextCandidate(candidate);
        }
        while (candidate.AsSpan().SequenceCompareTo(Subprime) >= 0);

        Assert.AreEqual(expected, Convert.ToHexString(candidate));
    }

    [TestMethod]
    public void DigestLength_UnsupportedHash_ThrowsArgumentException() =>
        Assert.ThrowsExactly<ArgumentException>(() => DeterministicDsaNonce.DigestLength(HashAlgorithmName.SHA3_256));

    private static byte[] Hash(string hashName, byte[] message)
    {
        if (hashName == "SHA224")
        {
            byte[] digest = new byte[Sha224.HashSize];
            FixedBlockMerkleDamgard<Sha224>.Hash([], [], message, message.Length, message.Length, digest);
            return digest;
        }

        return CryptographicOperations.HashData(new HashAlgorithmName(hashName), message);
    }

    /// <summary>RFC 6979's bits2octets: the leftmost 160 bits, less q once when not below it.</summary>
    private static byte[] ReduceModSubprime(byte[] hash)
    {
        System.Numerics.BigInteger z = new(hash.AsSpan(0, Math.Min(hash.Length, Subprime.Length)), isUnsigned: true, isBigEndian: true);
        System.Numerics.BigInteger q = new(Subprime, isUnsigned: true, isBigEndian: true);
        byte[] reduced = new byte[Subprime.Length];
        (z % q).TryWriteBytes(reduced.AsSpan(Subprime.Length - (z % q).GetByteCount(isUnsigned: true)), out _, isUnsigned: true, isBigEndian: true);
        return reduced;
    }
}
