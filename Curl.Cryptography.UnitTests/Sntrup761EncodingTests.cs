using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins <see cref="Sntrup761Encoding" />'s mixed-radix <c>Encode</c> and <c>Decode</c>
/// (NTRU Prime round 3 reference <c>Encode.c</c>, <c>Decode.c</c>) on moduli the sntrup761
/// sizes never reach: a modulus of 1, one-byte moduli, and pairs small enough to write no byte.
/// </summary>
[TestClass]
public sealed class Sntrup761EncodingTests
{
    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(new ushort[] { 0 }, new ushort[] { 1 }, "")]
    [DataRow(new ushort[] { 199 }, new ushort[] { 200 }, "c7")]
    [DataRow(new ushort[] { 4590 }, new ushort[] { 4591 }, "ee11")]
    [DataRow(new ushort[] { 5, 9 }, new ushort[] { 10, 100 }, "5f00")]
    [DataRow(new ushort[] { 3, 1, 2 }, new ushort[] { 4, 5, 6 }, "2f")]
    [DataRow(new ushort[] { 150, 170 }, new ushort[] { 200, 200 }, "6685")]
    public void Encode_Values_WritesTheReferenceBytesAndDecodesBack(ushort[] values, ushort[] moduli, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] output = new byte[16];
        diagnostics.Arrange("source", "NTRU Prime round 3 reference Encode.c and Decode.c");
        diagnostics.Arrange("values", string.Join(' ', values));
        diagnostics.Arrange("moduli", string.Join(' ', moduli));

        int written = Sntrup761Encoding.Encode(output, values, moduli);
        ushort[] decoded = new ushort[values.Length];
        Sntrup761Encoding.Decode(decoded, output.AsSpan(0, written), moduli);
        diagnostics.Bytes("encoded", output.AsSpan(0, written));
        diagnostics.Act("decoded", string.Join(' ', decoded));

        diagnostics.Diff("encoded", expected, Convert.ToHexStringLower(output.AsSpan(0, written)));
        diagnostics.Diff("decoded", string.Join(' ', values), string.Join(' ', decoded));
        Assert.AreEqual(expected, Convert.ToHexStringLower(output.AsSpan(0, written)));
        CollectionAssert.AreEqual(values, decoded);
    }

    // Decode reduces whatever it reads: 0xFF mod 200 = 55.
    [TestMethod]
    public void Decode_ValueAboveItsModulus_ReducesIt()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        ushort[] decoded = new ushort[1];
        diagnostics.Bytes("encoded", [0xFF]);
        diagnostics.Arrange("modulus", 200);

        Sntrup761Encoding.Decode(decoded, [0xFF], [200]);
        diagnostics.Act("decoded", decoded[0]);

        diagnostics.Assert("decoded", (ushort)55, decoded[0]);
        Assert.AreEqual((ushort)55, decoded[0]);
    }

    [TestMethod]
    public void EncodeSmall_EveryCoefficientValue_RoundTrips()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        short[] polynomial = new short[Sntrup761Ring.P];
        for (int index = 0; index < polynomial.Length; index++)
        {
            polynomial[index] = (short)((index % 3) - 1);
        }

        byte[] encoded = new byte[Sntrup761Encoding.SmallSize];
        short[] decoded = new short[Sntrup761Ring.P];
        diagnostics.Arrange("polynomial", "761 coefficients cycling -1, 0, 1 (sntrup761 small polynomial)");

        Sntrup761Encoding.EncodeSmall(encoded, polynomial);
        Sntrup761Encoding.DecodeSmall(decoded, encoded);
        diagnostics.Act("encoded length", encoded.Length);
        diagnostics.Bytes("encoded", encoded);

        diagnostics.Diff("decoded", string.Join(' ', polynomial), string.Join(' ', decoded));
        CollectionAssert.AreEqual(polynomial, decoded);
    }
}
