namespace Curl.Cryptography;

/// <summary>Pins <see cref="Uint14Division" /> against the divide instruction across the ranges NTRU Prime uses.</summary>
[TestClass]
public sealed class Uint14DivisionTests
{
    [TestMethod]
    [DataRow(0u, (ushort)3)]
    [DataRow(4590u, (ushort)4591)]
    [DataRow(4591u, (ushort)4591)]
    [DataRow(123456789u, (ushort)1531)]
    [DataRow(uint.MaxValue, (ushort)16383)]
    [DataRow(uint.MaxValue, (ushort)1)]
    public void DivideWithRemainder_Dividend_MatchesTheDivideInstruction(uint dividend, ushort modulus)
    {
        ushort remainder = Uint14Division.DivideWithRemainder(dividend, modulus, out uint quotient);

        Assert.AreEqual(dividend % modulus, (uint)remainder);
        Assert.AreEqual(dividend / modulus, quotient);
    }

    [TestMethod]
    [DataRow(-1, (ushort)3, (ushort)2)]
    [DataRow(-4591, (ushort)4591, (ushort)0)]
    [DataRow(-2296, (ushort)4591, (ushort)2295)]
    [DataRow(int.MinValue, (ushort)4591, (ushort)2512)]
    [DataRow(int.MaxValue, (ushort)4591, (ushort)2078)]
    public void Remainder_SignedDividend_IsTheNonNegativeRemainder(int dividend, ushort modulus, ushort expected)
    {
        Assert.AreEqual(expected, Uint14Division.Remainder(dividend, modulus));
        Assert.AreEqual(expected, (ushort)((((long)dividend % modulus) + modulus) % modulus));
    }
}
