using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins the edwards25519 point arithmetic in <see cref="Edwards25519" /> that Ed25519
/// signs with.
/// </summary>
[TestClass]
public sealed class Edwards25519Tests
{
    // RFC 8032 section 5.1: B has y = 4/5 and a positive (even) x.
    private const string BasePointEncoding = "5866666666666666666666666666666666666666666666666666666666666666";

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void TryDecode_BasePoint_EncodesBackToItself()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        long[] point = new long[Edwards25519.PointLength];
        byte[] encoded = new byte[Edwards25519.EncodedLength];
        diagnostics.Arrange("vector source", "RFC 8032 section 5.1, base point B");
        diagnostics.Bytes("encoding", Convert.FromHexString(BasePointEncoding));

        bool decoded = Edwards25519.TryDecode(point, Convert.FromHexString(BasePointEncoding));
        Edwards25519.Encode(encoded, point);
        diagnostics.Act("decoded", decoded);
        diagnostics.Act("encoded", Convert.ToHexStringLower(encoded));

        diagnostics.Assert("decoded", true, decoded);
        diagnostics.Diff("encoded", Convert.FromHexString(BasePointEncoding), encoded);
        Assert.IsTrue(decoded);
        Assert.AreEqual(BasePointEncoding, Convert.ToHexStringLower(encoded));
    }

    [TestMethod]
    public void Encode_Neutral_IsYOne()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        long[] point = new long[Edwards25519.PointLength];
        byte[] encoded = new byte[Edwards25519.EncodedLength];
        diagnostics.Arrange("point", "neutral element (0, 1)");

        Edwards25519.SetNeutral(point);
        Edwards25519.Encode(encoded, point);
        diagnostics.Act("encoded", Convert.ToHexStringLower(encoded));

        diagnostics.Diff("encoded", Convert.FromHexString("01" + new string('0', 62)), encoded);
        Assert.AreEqual("01" + new string('0', 62), Convert.ToHexStringLower(encoded));
    }

    [TestMethod]
    public void Add_BasePointToItself_IsTwiceTheBasePoint()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        long[] sum = new long[Edwards25519.PointLength];
        long[] product = new long[Edwards25519.PointLength];
        byte[] two = new byte[Scalar25519.EncodedLength];
        two[0] = 2;
        byte[] sumEncoding = new byte[Edwards25519.EncodedLength];
        byte[] productEncoding = new byte[Edwards25519.EncodedLength];
        Assert.IsTrue(Edwards25519.TryDecode(sum, Convert.FromHexString(BasePointEncoding)));
        diagnostics.Arrange("vector source", "RFC 8032 section 5.1, base point B");
        diagnostics.Bytes("scalar", two);

        Edwards25519.Add(sum, sum);
        Edwards25519.ScalarMultiplyBase(product, two);
        Edwards25519.Encode(sumEncoding, sum);
        Edwards25519.Encode(productEncoding, product);
        diagnostics.Act("B + B", Convert.ToHexStringLower(sumEncoding));
        diagnostics.Act("[2]B", Convert.ToHexStringLower(productEncoding));

        diagnostics.Diff("B + B against [2]B", productEncoding, sumEncoding);
        Assert.AreEqual(Convert.ToHexStringLower(productEncoding), Convert.ToHexStringLower(sumEncoding));
    }

    [TestMethod]
    public void Negate_BasePoint_FlipsTheSignBit()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        long[] point = new long[Edwards25519.PointLength];
        byte[] encoded = new byte[Edwards25519.EncodedLength];
        Assert.IsTrue(Edwards25519.TryDecode(point, Convert.FromHexString(BasePointEncoding)));
        diagnostics.Arrange("vector source", "RFC 8032 section 5.1, base point B");
        diagnostics.Bytes("encoding", Convert.FromHexString(BasePointEncoding));

        Edwards25519.Negate(point);
        Edwards25519.Encode(encoded, point);
        diagnostics.Act("encoded -B", Convert.ToHexStringLower(encoded));

        diagnostics.Diff("encoded -B", Convert.FromHexString("58666666666666666666666666666666666666666666666666666666666666e6"), encoded);
        Assert.AreEqual("58666666666666666666666666666666666666666666666666666666666666e6", Convert.ToHexStringLower(encoded));
    }

    // y = 0 gives x^2 = -1, whose root is sqrt(-1): both signs decode.
    [TestMethod]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000")]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000080")]
    public void TryDecode_YZero_DecodesWithEitherSign(string encoding)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        long[] point = new long[Edwards25519.PointLength];
        byte[] encoded = new byte[Edwards25519.EncodedLength];
        diagnostics.Bytes("encoding", Convert.FromHexString(encoding));
        diagnostics.Arrange("sign bit", encoding.EndsWith("80", StringComparison.Ordinal));

        bool decoded = Edwards25519.TryDecode(point, Convert.FromHexString(encoding));
        Edwards25519.Encode(encoded, point);
        diagnostics.Act("decoded", decoded);
        diagnostics.Act("encoded", Convert.ToHexStringLower(encoded));

        diagnostics.Assert("decoded", true, decoded);
        diagnostics.Diff("encoded", Convert.FromHexString(encoding), encoded);
        Assert.IsTrue(decoded);
        Assert.AreEqual(encoding, Convert.ToHexStringLower(encoded));
    }

    // y = p is y = 0 written non-canonically; y = 1 with the sign bit set asks for a
    // negative zero; y = 2 has no x on the curve.
    [TestMethod]
    [DataRow("edffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff7f")]
    [DataRow("0100000000000000000000000000000000000000000000000000000000000080")]
    [DataRow("0200000000000000000000000000000000000000000000000000000000000000")]
    public void TryDecode_InvalidEncoding_IsFalse(string encoding)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        long[] point = new long[Edwards25519.PointLength];
        diagnostics.Arrange("vector source", "RFC 8032 section 5.1.3, encodings that do not decode");
        diagnostics.Bytes("encoding", Convert.FromHexString(encoding));

        bool decoded = Edwards25519.TryDecode(point, Convert.FromHexString(encoding));
        diagnostics.Act("decoded", decoded);

        diagnostics.Assert("decoded", false, decoded);
        Assert.IsFalse(decoded);
    }
}
