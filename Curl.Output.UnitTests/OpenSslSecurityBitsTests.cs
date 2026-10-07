using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslSecurityBits"/> to OpenSSL 3.5's <c>ossl_ifc_ffc_compute_security_bits</c>:
/// the canonical sizes its table names, and the formula with its caps around them.
/// </summary>
[TestClass]
public sealed class OpenSslSecurityBitsTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(2048, 112)]
    [DataRow(3072, 128)]
    [DataRow(4096, 152)]
    [DataRow(6144, 176)]
    [DataRow(7680, 192)]
    [DataRow(8192, 200)]
    [DataRow(15360, 256)]
    public void ForModulusBits_CanonicalSize_ReturnsTheStandardsStrength(int modulusBits, int expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("modulus bits", modulusBits);

        var actual = OpenSslSecurityBits.ForModulusBits(modulusBits);

        diagnostics.Act("security bits", actual);
        diagnostics.Assert("security bits", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(7, 0)]
    [DataRow(687737, 1200)]
    [DataRow(1000000, 1200)]
    public void ForModulusBits_OutsideTheFormulasRange_ReturnsItsBounds(int modulusBits, int expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("modulus bits", modulusBits);

        var actual = OpenSslSecurityBits.ForModulusBits(modulusBits);

        diagnostics.Act("security bits", actual);
        diagnostics.Assert("security bits", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(1024, 80)]
    [DataRow(7679, 192)]
    [DataRow(15359, 256)]
    [DataRow(20000, 296)]
    public void ForModulusBits_OtherSize_EvaluatesTheFormula(int modulusBits, int expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("modulus bits", modulusBits);

        var actual = OpenSslSecurityBits.ForModulusBits(modulusBits);

        diagnostics.Act("security bits", actual);
        diagnostics.Assert("security bits", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
