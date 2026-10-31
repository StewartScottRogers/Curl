using Curl.Extension.Protocol.Amqp0.Wire;

namespace Curl.Extension.Protocol.Amqp0.UnitTests.Wire;

[TestClass]
public sealed class Amqp0DataTypeTests
{
    [TestMethod]
    public void Writer_WritesEveryIntegerTypeInNetworkByteOrder()
    {
        var writer = new Amqp0Writer();
        writer.WriteOctet(0x12);
        writer.WriteShort(0x3456);
        writer.WriteLong(0x789ABCDE);
        writer.WriteLongLong(0x0102030405060708);
        writer.WriteTimestamp(DateTimeOffset.FromUnixTimeSeconds(1700000000));

        Assert.AreEqual("12" + "3456" + "789ABCDE" + "0102030405060708" + "000000006553F100", Convert.ToHexString(writer.ToArray()));
    }

    [TestMethod]
    public void Reader_ReadsWhatTheWriterWrote()
    {
        var reader = new Amqp0Reader(Convert.FromHexString("12" + "3456" + "789ABCDE" + "0102030405060708" + "000000006553F100" + "0368C3A9" + "00000002AABB" + "CCDD"));

        Assert.AreEqual((byte)0x12, reader.ReadOctet());
        Assert.AreEqual((ushort)0x3456, reader.ReadShort());
        Assert.AreEqual(0x789ABCDEu, reader.ReadLong());
        Assert.AreEqual(0x0102030405060708ul, reader.ReadLongLong());
        Assert.AreEqual(DateTimeOffset.FromUnixTimeSeconds(1700000000), reader.ReadTimestamp());
        Assert.AreEqual("hé", reader.ReadShortString());
        CollectionAssert.AreEqual(new byte[] { 0xAA, 0xBB }, reader.ReadLongString());
        Assert.IsFalse(reader.IsAtEnd);
        CollectionAssert.AreEqual(new byte[] { 0xCC, 0xDD }, reader.ReadBytes(2));
        Assert.IsTrue(reader.IsAtEnd);
        reader.EnsureAtEnd("The test");
    }

    [TestMethod]
    public void Bits_PackFirstFieldIntoLowestBit()
    {
        var writer = new Amqp0Writer();
        writer.WriteBits(true, false, true, true);

        byte[] bytes = writer.ToArray();
        Assert.AreEqual("0D", Convert.ToHexString(bytes));
        CollectionAssert.AreEqual(new[] { true, false, true, true }, new Amqp0Reader(bytes).ReadBits(4));
    }

    [TestMethod]
    public void ShortString_WritesLengthOctetAndUtf8()
    {
        var writer = new Amqp0Writer();
        writer.WriteShortString("hé");
        writer.WriteLongString([0xAA]);

        Assert.AreEqual("0368C3A9" + "00000001AA", Convert.ToHexString(writer.ToArray()));
    }

    [TestMethod]
    public void ShortString_LongerThan255Bytes_IsSyntaxError()
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => new Amqp0Writer().WriteShortString(new string('x', 256)));

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }

    [TestMethod]
    [DataRow("01FF")]
    [DataRow("00000002FFFE")]
    public void Strings_NotUtf8_AreSyntaxError(string hex)
    {
        var reader = new Amqp0Reader(Convert.FromHexString(hex));

        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(
            () => _ = hex.Length == 4 ? reader.ReadShortString() : reader.ReadLongStringText());

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }

    [TestMethod]
    public void LongStringText_DecodesUtf8()
    {
        Assert.AreEqual("en_US", new Amqp0Reader(Convert.FromHexString("00000005656E5F5553")).ReadLongStringText());
    }

    [TestMethod]
    [DataRow("00")]
    [DataRow("0003AABB")]
    [DataRow("FFFFFFFF")]
    public void Reader_TruncatedField_IsSyntaxError(string hex)
    {
        var reader = new Amqp0Reader(Convert.FromHexString(hex));

        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => reader.ReadLongString());

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }

    [TestMethod]
    [DataRow("FFFFFFF1886E08FF")]
    [DataRow("0000003AFFF44180")]
    public void Timestamp_OutsideDateTimeOffset_IsSyntaxError(string hex)
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => new Amqp0Reader(Convert.FromHexString(hex)).ReadTimestamp());

        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, exception.ReplyCode);
    }

    [TestMethod]
    public void EnsureAtEnd_BytesLeft_IsSyntaxError()
    {
        var exception = Assert.ThrowsExactly<Amqp0WireFormatException>(() => new Amqp0Reader(new byte[] { 1 }).EnsureAtEnd("The test"));

        Assert.AreEqual("The test has bytes after its last field.", exception.Message);
    }

    [TestMethod]
    public void Exception_FactoriesCarryTheirReplyCodes()
    {
        Assert.AreEqual(Amqp0ReplyCode.FrameError, Amqp0WireFormatException.FrameError("x").ReplyCode);
        Assert.AreEqual(Amqp0ReplyCode.SyntaxError, Amqp0WireFormatException.SyntaxError("x").ReplyCode);
        Assert.AreEqual(Amqp0ReplyCode.CommandInvalid, Amqp0WireFormatException.CommandInvalid("x").ReplyCode);
        Assert.AreEqual((ushort)505, (ushort)Amqp0WireFormatException.UnexpectedFrame("x").ReplyCode);
    }
}
