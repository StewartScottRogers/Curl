using System.Numerics;
using Curl.Testing;

namespace Curl.Cryptography;

/// <summary>
/// Pins the arithmetic modulo the Ed25519 group order L in <see cref="Scalar25519" />
/// against <see cref="BigInteger" />.
/// </summary>
[TestClass]
public sealed class Scalar25519Tests
{
    private const string OrderEncoding = "edd3f55c1a631258d69cf7a2def9de1400000000000000000000000000000010";

    private static readonly BigInteger Order = BigInteger.Pow(2, 252) + BigInteger.Parse("27742317777372353535851937790883648493");

    /// <summary>Gets or sets the MSTest context the diagnostics write to.</summary>
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public void Reduce_WideValue_MatchesBigIntegerModulo(int seed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] wide = new byte[Scalar25519.WideLength];
        new Random(seed).NextBytes(wide);
        byte[] result = new byte[Scalar25519.EncodedLength];
        diagnostics.Arrange("source", "seeded random 64-byte value, checked against BigInteger modulo L (RFC 8032 section 5.1)");
        diagnostics.Arrange("seed", seed);
        diagnostics.Bytes("wide value", wide);

        Scalar25519.Reduce(result, wide);
        diagnostics.Act("reduced", Convert.ToHexString(result));

        BigInteger expected = new BigInteger(wide, isUnsigned: true) % Order;
        BigInteger actual = new BigInteger(result, isUnsigned: true);
        diagnostics.Assert("reduced scalar", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void Reduce_AllOnes_MatchesBigIntegerModulo()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        byte[] wide = new byte[Scalar25519.WideLength];
        wide.AsSpan().Fill(0xFF);
        byte[] result = new byte[Scalar25519.EncodedLength];
        diagnostics.Arrange("source", "largest 64-byte value, checked against BigInteger modulo L (RFC 8032 section 5.1)");
        diagnostics.Bytes("wide value", wide);

        Scalar25519.Reduce(result, wide);
        diagnostics.Act("reduced", Convert.ToHexString(result));

        BigInteger expected = new BigInteger(wide, isUnsigned: true) % Order;
        BigInteger actual = new BigInteger(result, isUnsigned: true);
        diagnostics.Assert("reduced scalar", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow(4)]
    [DataRow(5)]
    public void MultiplyAdd_Scalars_MatchBigInteger(int seed)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        Random random = new(seed);
        byte[] left = new byte[Scalar25519.EncodedLength];
        byte[] right = new byte[Scalar25519.EncodedLength];
        byte[] addend = new byte[Scalar25519.EncodedLength];
        random.NextBytes(left);
        random.NextBytes(right);
        random.NextBytes(addend);
        byte[] result = new byte[Scalar25519.EncodedLength];
        diagnostics.Arrange("source", "seeded random scalars, checked against BigInteger (left * right + addend) mod L");
        diagnostics.Arrange("seed", seed);
        diagnostics.Bytes("left", left);
        diagnostics.Bytes("right", right);
        diagnostics.Bytes("addend", addend);

        Scalar25519.MultiplyAdd(result, left, right, addend);
        diagnostics.Act("result", Convert.ToHexString(result));

        BigInteger expected = ((new BigInteger(left, isUnsigned: true) * new BigInteger(right, isUnsigned: true))
            + new BigInteger(addend, isUnsigned: true)) % Order;
        BigInteger actual = new BigInteger(result, isUnsigned: true);
        diagnostics.Assert("multiply-add result", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000000", true)]
    [DataRow("ecd3f55c1a631258d69cf7a2def9de1400000000000000000000000000000010", true)]
    [DataRow(OrderEncoding, false)]
    [DataRow("eed3f55c1a631258d69cf7a2def9de1400000000000000000000000000000010", false)]
    [DataRow("0000000000000000000000000000000000000000000000000000000000000011", false)]
    public void IsBelowOrder_Scalar_ComparesWithTheGroupOrder(string scalar, bool expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("source", "scalars around the group order L (RFC 8032 section 5.1.7 S < L check)");
        diagnostics.Bytes("scalar", Convert.FromHexString(scalar));

        bool actual = Scalar25519.IsBelowOrder(Convert.FromHexString(scalar));
        diagnostics.Act("is below order", actual);

        diagnostics.Assert("is below order", expected, actual);
        Assert.AreEqual(expected, actual);
    }
}
