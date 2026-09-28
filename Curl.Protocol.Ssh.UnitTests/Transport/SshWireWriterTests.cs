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
}
