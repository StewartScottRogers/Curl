namespace Curl.Extension.ObliviousHttp.BinaryHttp;

[TestClass]
public sealed class BinaryHttpReaderTests
{
    [TestMethod]
    [DataRow("25", 37L)]
    [DataRow("7bbd", 15293L)]
    [DataRow("9d7f3e7d", 494878333L)]
    [DataRow("c2197c5eff14e88c", 151288809941952652L)]
    [DataRow("4025", 37L)]
    public void ReadVariableLengthInteger_Rfc9000Encoding_GivesTheValueAndReachesTheEnd(string encoding, long expected)
    {
        BinaryHttpReader reader = new(Convert.FromHexString(encoding));

        long value = reader.ReadVariableLengthInteger();

        Assert.AreEqual(expected, value);
        Assert.IsTrue(reader.IsAtEnd);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("7b")]
    [DataRow("9d7f3e")]
    [DataRow("c2197c5eff14e8")]
    public void ReadVariableLengthInteger_InputEndsInsideTheInteger_ThrowsBinaryHttpFormatException(string encoding)
    {
        byte[] input = Convert.FromHexString(encoding);

        Assert.ThrowsExactly<BinaryHttpFormatException>(() => new BinaryHttpReader(input).ReadVariableLengthInteger());
    }

    [TestMethod]
    public void ReadLengthPrefixed_LengthWithinTheInput_GivesThoseBytes()
    {
        BinaryHttpReader reader = new(Convert.FromHexString("03474554ff"));

        byte[] bytes = reader.ReadLengthPrefixed().ToArray();

        Assert.AreEqual("474554", Convert.ToHexStringLower(bytes));
        Assert.IsFalse(reader.IsAtEnd);
    }

    [TestMethod]
    public void ReadLengthPrefixed_LengthLongerThanTheInputLeft_ThrowsBinaryHttpFormatException()
    {
        byte[] input = Convert.FromHexString("7fff4745");

        BinaryHttpFormatException exception =
            Assert.ThrowsExactly<BinaryHttpFormatException>(() => new BinaryHttpReader(input).ReadLengthPrefixed());

        Assert.AreEqual("A Binary HTTP length of 16383 is longer than the 2 bytes left in the message.", exception.Message);
    }

    [TestMethod]
    public void ReadPadding_AllZeroBytes_ReachesTheEnd()
    {
        BinaryHttpReader reader = new(new byte[4]);

        reader.ReadPadding();

        Assert.IsTrue(reader.IsAtEnd);
    }

    [TestMethod]
    public void ReadPadding_NonZeroByte_ThrowsBinaryHttpFormatException()
    {
        byte[] input = Convert.FromHexString("000001");

        Assert.ThrowsExactly<BinaryHttpFormatException>(() => new BinaryHttpReader(input).ReadPadding());
    }
}
