using System.Buffers;
using System.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins that <see cref="Base64DataEncoding" /> encodes each complete 3-byte group as soon as it has it, as
/// libcurl 8.22.0's <c>encoder_base64_read</c> does (upstream test667, BL-2630), breaking lines at 76 columns
/// across calls.
/// </summary>
[TestClass]
public sealed class Base64DataEncodingTests
{
    [TestMethod]
    public void TryEncode_ACompleteGroupBeforeTheEnd_EncodesItAtOnce()
    {
        Base64DataEncoding encoding = new();
        ArrayBufferWriter<byte> output = new();

        bool encoded = encoding.TryEncode("dum"u8, isFinal: false, output, out int consumed);

        Assert.IsTrue(encoded);
        Assert.AreEqual(3, consumed);
        Assert.AreEqual("ZHVt", Encoding.ASCII.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void TryEncode_AnIncompleteGroupBeforeTheEnd_KeepsItForTheNextCall()
    {
        Base64DataEncoding encoding = new();
        ArrayBufferWriter<byte> output = new();

        _ = encoding.TryEncode("dummy"u8, isFinal: false, output, out int consumed);

        Assert.AreEqual(3, consumed);
        Assert.AreEqual("ZHVt", Encoding.ASCII.GetString(output.WrittenSpan));
    }

    [TestMethod]
    public void TryEncode_AGroupAfterAFullLineInAnEarlierCall_StartsANewLine()
    {
        Base64DataEncoding encoding = new();
        ArrayBufferWriter<byte> output = new();
        _ = encoding.TryEncode(new byte[Base64DataEncoding.BytesPerLine], isFinal: false, output, out _);

        _ = encoding.TryEncode("dum"u8, isFinal: true, output, out int consumed);

        string text = Encoding.ASCII.GetString(output.WrittenSpan);
        Assert.AreEqual(3, consumed);
        Assert.AreEqual(new string('A', 76) + "\r\nZHVt", text);
    }
}
