using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="MlDsaEncoding" />'s hint encoding to FIPS 204 algorithms 20 and 21:
/// a hint round-trips, and each malformed encoding algorithm 21 rejects is refused.
/// </summary>
[TestClass]
public sealed class MlDsaEncodingTests
{
    private const int Omega = 4;
    private const int Rows = 2;

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryUnpackHint_WellFormed_RoundTripsThroughPackHint()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] encoded = [1, 5, 2, 0, 2, 3];
        int[] hint = new int[Rows * MlDsaPolynomial.Degree];
        byte[] repacked = new byte[Omega + Rows];
        diagnostics.Arrange("vector source", "FIPS 204 algorithms 20 and 21");
        diagnostics.Arrange("omega and rows", $"{Omega}, {Rows}");
        diagnostics.Bytes("encoded hint", encoded);

        bool unpacked = MlDsaEncoding.TryUnpackHint(encoded, Omega, hint);
        MlDsaEncoding.PackHint(hint, Omega, repacked);
        diagnostics.Act("unpacked", unpacked);
        diagnostics.Act("hint ones", hint.Sum());
        diagnostics.Bytes("repacked hint", repacked);

        diagnostics.Assert("unpacked", true, unpacked);
        diagnostics.Assert("hint ones", 3, hint.Sum());
        diagnostics.Assert("hint[1], hint[5], hint[degree + 2]", "1, 1, 1", $"{hint[1]}, {hint[5]}, {hint[MlDsaPolynomial.Degree + 2]}");
        diagnostics.Diff("repacked hint", encoded, repacked);
        Assert.IsTrue(unpacked);
        Assert.AreEqual(3, hint.Sum());
        Assert.AreEqual(1, hint[1]);
        Assert.AreEqual(1, hint[5]);
        Assert.AreEqual(1, hint[MlDsaPolynomial.Degree + 2]);
        CollectionAssert.AreEqual(encoded, repacked);
    }

    [TestMethod]
    [DataRow(new byte[] { 1, 2, 0, 0, 2, 1 }, "a count below the one before it")]
    [DataRow(new byte[] { 1, 2, 3, 4, 5, 5 }, "a count past omega")]
    [DataRow(new byte[] { 3, 3, 0, 0, 2, 2 }, "positions not strictly increasing")]
    [DataRow(new byte[] { 1, 0, 0, 7, 1, 1 }, "a nonzero byte after the last position")]
    public void TryUnpackHint_Malformed_ReturnsFalse(byte[] encoded, string defect)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        int[] hint = new int[Rows * MlDsaPolynomial.Degree];
        diagnostics.Arrange("defect", defect);
        diagnostics.Bytes("encoded hint", encoded);

        bool unpacked = MlDsaEncoding.TryUnpackHint(encoded, Omega, hint);
        diagnostics.Act("unpacked", unpacked);

        diagnostics.Assert("unpacked", false, unpacked);
        Assert.IsFalse(unpacked, defect);
    }
}
