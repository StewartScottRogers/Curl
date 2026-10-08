using Curl.Testing;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshWireWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Writer_WritesEachRfc4251TypeBigEndianWithLengthPrefixes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("values", "byte 0x14, true, false, uint32 0x01020304, string AA BB, name list a,bc, empty name list");
        SshWireWriter writer = new();
        writer.WriteByte(0x14);
        writer.WriteBoolean(true);
        writer.WriteBoolean(false);
        writer.WriteUInt32(0x01020304);
        writer.WriteString([0xAA, 0xBB]);
        writer.WriteNameList(["a", "bc"]);
        writer.WriteNameList([]);

        byte[] written = writer.ToArray();
        byte[] expected = [0x14, 1, 0, 1, 2, 3, 4, 0, 0, 0, 2, 0xAA, 0xBB, 0, 0, 0, 4, (byte)'a', (byte)',', (byte)'b', (byte)'c', 0, 0, 0, 0];
        diagnostics.Act("written byte count", written.Length);
        diagnostics.Bytes("written", written);

        diagnostics.Diff("written", expected, written);
        CollectionAssert.AreEqual(expected, written);
    }

    [TestMethod]
    [DataRow("", "00000000", DisplayName = "zero is an empty string (RFC 4251 section 5)")]
    [DataRow("000000", "00000000", DisplayName = "leading zeros alone are zero")]
    [DataRow("0009A378F9B2E332A7", "0000000809A378F9B2E332A7", DisplayName = "RFC 4251's 9a378f9b2e332a7")]
    [DataRow("80", "000000020080", DisplayName = "RFC 4251's 0x80 gets a sign byte")]
    public void WriteMpint_WritesTheShortestNonNegativeForm(string magnitude, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("magnitude hex", magnitude);
        SshWireWriter writer = new();

        writer.WriteMpint(Convert.FromHexString(magnitude));
        string written = Convert.ToHexString(writer.ToArray());
        diagnostics.Act("written hex", written);

        diagnostics.Diff("written hex", expected, written);
        Assert.AreEqual(expected, written);
    }
}
