using System.Security.Cryptography;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="FixedBlockHmac" /> to the BCL's HMAC-SHA1, HMAC-SHA256 and HMAC-SHA384
/// over <c>header || data[..dataLength]</c> for every secret length a TLS CBC record's
/// padding allows, with short and long keys, and checks its argument guards (BL-795).
/// </summary>
[TestClass]
public sealed class FixedBlockHmacTests
{
    private static readonly byte[] Header = [0, 0, 0, 0, 0, 0, 0, 7, 23, 3, 3, 1, 2];

    private static readonly byte[] Data = Enumerable.Range(0, 400).Select(value => (byte)(value ^ 0xa5)).ToArray();

    [TestMethod]
    [DataRow("SHA1", 20)]
    [DataRow("SHA256", 32)]
    [DataRow("SHA384", 48)]
    [DataRow("SHA1", 200)]
    [DataRow("SHA384", 200)]
    public void Compute_EveryLengthOfTheLast256_MatchesTheBclHmac(string hashName, int keyLength)
    {
        HashAlgorithmName hash = new(hashName);
        byte[] key = Enumerable.Range(1, keyLength).Select(value => (byte)value).ToArray();
        byte[] mac = new byte[HmacLength(hash)];
        int minimum = Data.Length - 256;
        for (int length = minimum; length <= Data.Length; length++)
        {
            FixedBlockHmac.Compute(hash, key, Header, Data, length, minimum, mac);

            CollectionAssert.AreEqual(CryptographicOperations.HmacData(hash, key, (byte[])[.. Header, .. Data.AsSpan(0, length)]), mac, $"length {length}");
        }
    }

    [TestMethod]
    public void Compute_AnUnsupportedHash_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => FixedBlockHmac.Compute(HashAlgorithmName.MD5, [1], [], [], 0, 0, new byte[16]));
    }

    [TestMethod]
    public void Compute_AWrongDestinationLength_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() => FixedBlockHmac.Compute(HashAlgorithmName.SHA256, [1], [], [], 0, 0, new byte[20]));
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(3, 0)]
    [DataRow(1, 2)]
    [DataRow(0, -1)]
    [DataRow(0, 3)]
    public void Compute_ALengthOutOfRange_Throws(int dataLength, int minimumDataLength)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => FixedBlockHmac.Compute(HashAlgorithmName.SHA1, [1], [], [1, 2], dataLength, minimumDataLength, new byte[20]));
    }

    private static int HmacLength(HashAlgorithmName hash) => CryptographicOperations.HmacData(hash, [1], []).Length;
}
