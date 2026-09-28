using System.Numerics;

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

    [TestMethod]
    public void ToLimbsAndFromLimbs_BigEndianBytes_RoundTripLeastSignificantLimbFirst()
    {
        uint[] limbs = new uint[2];
        byte[] bytes = new byte[8];

        MontgomeryModulus.ToLimbs([0x01, 0x02, 0x03, 0x04, 0x05], limbs);
        MontgomeryModulus.FromLimbs(limbs, bytes);

        CollectionAssert.AreEqual(new uint[] { 0x02030405, 0x01 }, limbs);
        Assert.AreEqual("0000000102030405", Convert.ToHexString(bytes));
    }

    [TestMethod]
    [DataRow("02", "03")]
    [DataRow("07FFFFFFFFFFFFFFFFFFFFFFFFFE", "07FFFFFFFFFFFFFFFFFFFFFFFFFE")]
    [DataRow("0123456789ABCDEF0123456789", "00")]
    public void Multiply_OperandsBelowTheModulus_GivesTheProductTimesRInverse(string left, string right)
    {
        var modulus = new MontgomeryModulus(Modulus);
        BigInteger p = ToInteger(Modulus);
        BigInteger r = BigInteger.One << (32 * modulus.LimbCount);
        uint[] result = new uint[modulus.LimbCount];

        modulus.Multiply(result, Limbs(modulus, left), Limbs(modulus, right), new uint[modulus.LimbCount + 2]);

        BigInteger expected = ToInteger(Convert.FromHexString(left)) * ToInteger(Convert.FromHexString(right))
            * BigInteger.ModPow(r, p - 2, p) % p;
        Assert.AreEqual(expected, ToInteger(result));
    }

    [TestMethod]
    [DataRow("03", "00")]
    [DataRow("03", "01")]
    [DataRow("07FFFFFFFFFFFFFFFFFFFFFFFFFE", "FFFF")]
    [DataRow("0123456789ABCDEF", "07FFFFFFFFFFFFFFFFFFFFFFFFFE")]
    public void Exponentiate_PublicTestValues_EqualsBigIntegerModPow(string baseValue, string exponent)
    {
        var modulus = new MontgomeryModulus(Modulus);
        uint[] result = new uint[modulus.LimbCount];

        modulus.Exponentiate(Limbs(modulus, baseValue), Convert.FromHexString(exponent), result);

        BigInteger expected = BigInteger.ModPow(
            ToInteger(Convert.FromHexString(baseValue)),
            ToInteger(Convert.FromHexString(exponent)),
            ToInteger(Modulus));
        Assert.AreEqual(expected, ToInteger(result));
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
