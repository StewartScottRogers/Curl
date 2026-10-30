namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Blake2s" /> to RFC 7693 Appendix B and to the official keyed known-answer
/// vectors (BLAKE2 reference repository, <c>testvectors/blake2s-kat.txt</c>: key 00..1f,
/// input the first n bytes of 00 01 02 ...).
/// </summary>
[TestClass]
public sealed class Blake2sTests
{
    private const string KatKey = "000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f";

    [TestMethod]
    public void HashData_Abc_MatchesRfc7693AppendixB()
    {
        Span<byte> digest = stackalloc byte[Blake2s.HashSize];

        Blake2s.HashData("abc"u8, digest);

        Assert.AreEqual("508c5e8c327c14e2e1a72ba34eeb452f37458b209ed63a294d999b4c86675982", Convert.ToHexStringLower(digest));
    }

    [TestMethod]
    [DataRow(0, "48a8997da407876b3d79c0d92325ad3b89cbb754d86ab71aee047ad345fd2c49")]
    [DataRow(1, "40d15fee7c328830166ac3f918650f807e7e01e177258cdc0a39b11f598066f1")]
    [DataRow(63, "c65382513f07460da39833cb666c5ed82e61b9e998f4b0c4287cee56c3cc9bcd")]
    [DataRow(64, "8975b0577fd35566d750b362b0897a26c399136df07bababbde6203ff2954ed4")]
    [DataRow(65, "21fe0ceb0052be7fb0f004187cacd7de67fa6eb0938d927677f2398c132317a8")]
    [DataRow(255, "3fb735061abc519dfe979e54c1ee5bfad0a9d858b3315bad34bde999efd724dd")]
    public void HashData_KeyedKnownAnswer_MatchesOfficialVector(int length, string expected)
    {
        byte[] digest = new byte[Blake2s.HashSize];

        Blake2s.HashData(Convert.FromHexString(KatKey), SequentialBytes(length), digest);

        Assert.AreEqual(expected, Convert.ToHexStringLower(digest));
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(64)]
    [DataRow(100)]
    public void AppendData_InPieces_MatchesOneShotAndResetsForTheNextMessage(int pieceLength)
    {
        byte[] message = SequentialBytes(255);
        byte[] oneShot = new byte[20];
        Blake2s.HashData(Convert.FromHexString(KatKey), message, oneShot);
        using var hash = new Blake2s(20, Convert.FromHexString(KatKey));
        byte[] first = new byte[20];
        byte[] second = new byte[20];

        for (int pass = 0; pass < 2; pass++)
        {
            for (int offset = 0; offset < message.Length; offset += pieceLength)
            {
                hash.AppendData(message.AsSpan(offset, Math.Min(pieceLength, message.Length - offset)));
            }

            hash.GetHashAndReset(pass == 0 ? first : second);
        }

        CollectionAssert.AreEqual(oneShot, first);
        CollectionAssert.AreEqual(oneShot, second);
    }

    [TestMethod]
    public void HashData_ShorterDigest_IsNotATruncationOfTheLongerOne()
    {
        byte[] full = new byte[Blake2s.HashSize];
        byte[] shorter = new byte[16];

        Blake2s.HashData("abc"u8, full);
        Blake2s.HashData("abc"u8, shorter);

        CollectionAssert.AreNotEqual(full[..16], shorter);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(33)]
    public void HashData_DestinationOutOfRange_Throws(int length) =>
        Assert.ThrowsExactly<ArgumentException>(() => Blake2s.HashData([], new byte[length]));

    [TestMethod]
    public void Constructor_KeyTooLong_Throws() =>
        Assert.ThrowsExactly<ArgumentException>(() => new Blake2s(Blake2s.HashSize, new byte[Blake2s.MaxKeySize + 1]));

    [TestMethod]
    public void GetHashAndReset_DestinationNotTheDigestLength_Throws()
    {
        using var hash = new Blake2s(16);

        Assert.ThrowsExactly<ArgumentException>(() => hash.GetHashAndReset(new byte[Blake2s.HashSize]));
    }

    [TestMethod]
    public void AppendData_AfterDispose_ThrowsObjectDisposed()
    {
        var hash = new Blake2s();

        hash.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => hash.AppendData([1]));
        Assert.ThrowsExactly<ObjectDisposedException>(() => hash.GetHashAndReset(new byte[Blake2s.HashSize]));
    }

    private static byte[] SequentialBytes(int length)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < length; index++)
        {
            bytes[index] = (byte)index;
        }

        return bytes;
    }
}
