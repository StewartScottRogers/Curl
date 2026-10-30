using Curl.Extension.CompressionDictionary.Brotli;

namespace Curl.Extension.CompressionDictionary.UnitTests.Brotli;

[TestClass]
public sealed class BrotliLiteralContextTests
{
    // RFC 7932 section 7.1: LSB6, MSB6, UTF8 (Lut0 | Lut1) and Signed ((Lut2 << 3) | Lut2).
    [TestMethod]
    [DataRow(0, 0x41, 0, 1)]
    [DataRow(1, 0x41, 0, 16)]
    [DataRow(2, (int)'a', (int)' ', 56)]
    [DataRow(2, 0xC5, 0xC5, 3)]
    [DataRow(3, 0xFF, 0x10, 58)]
    public void Of_Mode_GivesSection71Context(int mode, int last, int secondLast, int expected) =>
        Assert.AreEqual(expected, BrotliLiteralContext.Of(mode, (byte)last, (byte)secondLast));
}
