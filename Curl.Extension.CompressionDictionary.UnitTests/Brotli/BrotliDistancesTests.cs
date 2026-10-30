using Curl.Extension.CompressionDictionary.Brotli;

namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

[TestClass]
public sealed class BrotliDistancesTests
{
    // RFC 7932 section 4: with NPOSTFIX 2 and NDIRECT 4, codes 16 to 19 are distances 1 to 4, and code 21
    // (postfix 1, one extra bit set) is ((0 + 1) << 2) + 1 + 4 + 1 = 10.
    [TestMethod]
    [DataRow(17, 2)]
    [DataRow(21, 10)]
    public void Resolve_DirectAndPostfixCodes_GiveSection4Distances(int code, int expected)
    {
        var reader = new BrotliBitReader([0x01]);

        Assert.AreEqual(expected, new BrotliDistances().Resolve(code, 2, 4, reader));
    }

    [TestMethod]
    public void Resolve_LastDistanceLessOneAfterDistanceOne_Throws()
    {
        var distances = new BrotliDistances();
        distances.RememberUnlessLast(16, 1);

        Assert.ThrowsExactly<BrotliFormatException>(() => distances.Resolve(4, 0, 0, new BrotliBitReader([])));
    }
}
