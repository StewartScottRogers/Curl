namespace Curl.Protobuf.Descriptors;

/// <summary>
/// Pins how <see cref="ProtoIntegerLiteral" /> reads decimal, octal and hexadecimal literals.
/// </summary>
[TestClass]
public sealed class ProtoIntegerLiteralTests
{
    [TestMethod]
    [DataRow("0", 0UL)]
    [DataRow("017", 15UL)]
    [DataRow("0x1F", 31UL)]
    [DataRow("0XfF", 255UL)]
    [DataRow("18446744073709551615", ulong.MaxValue)]
    public void TryParse_LiteralThatFits_ReadsItsValue(string literal, ulong expected)
    {
        var parsed = ProtoIntegerLiteral.TryParse(literal, out var value);

        Assert.AreEqual((true, expected), (parsed, value));
    }

    [TestMethod]
    [DataRow("18446744073709551616")]
    [DataRow("0x10000000000000000")]
    public void TryParse_LiteralPastSixtyFourBits_IsFalse(string literal)
    {
        Assert.IsFalse(ProtoIntegerLiteral.TryParse(literal, out _));
    }
}
