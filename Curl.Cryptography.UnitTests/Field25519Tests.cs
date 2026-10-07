using Curl.Testing;

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

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(PrimeEncoding, "0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("f0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f", "0300000000000000000000000000000000000000000000000000000000000000")]
    [DataRow(PrimeMinusOneEncoding, PrimeMinusOneEncoding)]
    [DataRow("0900000000000000000000000000000000000000000000000000000000000000", "0900000000000000000000000000000000000000000000000000000000000000")]
    public void Encode_DecodedValue_IsReducedModuloThePrime(string encoded, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> element = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        diagnostics.Arrange("prime", "2^255 - 19, RFC 7748 section 4.1");
        diagnostics.Bytes("encoded", Convert.FromHexString(encoded));

        Field25519.Decode(element, Convert.FromHexString(encoded));
        Field25519.Encode(result, element);
        diagnostics.Act("re-encoded", Convert.ToHexStringLower(result));

        diagnostics.Diff("re-encoded", Convert.FromHexString(expected), result);
        Assert.AreEqual(expected, Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void Decode_TopBitSet_IgnoresIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] encoded = new byte[Field25519.EncodedLength];
        encoded[0] = 5;
        encoded[31] = 0x80;
        Span<long> element = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        diagnostics.Arrange("vector source", "RFC 7748 section 5: the top bit of a u-coordinate is masked");
        diagnostics.Bytes("encoded", encoded);

        Field25519.Decode(element, encoded);
        Field25519.Encode(result, element);
        diagnostics.Act("re-encoded", Convert.ToHexStringLower(result));

        diagnostics.Diff("re-encoded", Convert.FromHexString("05" + new string('0', 62)), result);
        Assert.AreEqual("05" + new string('0', 62), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void Subtract_OneMinusTwo_IsThePrimeMinusOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> one = stackalloc long[Field25519.LimbCount];
        Span<long> two = stackalloc long[Field25519.LimbCount];
        Span<long> difference = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.SetSmall(one, 1);
        Field25519.SetSmall(two, 2);
        diagnostics.Arrange("operation", "1 - 2 mod 2^255 - 19");

        Field25519.Subtract(difference, one, two);
        Field25519.Encode(result, difference);
        diagnostics.Act("difference", Convert.ToHexStringLower(result));

        diagnostics.Diff("difference", Convert.FromHexString(PrimeMinusOneEncoding), result);
        Assert.AreEqual(PrimeMinusOneEncoding, Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void Multiply_PrimeMinusOneSquared_IsOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> minusOne = stackalloc long[Field25519.LimbCount];
        Span<long> square = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.Decode(minusOne, Convert.FromHexString(PrimeMinusOneEncoding));
        diagnostics.Arrange("operation", "(p - 1)^2 mod p");
        diagnostics.Bytes("p - 1", Convert.FromHexString(PrimeMinusOneEncoding));

        Field25519.Square(square, minusOne);
        Field25519.Encode(result, square);
        diagnostics.Act("square", Convert.ToHexStringLower(result));

        diagnostics.Diff("square", Convert.FromHexString("01" + new string('0', 62)), result);
        Assert.AreEqual("01" + new string('0', 62), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    [DataRow((ushort)2)]
    [DataRow((ushort)9)]
    [DataRow((ushort)0xFFFF)]
    public void Invert_TimesTheValue_IsOne(ushort value)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> element = stackalloc long[Field25519.LimbCount];
        Span<long> inverse = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.SetSmall(element, value);
        diagnostics.Arrange("value", value);

        Field25519.Invert(inverse, element);
        Field25519.Multiply(inverse, inverse, element);
        Field25519.Encode(result, inverse);
        diagnostics.Act("value^-1 * value", Convert.ToHexStringLower(result));

        diagnostics.Diff("value^-1 * value", Convert.FromHexString("01" + new string('0', 62)), result);
        Assert.AreEqual("01" + new string('0', 62), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void Invert_Zero_IsZero()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> element = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.SetSmall(element, 0);
        diagnostics.Arrange("value", 0);

        Field25519.Invert(element, element);
        Field25519.Encode(result, element);
        diagnostics.Act("inverse", Convert.ToHexStringLower(result));

        diagnostics.Diff("inverse", new byte[Field25519.EncodedLength], result);
        Assert.AreEqual(new string('0', 64), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    [DataRow(1u, 7L, 3L)]
    [DataRow(0u, 3L, 7L)]
    [DataRow(2u, 3L, 7L)]
    public void ConditionalSwap_ReadsOnlyTheLowestBit(uint bit, long expectedLeft, long expectedRight)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> left = stackalloc long[Field25519.LimbCount];
        Span<long> right = stackalloc long[Field25519.LimbCount];
        Field25519.SetSmall(left, 3);
        Field25519.SetSmall(right, 7);
        diagnostics.Arrange("swap bit", bit);
        diagnostics.Arrange("left, right", "3, 7");

        Field25519.ConditionalSwap(left, right, bit);
        diagnostics.Act("left limb 0", left[0]);
        diagnostics.Act("right limb 0", right[0]);

        diagnostics.Assert("left limb 0", expectedLeft, left[0]);
        diagnostics.Assert("right limb 0", expectedRight, right[0]);
        Assert.AreEqual(expectedLeft, left[0]);
        Assert.AreEqual(expectedRight, right[0]);
    }

    [TestMethod]
    public void Negate_One_IsThePrimeMinusOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> element = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.SetSmall(element, 1);
        diagnostics.Arrange("value", 1);

        Field25519.Negate(element, element);
        Field25519.Encode(result, element);
        diagnostics.Act("negation", Convert.ToHexStringLower(result));

        diagnostics.Diff("negation", Convert.FromHexString(PrimeMinusOneEncoding), result);
        Assert.AreEqual(PrimeMinusOneEncoding, Convert.ToHexStringLower(result));
    }

    [TestMethod]
    public void PowerByPublicExponent_ThreeToTheFifth_Is243()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> element = stackalloc long[Field25519.LimbCount];
        byte[] result = new byte[Field25519.EncodedLength];
        Field25519.SetSmall(element, 3);
        diagnostics.Arrange("base", 3);
        diagnostics.Bytes("exponent", [5, 0]);

        Field25519.PowerByPublicExponent(element, element, [5, 0]);
        Field25519.Encode(result, element);
        diagnostics.Act("power", Convert.ToHexStringLower(result));

        diagnostics.Diff("power", Convert.FromHexString("f3" + new string('0', 62)), result);
        Assert.AreEqual("f3" + new string('0', 62), Convert.ToHexStringLower(result));
    }

    [TestMethod]
    [DataRow(PrimeEncoding, "0000000000000000000000000000000000000000000000000000000000000000", true)]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000000", "0200000000000000000000000000000000000000000000000000000000000000", false)]
    public void AreEqual_ComparesCanonicalValues(string left, string right, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> leftElement = stackalloc long[Field25519.LimbCount];
        Span<long> rightElement = stackalloc long[Field25519.LimbCount];
        Field25519.Decode(leftElement, Convert.FromHexString(left));
        Field25519.Decode(rightElement, Convert.FromHexString(right));
        diagnostics.Arrange("prime", "2^255 - 19, RFC 7748 section 4.1");
        diagnostics.Bytes("left", Convert.FromHexString(left));
        diagnostics.Bytes("right", Convert.FromHexString(right));

        bool equal = Field25519.AreEqual(leftElement, rightElement);
        diagnostics.Act("equal", equal);

        diagnostics.Assert("equal", expected, equal);
        Assert.AreEqual(expected, equal);
    }

    [TestMethod]
    [DataRow(PrimeMinusOneEncoding, 0u)]
    [DataRow("f0ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f", 1u)]
    public void Parity_IsTheLowestBitOfTheCanonicalValue(string encoded, uint expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> element = stackalloc long[Field25519.LimbCount];
        Field25519.Decode(element, Convert.FromHexString(encoded));
        diagnostics.Arrange("prime", "2^255 - 19, RFC 7748 section 4.1");
        diagnostics.Bytes("encoded", Convert.FromHexString(encoded));

        uint parity = Field25519.Parity(element);
        diagnostics.Act("parity", parity);

        diagnostics.Assert("parity", expected, parity);
        Assert.AreEqual(expected, parity);
    }

    [TestMethod]
    public void Clear_ZeroesEveryLimb()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Span<long> element = stackalloc long[Field25519.LimbCount];
        element.Fill(-1);
        diagnostics.Arrange("limbs before", "every limb -1");

        Field25519.Clear(element);
        int firstNonZero = element.IndexOfAnyExcept(0L);
        diagnostics.Act("first non-zero limb", firstNonZero);

        diagnostics.Assert("first non-zero limb", -1, firstNonZero);
        Assert.IsTrue(firstNonZero < 0);
    }
}
