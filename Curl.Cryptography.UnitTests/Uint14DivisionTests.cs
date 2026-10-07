using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>Pins <see cref="Uint14Division" /> against the divide instruction across the ranges NTRU Prime uses.</summary>
[TestClass]
public sealed class Uint14DivisionTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(0u, (ushort)3)]
    [DataRow(4590u, (ushort)4591)]
    [DataRow(4591u, (ushort)4591)]
    [DataRow(123456789u, (ushort)1531)]
    [DataRow(uint.MaxValue, (ushort)16383)]
    [DataRow(uint.MaxValue, (ushort)1)]
    public void DivideWithRemainder_Dividend_MatchesTheDivideInstruction(uint dividend, ushort modulus)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("source", "NTRU Prime uint32_divmod_uint14, checked against the divide instruction");
        diagnostics.Arrange("dividend", dividend);
        diagnostics.Arrange("modulus", modulus);

        ushort remainder = Uint14Division.DivideWithRemainder(dividend, modulus, out uint quotient);
        diagnostics.Act("remainder", remainder);
        diagnostics.Act("quotient", quotient);

        diagnostics.Assert("remainder", dividend % modulus, (uint)remainder);
        diagnostics.Assert("quotient", dividend / modulus, quotient);
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
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("source", "NTRU Prime int32_mod_uint14, checked against long arithmetic");
        diagnostics.Arrange("dividend", dividend);
        diagnostics.Arrange("modulus", modulus);

        ushort actual = Uint14Division.Remainder(dividend, modulus);
        ushort reference = (ushort)((((long)dividend % modulus) + modulus) % modulus);
        diagnostics.Act("remainder", actual);
        diagnostics.Act("long arithmetic remainder", reference);

        diagnostics.Assert("remainder", expected, actual);
        diagnostics.Assert("long arithmetic remainder", expected, reference);
        Assert.AreEqual(expected, Uint14Division.Remainder(dividend, modulus));
        Assert.AreEqual(expected, (ushort)((((long)dividend % modulus) + modulus) % modulus));
    }
}
