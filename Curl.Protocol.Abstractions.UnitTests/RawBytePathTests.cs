using Curl.Testing;

namespace Curl.Protocol.Abstractions;

/// <summary>
/// Pins <see cref="RawBytePath" />'s byte-keeping text form (CGR-114, BL-3899): a byte that is not
/// valid UTF-8 becomes U+DC00 + the byte and comes back as itself, and valid UTF-8 is unchanged.
/// </summary>
[TestClass]
public sealed class RawBytePathTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void FromBytes_LatinOneE9_KeepsTheByteAsItsSurrogate()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bytes", "63 61 66 E9 2E 74 78 74");

        string path = RawBytePath.FromBytes([0x63, 0x61, 0x66, 0xE9, 0x2E, 0x74, 0x78, 0x74]);

        diagnostics.Act("path", path);
        Assert.AreEqual("caf\uDCE9.txt", path);
    }

    [TestMethod]
    public void FromBytes_ValidUtf8_IsThePlainText()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bytes", "café.txt as UTF-8");

        string path = RawBytePath.FromBytes("café.txt"u8);

        diagnostics.Act("path", path);
        Assert.AreEqual("café.txt", path);
        Assert.IsFalse(RawBytePath.HoldsRawByte(path));
    }

    [TestMethod]
    // Expected UTF-16 code units in hexadecimal: an attribute's string cannot hold a lone surrogate.
    [DataRow(new byte[] { 0x2F, 0x80 }, "002F DC80")]
    [DataRow(new byte[] { 0xFF, 0x41 }, "DCFF 0041")]
    [DataRow(new byte[] { 0xC3 }, "DCC3")]
    [DataRow(new byte[] { 0xF0, 0x9F, 0x98, 0x80, 0xFE }, "D83D DE00 DCFE")]
    public void FromBytes_ThenToBytes_KeepsEveryByte(byte[] bytes, string expectedCodeUnits)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("bytes", Convert.ToHexString(bytes));

        string path = RawBytePath.FromBytes(bytes);
        byte[] roundTripped = RawBytePath.ToBytes(path);

        diagnostics.Act("round trip", Convert.ToHexString(roundTripped));
        Assert.AreEqual(expectedCodeUnits, string.Join(" ", path.Select(c => ((int)c).ToString("X4", System.Globalization.CultureInfo.InvariantCulture))));
        Assert.IsTrue(RawBytePath.HoldsRawByte(path));
        CollectionAssert.AreEqual(bytes, roundTripped);
    }

    [TestMethod]
    public void ToBytes_LoneSurrogateOutsideTheRawByteRange_EncodesAsReplacementCharacter()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("path", "a U+DC10 b");

        byte[] bytes = RawBytePath.ToBytes("a\uDC10b");

        diagnostics.Act("bytes", Convert.ToHexString(bytes));
        Assert.AreEqual("61EFBFBD62", Convert.ToHexString(bytes));
        Assert.IsFalse(RawBytePath.HoldsRawByte("a\uDC10b"));
    }

    [TestMethod]
    public void HoldsRawByte_NullPath_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => RawBytePath.HoldsRawByte(null!));
    }

    [TestMethod]
    public void ToBytes_NullPath_Throws()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => RawBytePath.ToBytes(null!));
    }
}
