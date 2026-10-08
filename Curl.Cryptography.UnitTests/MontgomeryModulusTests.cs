using System.Numerics;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Checks <see cref="MontgomeryModulus" /> against <see cref="BigInteger" /> directly:
/// limb conversion, Montgomery multiplication, and exponentiation.
/// </summary>
[TestClass]
public sealed class MontgomeryModulusTests
{
    // 2^107 - 1, a Mersenne prime of 14 bytes, which is not a whole number of limbs.
    private static readonly byte[] Modulus = ((BigInteger.One << 107) - 1).ToByteArray(isUnsigned: true, isBigEndian: true);

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void ToLimbsAndFromLimbs_BigEndianBytes_RoundTripLeastSignificantLimbFirst()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        uint[] limbs = new uint[2];
        byte[] bytes = new byte[8];
        byte[] input = [0x01, 0x02, 0x03, 0x04, 0x05];
        diagnostics.Bytes("big-endian input", input);
        diagnostics.Arrange("limb count", limbs.Length);

        MontgomeryModulus.ToLimbs(input, limbs);
        MontgomeryModulus.FromLimbs(limbs, bytes);
        diagnostics.Act("limbs", string.Join(", ", limbs.Select(limb => $"0x{limb:X8}")));
        diagnostics.Act("bytes back", Convert.ToHexString(bytes));

        diagnostics.Assert("limbs", "0x02030405, 0x00000001", string.Join(", ", limbs.Select(limb => $"0x{limb:X8}")));
        diagnostics.Diff("bytes back", Convert.FromHexString("0000000102030405"), bytes);
        CollectionAssert.AreEqual(new uint[] { 0x02030405, 0x01 }, limbs);
        Assert.AreEqual("0000000102030405", Convert.ToHexString(bytes));
    }

    [TestMethod]
    [DataRow("02", "03")]
    [DataRow("07FFFFFFFFFFFFFFFFFFFFFFFFFE", "07FFFFFFFFFFFFFFFFFFFFFFFFFE")]
    [DataRow("0123456789ABCDEF0123456789", "00")]
    public void Multiply_OperandsBelowTheModulus_GivesTheProductTimesRInverse(string left, string right)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var modulus = new MontgomeryModulus(Modulus);
        BigInteger p = ToInteger(Modulus);
        BigInteger r = BigInteger.One << (32 * modulus.LimbCount);
        uint[] result = new uint[modulus.LimbCount];
        ArrangeModulus(diagnostics, modulus);
        diagnostics.Arrange("left", left);
        diagnostics.Arrange("right", right);

        modulus.Multiply(result, Limbs(modulus, left), Limbs(modulus, right), new uint[modulus.LimbCount + 2]);
        diagnostics.Act("result", ToInteger(result));

        BigInteger expected = ToInteger(Convert.FromHexString(left)) * ToInteger(Convert.FromHexString(right))
            * BigInteger.ModPow(r, p - 2, p) % p;
        diagnostics.Assert("left * right * R^-1 mod p", expected, ToInteger(result));
        Assert.AreEqual(expected, ToInteger(result));
    }

    [TestMethod]
    [DataRow("02", "03")]
    [DataRow("01FFFFFFFFFFFFFFFFFFFFFE", "01FFFFFFFFFFFFFFFFFFFFFE")]
    public void Multiply_OddLimbCount_GivesTheProductTimesRInverseOnTheThirtyTwoBitLoop(string left, string right)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        BigInteger p = (BigInteger.One << 89) - 1;
        byte[] modulusBytes = p.ToByteArray(isUnsigned: true, isBigEndian: true);
        var modulus = new MontgomeryModulus(modulusBytes);
        BigInteger r = BigInteger.One << (32 * modulus.LimbCount);
        uint[] result = new uint[modulus.LimbCount];
        ArrangeModulus(diagnostics, modulus);
        diagnostics.Arrange("left", left);
        diagnostics.Arrange("right", right);

        modulus.Multiply(result, Limbs(modulus, left), Limbs(modulus, right), new uint[modulus.LimbCount + 2]);
        diagnostics.Act("result", ToInteger(result));

        BigInteger expected = ToInteger(Convert.FromHexString(left)) * ToInteger(Convert.FromHexString(right))
            * BigInteger.ModPow(r, p - 2, p) % p;
        diagnostics.Assert("limb count", 3, modulus.LimbCount);
        diagnostics.Assert("left * right * R^-1 mod p", expected, ToInteger(result));
        Assert.AreEqual(3, modulus.LimbCount);
        Assert.AreEqual(expected, ToInteger(result));
    }

    [TestMethod]
    [DataRow("03", "00")]
    [DataRow("03", "01")]
    [DataRow("07FFFFFFFFFFFFFFFFFFFFFFFFFE", "FFFF")]
    [DataRow("0123456789ABCDEF", "07FFFFFFFFFFFFFFFFFFFFFFFFFE")]
    public void Exponentiate_PublicTestValues_EqualsBigIntegerModPow(string baseValue, string exponent)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var modulus = new MontgomeryModulus(Modulus);
        uint[] result = new uint[modulus.LimbCount];
        ArrangeModulus(diagnostics, modulus);
        diagnostics.Arrange("base", baseValue);
        diagnostics.Arrange("exponent", exponent);

        using (diagnostics.Phase("exponentiate"))
        {
            modulus.Exponentiate(Limbs(modulus, baseValue), Convert.FromHexString(exponent), result);
        }

        diagnostics.Act("result", ToInteger(result));

        BigInteger expected = BigInteger.ModPow(
            ToInteger(Convert.FromHexString(baseValue)),
            ToInteger(Convert.FromHexString(exponent)),
            ToInteger(Modulus));
        diagnostics.Assert("base^exponent mod p", expected, ToInteger(result));
        Assert.AreEqual(expected, ToInteger(result));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("05")]
    [DataRow("07FFFFFFFFFFFFFFFFFFFFFFFFFF")]
    [DataRow("FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF")]
    public void Reduce_ValueOfAnyLength_EqualsTheBigIntegerRemainder(string value)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var modulus = new MontgomeryModulus(Modulus);
        byte[] bytes = Convert.FromHexString(value);
        uint[] limbs = new uint[(bytes.Length + 3) / 4];
        MontgomeryModulus.ToLimbs(bytes, limbs);
        uint[] result = new uint[modulus.LimbCount];
        ArrangeModulus(diagnostics, modulus);
        diagnostics.Bytes("value", bytes);

        modulus.Reduce(limbs, result);
        diagnostics.Act("result", ToInteger(result));

        diagnostics.Assert("value mod p", ToInteger(bytes) % ToInteger(Modulus), ToInteger(result));
        Assert.AreEqual(ToInteger(bytes) % ToInteger(Modulus), ToInteger(result));
    }

    [TestMethod]
    [DataRow("05", "03")]
    [DataRow("03", "05")]
    [DataRow("00", "07FFFFFFFFFFFFFFFFFFFFFFFFFE")]
    public void Subtract_OperandsBelowTheModulus_EqualsTheDifferenceModuloTheModulus(string left, string right)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var modulus = new MontgomeryModulus(Modulus);
        BigInteger p = ToInteger(Modulus);
        uint[] result = new uint[modulus.LimbCount];
        ArrangeModulus(diagnostics, modulus);
        diagnostics.Arrange("left", left);
        diagnostics.Arrange("right", right);

        modulus.Subtract(result, Limbs(modulus, left), Limbs(modulus, right));
        diagnostics.Act("result", ToInteger(result));

        BigInteger expected = ((ToInteger(Convert.FromHexString(left)) - ToInteger(Convert.FromHexString(right))) % p + p) % p;
        diagnostics.Assert("(left - right) mod p", expected, ToInteger(result));
        Assert.AreEqual(expected, ToInteger(result));
    }

    [TestMethod]
    [DataRow("07FFFFFFFFFFFFFFFFFFFFFFFFFE", "07FFFFFFFFFFFFFFFFFFFFFFFFFE")]
    [DataRow("FFFFFFFFFFFFFFFFFFFFFFFFFFFF", "0123456789")]
    public void MultiplyModulo_LeftBelowRRightBelowTheModulus_EqualsTheProductModuloTheModulus(string left, string right)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var modulus = new MontgomeryModulus(Modulus);
        uint[] result = new uint[modulus.LimbCount];
        ArrangeModulus(diagnostics, modulus);
        diagnostics.Arrange("left", left);
        diagnostics.Arrange("right", right);

        modulus.MultiplyModulo(result, Limbs(modulus, left), Limbs(modulus, right));
        diagnostics.Act("result", ToInteger(result));

        diagnostics.Assert("left * right mod p", ToInteger(Convert.FromHexString(left)) * ToInteger(Convert.FromHexString(right)) % ToInteger(Modulus), ToInteger(result));
        Assert.AreEqual(ToInteger(Convert.FromHexString(left)) * ToInteger(Convert.FromHexString(right)) % ToInteger(Modulus), ToInteger(result));
    }

    [TestMethod]
    [DataRow("07FFFFFFFFFFFFFFFFFFFFFFFFFE", true)]
    [DataRow("07FFFFFFFFFFFFFFFFFFFFFFFFFF", false)]
    [DataRow("080000000000000000000000000000", false)]
    public void IsBelowModulus_Value_IsTrueOnlyBelowTheModulus(string value, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var modulus = new MontgomeryModulus(Modulus);
        uint[] limbs = new uint[modulus.LimbCount];
        MontgomeryModulus.ToLimbs(Convert.FromHexString(value.PadLeft(28, '0')), limbs);
        ArrangeModulus(diagnostics, modulus);
        diagnostics.Arrange("value", value);

        bool below = modulus.IsBelowModulus(limbs);
        diagnostics.Act("is below modulus", below);

        diagnostics.Assert("is below modulus", expected, below);
        Assert.AreEqual(expected, modulus.IsBelowModulus(limbs));
    }

    [TestMethod]
    [DataRow(89, 3)]
    [DataRow(521, 17)]
    [DataRow(607, 19)]
    public void Exponentiate_ModulusOfAnOddLimbCount_EqualsBigIntegerModPow(int mersenneExponent, int expectedLimbCount)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        BigInteger p = (BigInteger.One << mersenneExponent) - 1;
        var modulus = new MontgomeryModulus(p.ToByteArray(isUnsigned: true, isBigEndian: true));
        BigInteger baseValue = p - 2;
        byte[] exponent = Enumerable.Range(0, 40).Select(index => (byte)((index * 37) + 11)).ToArray();
        uint[] limbs = new uint[modulus.LimbCount];
        MontgomeryModulus.ToLimbs(baseValue.ToByteArray(isUnsigned: true, isBigEndian: true), limbs);
        diagnostics.Arrange("modulus", $"2^{mersenneExponent} - 1");
        diagnostics.Arrange("limb count", modulus.LimbCount);
        diagnostics.Arrange("base", "p - 2");
        diagnostics.Bytes("exponent", exponent);

        modulus.Exponentiate(limbs, exponent, limbs);
        diagnostics.Act("result", ToInteger(limbs));

        BigInteger expected = BigInteger.ModPow(baseValue, ToInteger(exponent), p);
        diagnostics.Assert("limb count", expectedLimbCount, modulus.LimbCount);
        diagnostics.Assert("base^exponent mod p", expected, ToInteger(limbs));
        Assert.AreEqual(expectedLimbCount, modulus.LimbCount);
        Assert.AreEqual(expected, ToInteger(limbs));
    }

    [TestMethod]
    public void Exponentiate_ExponentsOfOneLength_TakeTheSameSequenceOfOperationsWhateverTheirBits()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        var modulus = new MontgomeryModulus(Modulus);
        uint[] baseValue = Limbs(modulus, "0123456789ABCDEF");
        uint[] result = new uint[modulus.LimbCount];
        byte[][] exponents = [new byte[8], Enumerable.Repeat((byte)0xFF, 8).ToArray(), Convert.FromHexString("80000000000000F1"), Convert.FromHexString("0F1E2D3C4B5A6978")];
        ArrangeModulus(diagnostics, modulus);
        diagnostics.Arrange("exponents", string.Join(", ", exponents.Select(Convert.ToHexString)));

        List<List<string>> sequences = [];
        foreach (byte[] exponent in exponents)
        {
            List<string> operations = [];
            modulus.Exponentiate(baseValue, exponent, result, operations);
            sequences.Add(operations);
        }

        diagnostics.Act("operation counts", string.Join(", ", sequences.Select(sequence => sequence.Count)));

        // Each 4-bit window: four squarings, one masked look-up, one multiplication.
        string[] window = ["square", "square", "square", "square", "select", "multiply"];
        string[] expected = Enumerable.Repeat(window, 16).SelectMany(step => step).ToArray();
        diagnostics.Assert("operations of every exponent", string.Join(" ", expected), string.Join(" ", sequences[1]));
        foreach (List<string> sequence in sequences)
        {
            CollectionAssert.AreEqual(expected, sequence);
        }
    }

    private static void ArrangeModulus(TestDiagnostics diagnostics, MontgomeryModulus modulus)
    {
        diagnostics.Arrange("modulus", "2^107 - 1");
        diagnostics.Arrange("limb count", modulus.LimbCount);
    }

    private static uint[] Limbs(MontgomeryModulus modulus, string hex)
    {
        uint[] limbs = new uint[modulus.LimbCount];
        MontgomeryModulus.ToLimbs(Convert.FromHexString(hex), limbs);
        return limbs;
    }

    private static BigInteger ToInteger(ReadOnlySpan<byte> bigEndian) => new(bigEndian, isUnsigned: true, isBigEndian: true);

    private static BigInteger ToInteger(uint[] limbs)
    {
        byte[] bytes = new byte[4 * limbs.Length];
        MontgomeryModulus.FromLimbs(limbs, bytes);
        return ToInteger(bytes);
    }
}
