namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshWireWriterTests
{
    [TestMethod]
    public void Writer_WritesEachRfc4251TypeBigEndianWithLengthPrefixes()
    {
        SshWireWriter writer = new();
        writer.WriteByte(0x14);
        writer.WriteBoolean(true);
        writer.WriteBoolean(false);
        writer.WriteUInt32(0x01020304);
        writer.WriteString([0xAA, 0xBB]);
        writer.WriteNameList(["a", "bc"]);
        writer.WriteNameList([]);

        CollectionAssert.AreEqual(
            new byte[] { 0x14, 1, 0, 1, 2, 3, 4, 0, 0, 0, 2, 0xAA, 0xBB, 0, 0, 0, 4, (byte)'a', (byte)',', (byte)'b', (byte)'c', 0, 0, 0, 0 },
            writer.ToArray());
    }

    [TestMethod]
    [DataRow("", "00000000", DisplayName = "zero is an empty string (RFC 4251 section 5)")]
    [DataRow("000000", "00000000", DisplayName = "leading zeros alone are zero")]
    [DataRow("0009A378F9B2E332A7", "0000000809A378F9B2E332A7", DisplayName = "RFC 4251's 9a378f9b2e332a7")]
    [DataRow("80", "000000020080", DisplayName = "RFC 4251's 0x80 gets a sign byte")]
    public void WriteMpint_WritesTheShortestNonNegativeForm(string magnitude, string expected)
    {
        SshWireWriter writer = new();

        writer.WriteMpint(Convert.FromHexString(magnitude));

        Assert.AreEqual(expected, Convert.ToHexString(writer.ToArray()));
    }
}
