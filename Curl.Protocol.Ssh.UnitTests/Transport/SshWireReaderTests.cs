using Curl.Protocol.Ssh.Authentication;
using Curl.Testing;

namespace Curl.Protocol.Ssh.Transport;

[TestClass]
public sealed class SshWireReaderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Reader_ReadsWhatTheWriterWrote()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        SshWireWriter writer = new();
        writer.WriteByte(7);
        writer.WriteBoolean(true);
        writer.WriteBoolean(false);
        writer.WriteUInt32(uint.MaxValue);
        writer.WriteString([1, 2, 3]);
        writer.WriteNameList(["curve25519-sha256", "ext-info-c"]);
        diagnostics.Arrange("values", "byte 7, true, false, uint32 0xFFFFFFFF, string 01 02 03, name list curve25519-sha256,ext-info-c, empty name list");
        writer.WriteNameList([]);
        diagnostics.Bytes("written", writer.ToArray());
        SshWireReader reader = new(writer.ToArray());

        byte byteValue = reader.ReadByte();
        bool trueValue = reader.ReadBoolean();
        bool falseValue = reader.ReadBoolean();
        uint number = reader.ReadUInt32();
        byte[] stringValue = reader.ReadString().ToArray();
        string[] names = reader.ReadNameList().ToArray();
        int emptyNameCount = reader.ReadNameList().Count;
        diagnostics.Act("byte", byteValue);
        diagnostics.Act("first boolean", trueValue);
        diagnostics.Act("second boolean", falseValue);
        diagnostics.Act("uint32", number);
        diagnostics.Bytes("string", stringValue);
        diagnostics.Act("name list", SshAuthenticationDiagnostics.Lines(names));
        diagnostics.Act("empty name list count", emptyNameCount);

        diagnostics.Assert("byte", (byte)7, byteValue);
        diagnostics.Assert("first boolean", true, trueValue);
        diagnostics.Assert("second boolean", false, falseValue);
        diagnostics.Assert("uint32", uint.MaxValue, number);
        diagnostics.Diff("string", new byte[] { 1, 2, 3 }, stringValue);
        diagnostics.AssertLines("name list", ["curve25519-sha256", "ext-info-c"], names);
        diagnostics.Assert("empty name list count", 0, emptyNameCount);
        Assert.AreEqual((byte)7, byteValue);
        Assert.IsTrue(trueValue);
        Assert.IsFalse(falseValue);
        Assert.AreEqual(uint.MaxValue, number);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, stringValue);
        CollectionAssert.AreEqual(new[] { "curve25519-sha256", "ext-info-c" }, names);
        Assert.AreEqual(0, emptyNameCount);
    }

    [TestMethod]
    public void Reader_AnyNonZeroByteIsTrue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] encoded = [0x80];
        diagnostics.Arrange("encoded byte count", encoded.Length);
        diagnostics.Bytes("encoded", encoded);

        bool value = new SshWireReader(encoded).ReadBoolean();
        diagnostics.Act("boolean", value);

        diagnostics.Assert("boolean", true, value);
        Assert.IsTrue(value);
    }

    [TestMethod]
    public void ReadByte_AtTheEnd_ThrowsInvalidData()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded", "(empty)");

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() => new SshWireReader(Array.Empty<byte>()).ReadByte());
        diagnostics.Act("exception", $"{failure.GetType().Name}: {SshAuthenticationDiagnostics.Text(failure.Message)}");

        diagnostics.Assert("exception type", nameof(InvalidDataException), failure.GetType().Name);
        Assert.AreEqual(typeof(InvalidDataException), failure.GetType());
    }

    [TestMethod]
    public void ReadUInt32_WithThreeBytesLeft_ThrowsInvalidData()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded", "00 00 00 (a uint32 needs 4 bytes)");
        diagnostics.Bytes("encoded", [0, 0, 0]);

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() => new SshWireReader(new byte[] { 0, 0, 0 }).ReadUInt32());
        diagnostics.Act("exception", $"{failure.GetType().Name}: {SshAuthenticationDiagnostics.Text(failure.Message)}");

        diagnostics.Assert("exception type", nameof(InvalidDataException), failure.GetType().Name);
        Assert.AreEqual(typeof(InvalidDataException), failure.GetType());
    }

    [TestMethod]
    [DataRow(new byte[] { 0, 0, 0, 3, 1, 2 }, DisplayName = "one byte short")]
    [DataRow(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 1 }, DisplayName = "length 2^32 - 1")]
    public void ReadString_LengthPastTheEnd_ThrowsInvalidData(byte[] payload)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded byte count", payload.Length);
        diagnostics.Bytes("encoded", payload);

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() => new SshWireReader(payload).ReadString());
        diagnostics.Act("exception", $"{failure.GetType().Name}: {SshAuthenticationDiagnostics.Text(failure.Message)}");

        diagnostics.Assert("exception type", nameof(InvalidDataException), failure.GetType().Name);
        Assert.AreEqual(typeof(InvalidDataException), failure.GetType());
    }

    [TestMethod]
    [DataRow("00000000", "", DisplayName = "zero")]
    [DataRow("000000020080", "80", DisplayName = "the sign byte is dropped")]
    [DataRow("0000000300000A", "0A", DisplayName = "redundant leading zeros are dropped")]
    [DataRow("000000020000", "", DisplayName = "zeros alone are zero")]
    public void ReadMpint_ReturnsTheMagnitude(string encoded, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded hex", encoded);
        SshWireReader reader = new(Convert.FromHexString(encoded));

        string magnitude = Convert.ToHexString(reader.ReadMpint().Span);
        diagnostics.Act("magnitude hex", magnitude);

        diagnostics.Diff("magnitude hex", expected, magnitude);
        Assert.AreEqual(expected, magnitude);
    }

    [TestMethod]
    public void ReadMpint_Negative_ThrowsInvalidData()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded hex", "00000001FF");

        InvalidDataException failure = Assert.ThrowsExactly<InvalidDataException>(() => new SshWireReader(Convert.FromHexString("00000001FF")).ReadMpint());
        diagnostics.Act("exception", $"{failure.GetType().Name}: {SshAuthenticationDiagnostics.Text(failure.Message)}");

        diagnostics.Assert("exception type", nameof(InvalidDataException), failure.GetType().Name);
        Assert.AreEqual(typeof(InvalidDataException), failure.GetType());
    }

    [TestMethod]
    public void ReadName_ReadsAnAsciiString()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoded hex", "000000077373682D727361");

        string name = new SshWireReader(Convert.FromHexString("000000077373682D727361")).ReadName();
        diagnostics.Act("name", SshAuthenticationDiagnostics.Text(name));

        diagnostics.Diff("name", "ssh-rsa", name);
        Assert.AreEqual("ssh-rsa", name);
    }
}
