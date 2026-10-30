namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Pins which runs of number characters <see cref="ProtoNumberLiteral" /> reads as integer
/// and float literals.
/// </summary>
[TestClass]
public sealed class ProtoNumberLiteralTests
{
    [TestMethod]
    [DataRow("0")]
    [DataRow("017")]
    [DataRow("0x1F")]
    [DataRow("0Xab")]
    [DataRow("42")]
    public void IsInteger_IntegerLiteral_IsTrue(string literal)
    {
        Assert.IsTrue(ProtoNumberLiteral.IsInteger(literal));
    }

    [TestMethod]
    [DataRow("0x")]
    [DataRow("0xg")]
    [DataRow("08")]
    [DataRow("1_0")]
    [DataRow("1.5")]
    public void IsInteger_OtherText_IsFalse(string literal)
    {
        Assert.IsFalse(ProtoNumberLiteral.IsInteger(literal));
    }

    [TestMethod]
    [DataRow("1.5")]
    [DataRow("5.")]
    [DataRow(".5")]
    [DataRow("1e5")]
    [DataRow("1.5E-3")]
    [DataRow(".5e+3")]
    public void IsFloat_FloatLiteral_IsTrue(string literal)
    {
        Assert.IsTrue(ProtoNumberLiteral.IsFloat(literal));
    }

    [TestMethod]
    [DataRow("15")]
    [DataRow("1_e5")]
    [DataRow(".")]
    [DataRow("1_.5")]
    [DataRow("1.5_")]
    [DataRow("1.2.3")]
    [DataRow("1e")]
    [DataRow("1e+")]
    [DataRow("1ex")]
    public void IsFloat_OtherText_IsFalse(string literal)
    {
        Assert.IsFalse(ProtoNumberLiteral.IsFloat(literal));
    }
}
