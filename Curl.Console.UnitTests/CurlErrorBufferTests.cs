namespace Curl.Console;

/// <summary>
/// Pins <see cref="CurlErrorBuffer" />: a message of up to 255 UTF-8 bytes is kept whole, a
/// longer one is cut to 255 bytes, and a character the limit falls inside is dropped whole.
/// </summary>
[TestClass]
public sealed class CurlErrorBufferTests
{
    [TestMethod]
    public void Truncate_Null_ReturnsNull()
    {
        Assert.IsNull(CurlErrorBuffer.Truncate(null));
    }

    [TestMethod]
    public void Truncate_MessageOf255Bytes_ReturnsItWhole()
    {
        string message = new('a', 255);

        Assert.AreSame(message, CurlErrorBuffer.Truncate(message));
    }

    [TestMethod]
    public void Truncate_AsciiMessageOver255Bytes_ReturnsFirst255()
    {
        string message = new('a', 300);

        Assert.AreEqual(message[..255], CurlErrorBuffer.Truncate(message));
    }

    [TestMethod]
    public void Truncate_TwoByteCharacterAcrossTheLimit_DropsItWhole()
    {
        // 254 ASCII bytes, then "é" (C3 A9) takes bytes 255 and 256: only its first fits.
        string message = new string('a', 254) + "éé";

        Assert.AreEqual(new string('a', 254), CurlErrorBuffer.Truncate(message));
    }

    [TestMethod]
    public void Truncate_ThreeByteCharacterEndingAtTheLimit_KeepsIt()
    {
        // 252 ASCII bytes, then "€" (E2 82 AC) is bytes 253 to 255.
        string message = new string('a', 252) + "€€";

        Assert.AreEqual(new string('a', 252) + "€", CurlErrorBuffer.Truncate(message));
    }
}
