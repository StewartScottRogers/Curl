namespace Curl.HttpDatagram.UnitTests;

[TestClass]
public sealed class HttpDatagramVariableLengthIntegerTests
{
    [TestMethod]
    [DataRow(37UL, new byte[] { 0x25 })]
    [DataRow(15293UL, new byte[] { 0x7B, 0xBD })]
    [DataRow(494878333UL, new byte[] { 0x9D, 0x7F, 0x3E, 0x7D })]
    [DataRow(151288809941952652UL, new byte[] { 0xC2, 0x19, 0x7C, 0x5E, 0xFF, 0x14, 0xE8, 0x8C })]
    public void Write_Rfc9000AppendixAValue_WritesItsShortestEncoding(ulong value, byte[] expected)
    {
        var destination = new byte[8];

        var written = HttpDatagramVariableLengthInteger.Write(value, destination);

        CollectionAssert.AreEqual(expected, destination[..written]);
    }

    [TestMethod]
    [DataRow(new byte[] { 0x25 }, 37UL, 1)]
    [DataRow(new byte[] { 0x40, 0x25 }, 37UL, 2)]
    [DataRow(new byte[] { 0x9D, 0x7F, 0x3E, 0x7D, 0xFF }, 494878333UL, 4)]
    [DataRow(new byte[] { 0xC2, 0x19, 0x7C, 0x5E, 0xFF, 0x14, 0xE8, 0x8C }, 151288809941952652UL, 8)]
    public void TryRead_WholeInteger_ReturnsValueAndLength(byte[] source, ulong expected, int expectedLength)
    {
        var read = HttpDatagramVariableLengthInteger.TryRead(source, out var value, out var consumed);

        Assert.IsTrue(read);
        Assert.AreEqual(expected, value);
        Assert.AreEqual(expectedLength, consumed);
    }

    [TestMethod]
    [DataRow(new byte[0])]
    [DataRow(new byte[] { 0x7B })]
    [DataRow(new byte[] { 0xC2, 0x19, 0x7C })]
    public void TryRead_IncompleteInteger_ReturnsFalse(byte[] source)
    {
        var read = HttpDatagramVariableLengthInteger.TryRead(source, out var value, out var consumed);

        Assert.IsFalse(read);
        Assert.AreEqual(0UL, value);
        Assert.AreEqual(0, consumed);
    }

    [TestMethod]
    [DataRow(63UL, 1)]
    [DataRow(64UL, 2)]
    [DataRow(16383UL, 2)]
    [DataRow(16384UL, 4)]
    [DataRow(1073741823UL, 4)]
    [DataRow(1073741824UL, 8)]
    [DataRow(HttpDatagramVariableLengthInteger.MaximumValue, 8)]
    public void GetEncodedLength_BoundaryValue_ReturnsItsLength(ulong value, int expected)
    {
        var length = HttpDatagramVariableLengthInteger.GetEncodedLength(value);

        Assert.AreEqual(expected, length);
    }

    [TestMethod]
    public void GetEncodedLength_AboveMaximum_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => HttpDatagramVariableLengthInteger.GetEncodedLength(HttpDatagramVariableLengthInteger.MaximumValue + 1));
    }
}
