using System.Text;
using Curl.Protocol.Abstractions;
using HttpRequestOptions = Curl.Protocol.Abstractions.HttpRequestOptions;

namespace Curl.Original.Protocol.Http;

/// <summary>
/// Pins CGR-020 (BL-3549): a header file's bytes UTF-8 cannot read reach the request head as lone
/// surrogates U+DC80 to U+DCFF, and the head gives each back as its own byte, as curl 8.22.0 sends a
/// header file's bytes unchanged (measured: <c>X: E9 FF</c> goes out as <c>X: E9 FF</c>).
/// </summary>
[TestClass]
public sealed class Cgr020Tests
{
    [TestMethod]
    public void Format_HeaderHoldsEscapedBytes_SendsTheBytesUnchanged()
    {
        HttpRequestOptions options = new() { Headers = ["X: \uDCE9\uDCFF"], CommandLineTextEncoding = Encoding.UTF8 };

        byte[] head = HttpRequestHeadFormatter.Format(CurlUrl.Parse("http://example.com/a"), options);

        Assert.Contains("\r\nX: éÿ\r\n", Encoding.Latin1.GetString(head));
    }

    [TestMethod]
    public void WireText_EscapeFirstAndBetweenText_GivesEachEscapedByteAndEncodesTheRest()
    {
        string wire = HttpRequestHeadFormatter.WireText("\uDC80aé\uDCC3(", Encoding.UTF8);

        Assert.AreEqual("\u0080aÃ©Ã(", wire);
    }

    [TestMethod]
    public void WireText_LowSurrogateInAPair_EncodesThePairNotAByte()
    {
        string wire = HttpRequestHeadFormatter.WireText("💀", Encoding.UTF8);

        Assert.AreEqual("ð\u009F\u0092\u0080", wire);
    }

    [TestMethod]
    public void WireText_SurrogateOutsideTheEscapedRange_IsNotGivenAsAByte()
    {
        string wire = HttpRequestHeadFormatter.WireText("\uDC7F\uDD00", Encoding.UTF8);

        Assert.AreEqual("ï¿½ï¿½", wire);
    }
}
