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

    [TestMethod]
    public void TryUnpackHint_WellFormed_RoundTripsThroughPackHint()
    {
        byte[] encoded = [1, 5, 2, 0, 2, 3];
        int[] hint = new int[Rows * MlDsaPolynomial.Degree];
        byte[] repacked = new byte[Omega + Rows];

        bool unpacked = MlDsaEncoding.TryUnpackHint(encoded, Omega, hint);
        MlDsaEncoding.PackHint(hint, Omega, repacked);

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
        int[] hint = new int[Rows * MlDsaPolynomial.Degree];

        bool unpacked = MlDsaEncoding.TryUnpackHint(encoded, Omega, hint);

        Assert.IsFalse(unpacked, defect);
    }
}
