namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshWireReaderTests
{
    [TestMethod]
    public void Reader_ReadsWhatTheWriterWrote()
    {
        SshWireWriter writer = new();
        writer.WriteByte(7);
        writer.WriteBoolean(true);
        writer.WriteBoolean(false);
        writer.WriteUInt32(uint.MaxValue);
        writer.WriteString([1, 2, 3]);
        writer.WriteNameList(["curve25519-sha256", "ext-info-c"]);
        writer.WriteNameList([]);
        SshWireReader reader = new(writer.ToArray());

        Assert.AreEqual((byte)7, reader.ReadByte());
        Assert.IsTrue(reader.ReadBoolean());
        Assert.IsFalse(reader.ReadBoolean());
        Assert.AreEqual(uint.MaxValue, reader.ReadUInt32());
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, reader.ReadString().ToArray());
        CollectionAssert.AreEqual(new[] { "curve25519-sha256", "ext-info-c" }, reader.ReadNameList().ToArray());
        Assert.AreEqual(0, reader.ReadNameList().Count);
    }

    [TestMethod]
    public void Reader_AnyNonZeroByteIsTrue()
    {
        Assert.IsTrue(new SshWireReader(new byte[] { 0x80 }).ReadBoolean());
    }

    [TestMethod]
    public void ReadByte_AtTheEnd_ThrowsInvalidData()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => new SshWireReader(Array.Empty<byte>()).ReadByte());
    }

    [TestMethod]
    public void ReadUInt32_WithThreeBytesLeft_ThrowsInvalidData()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => new SshWireReader(new byte[] { 0, 0, 0 }).ReadUInt32());
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0, 3, 1, 2 }, DisplayName = "one byte short")]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 1 }, DisplayName = "length 2^32 - 1")]
    public void ReadString_LengthPastTheEnd_ThrowsInvalidData(byte[] payload)
    {
        Assert.ThrowsExactly<InvalidDataException>(() => new SshWireReader(payload).ReadString());
    }

    [TestMethod]
    [DataRow("00000000", "", DisplayName = "zero")]
    [DataRow("000000020080", "80", DisplayName = "the sign byte is dropped")]
    [DataRow("0000000300000A", "0A", DisplayName = "redundant leading zeros are dropped")]
    [DataRow("000000020000", "", DisplayName = "zeros alone are zero")]
    public void ReadMpint_ReturnsTheMagnitude(string encoded, string expected)
    {
        SshWireReader reader = new(Convert.FromHexString(encoded));

        Assert.AreEqual(expected, Convert.ToHexString(reader.ReadMpint().Span));
    }

    [TestMethod]
    public void ReadMpint_Negative_ThrowsInvalidData()
    {
        Assert.ThrowsExactly<InvalidDataException>(() => new SshWireReader(Convert.FromHexString("00000001FF")).ReadMpint());
    }

    [TestMethod]
    public void ReadName_ReadsAnAsciiString()
    {
        Assert.AreEqual("ssh-rsa", new SshWireReader(Convert.FromHexString("000000077373682D727361")).ReadName());
    }
}
