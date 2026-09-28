namespace Curl.Networking;

/// <summary>
/// Pins <see cref="CurlErrorBuffer" />: a message of up to 255 characters is kept whole, and a
/// longer one is cut to its first 255, as curl 8.21.0's 256-byte error buffer cuts it.
/// </summary>
[TestClass]
public sealed class CurlErrorBufferTests
{
    [TestMethod]
    [DataRow(0)]
    [DataRow(254)]
    [DataRow(255)]
    public void Truncate_WithMessageOfAtMost255Characters_ReturnsItWhole(int length)
    {
        var message = new string('m', length);

        Assert.AreSame(message, CurlErrorBuffer.Truncate(message));
    }

    [TestMethod]
    [DataRow(256)]
    [DataRow(65559)]
    public void Truncate_WithMessageLongerThan255Characters_ReturnsItsFirst255(int length)
    {
        var message = "Could not resolve host: " + new string('a', length - 24);

        Assert.AreEqual(message[..255], CurlErrorBuffer.Truncate(message));
    }
}
