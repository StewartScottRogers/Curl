namespace Curl.Cryptography;

/// <summary>
/// Pins the GF(2^255 - 19) arithmetic in <see cref="Field25519" /> that X25519 uses and
/// Ed25519 reuses.
/// </summary>
[TestClass]
public sealed class Field25519Tests
{
    private const string PrimeEncoding = "edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f";
    private const string PrimeMinusOneEncoding = "ecffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f";

    [TestMethod]
    [DataRow(PrimeEncoding, "0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("f0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f", "0300000000000000000000000000000000000000000000000000000000000000")]
    [DataRow(PrimeMinusOneEncoding, PrimeMinusOneEncoding)]
    [DataRow("0900000000000000000000000000000000000000000000000000000000000000", "0900000000000000000000000000000000000000000000000000000000000000")]
    public void Encode_DecodedValue_IsReducedModuloThePrime(string encoded, string expected)
    {
        Span<long> element = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];

        Field25519.Decode(element, Convert.FromHexString(encoded));
        Field25519.Encode(result, element);

        Assert.AreEqual(expected, Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void Decode_TopBitSet_IgnoresIt()
    {
        byte[] encoded = new byte[Field25519.EncodedLength];
        encoded[0] = 5;
        encoded[31] = 0x80;
        Span<long> element = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];

        Field25519.Decode(element, encoded);
        Field25519.Encode(result, element);

        Assert.AreEqual("05" + new string('0', 62), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void Subtract_OneMinusTwo_IsThePrimeMinusOne()
    {
        Span<long> one = stackalloc long[Field25519.LimbCount];
        Span<long> two = stackalloc long[Field25519.LimbCount];
        Span<long> difference = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.SetSmall(one, 1);
        Field25519.SetSmall(two, 2);

        Field25519.Subtract(difference, one, two);
        Field25519.Encode(result, difference);

        Assert.AreEqual(PrimeMinusOneEncoding, Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void Multiply_PrimeMinusOneSquared_IsOne()
    {
        Span<long> minusOne = stackalloc long[Field25519.LimbCount];
        Span<long> square = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.Decode(minusOne, Convert.FromHexString(PrimeMinusOneEncoding));

        Field25519.Square(square, minusOne);
        Field25519.Encode(result, square);

        Assert.AreEqual("01" + new string('0', 62), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    [DataRow((ushort)2)]
    [DataRow((ushort)9)]
    [DataRow((ushort)0xFFFF)]
    public void Invert_TimesTheValue_IsOne(ushort value)
    {
        Span<long> element = stackalloc long[Field25519.LimbCount];
        Span<long> inverse = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.SetSmall(element, value);

        Field25519.Invert(inverse, element);
        Field25519.Multiply(inverse, inverse, element);
        Field25519.Encode(result, inverse);

        Assert.AreEqual("01" + new string('0', 62), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void Invert_Zero_IsZero()
    {
        Span<long> element = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.SetSmall(element, 0);

        Field25519.Invert(element, element);
        Field25519.Encode(result, element);

        Assert.AreEqual(new string('0', 64), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    [DataRow(1u, 7L, 3L)]
    [DataRow(0u, 3L, 7L)]
    [DataRow(2u, 3L, 7L)]
    public void ConditionalSwap_ReadsOnlyTheLowestBit(uint bit, long expectedLeft, long expectedRight)
    {
        Span<long> left = stackalloc long[Field25519.LimbCount];
        Span<long> right = stackalloc long[Field25519.LimbCount];
        Field25519.SetSmall(left, 3);
        Field25519.SetSmall(right, 7);

        Field25519.ConditionalSwap(left, right, bit);

        Assert.AreEqual(expectedLeft, left[0]);
        Assert.AreEqual(expectedRight, right[0]);
    }

    [TestMethod]
    public void Clear_ZeroesEveryLimb()
    {
        Span<long> element = stackalloc long[Field25519.LimbCount];
        element.Fill(-1);

        Field25519.Clear(element);

        Assert.IsTrue(element.IndexOfAnyExcept(0L) < 0);
    }
}
