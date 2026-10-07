using System.Security.Cryptography;
using Curl.Testing;

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

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("SHA1", 20)]
    [DataRow("SHA256", 32)]
    [DataRow("SHA384", 48)]
    [DataRow("SHA1", 200)]
    [DataRow("SHA384", 200)]
    public void Compute_EveryLengthOfTheLast256_MatchesTheBclHmac(string hashName, int keyLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        HashAlgorithmName hash = new(hashName);
        byte[] key = Enumerable.Range(1, keyLength).Select(value => (byte)value).ToArray();
        byte[] mac = new byte[HmacLength(hash)];
        int minimum = Data.Length - 256;
        diagnostics.Arrange("vector source", "the BCL's CryptographicOperations.HmacData over header || data[..length]");
        diagnostics.Arrange("hash", hashName);
        diagnostics.Arrange("data lengths", $"{minimum} to {Data.Length}");
        diagnostics.Bytes("key", key);
        diagnostics.Bytes("header", Header);
        int compared = 0;
        using (diagnostics.Phase("compare with the BCL"))
        {
            for (int length = minimum; length <= Data.Length; length++)
            {
                FixedBlockHmac.Compute(hash, key, Header, Data, length, minimum, mac);
                byte[] expected = CryptographicOperations.HmacData(hash, key, (byte[])[.. Header, .. Data.AsSpan(0, length)]);
                compared++;
                if (!expected.AsSpan().SequenceEqual(mac))
                {
                    diagnostics.Arrange("failing length", length);
                    diagnostics.Diff("mac", expected, mac);
                }

                CollectionAssert.AreEqual(expected, mac, $"length {length}");
            }
        }

        diagnostics.Act("lengths compared", compared);
        diagnostics.Assert("every mac matches the BCL", true, true);
    }

    [TestMethod]
    public void Compute_AnUnsupportedHash_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("hash", "MD5");

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => FixedBlockHmac.Compute(HashAlgorithmName.MD5, [1], [], [], 0, 0, new byte[16]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    public void Compute_AWrongDestinationLength_Throws()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("hash", "SHA256");
        diagnostics.Arrange("destination length", 20);

        ArgumentException exception = Assert.ThrowsExactly<ArgumentException>(() => FixedBlockHmac.Compute(HashAlgorithmName.SHA256, [1], [], [], 0, 0, new byte[20]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentException), exception.GetType().Name);
    }

    [TestMethod]
    [DataRow(-1, 0)]
    [DataRow(3, 0)]
    [DataRow(1, 2)]
    [DataRow(0, -1)]
    [DataRow(0, 3)]
    public void Compute_ALengthOutOfRange_Throws(int dataLength, int minimumDataLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("data buffer length", 2);
        diagnostics.Arrange("data length", dataLength);
        diagnostics.Arrange("minimum data length", minimumDataLength);

        ArgumentOutOfRangeException exception = Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => FixedBlockHmac.Compute(HashAlgorithmName.SHA1, [1], [], [1, 2], dataLength, minimumDataLength, new byte[20]));
        diagnostics.Act("exception", exception.GetType().Name);

        diagnostics.Assert("exception", nameof(ArgumentOutOfRangeException), exception.GetType().Name);
    }

    private static int HmacLength(HashAlgorithmName hash) => CryptographicOperations.HmacData(hash, [1], []).Length;
}
