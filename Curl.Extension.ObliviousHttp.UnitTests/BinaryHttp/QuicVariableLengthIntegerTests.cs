namespace Curl.Extension.ObliviousHttp.BinaryHttp;

[TestClass]
public sealed class QuicVariableLengthIntegerTests
{
    [TestMethod]
    [DataRow(37L, "25")]
    [DataRow(15293L, "7bbd")]
    [DataRow(494878333L, "9d7f3e7d")]
    [DataRow(151288809941952652L, "c2197c5eff14e88c")]
    [DataRow(63L, "3f")]
    [DataRow(64L, "4040")]
    [DataRow(16384L, "80004000")]
    [DataRow(1073741824L, "c000000040000000")]
    [DataRow(QuicVariableLengthInteger.MaxValue, "ffffffffffffffff")]
    public void Write_Value_WritesTheShortestRfc9000Encoding(long value, string expected)
    {
        using MemoryStream destination = new();

        QuicVariableLengthInteger.Write(destination, value);

        Assert.AreEqual(expected, Convert.ToHexStringLower(destination.ToArray()));
    }

    [TestMethod]
    [DataRow(-1L)]
    [DataRow(QuicVariableLengthInteger.MaxValue + 1)]
    public void Write_ValueOutOfRange_ThrowsArgumentOutOfRangeException(long value)
    {
        using MemoryStream destination = new();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => QuicVariableLengthInteger.Write(destination, value));
    }
}
