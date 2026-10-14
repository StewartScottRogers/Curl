namespace Curl.HttpMessageSignatures;

/// <summary>Checks the hex key decoder against curl 8.22.0's <c>decode_hex_key</c> rules and messages.</summary>
[TestClass]
public sealed class HttpSignatureKeyTests
{
    [TestMethod]
    public void DecodeHex_DigitsInEitherCaseWithTrailingLineEnd_ReturnsTheBytes()
    {
        var key = HttpSignatureKey.DecodeHex("09aFAf\r\n\n");

        CollectionAssert.AreEqual(new byte[] { 0x09, 0xaf, 0xaf }, key);
    }

    [TestMethod]
    [DataRow("", "httpsig: invalid hex key (length 0)")]
    [DataRow("\n", "httpsig: invalid hex key (length 0)")]
    [DataRow("abc", "httpsig: invalid hex key (length 3)")]
    [DataRow("0g", "httpsig: invalid hex at position 0 ('0g')")]
    [DataRow("00g0", "httpsig: invalid hex at position 2 ('g0')")]
    public void DecodeHex_BadText_ThrowsCurlsMessage(string hexKey, string message)
    {
        var exception = Assert.ThrowsExactly<HttpSignatureException>(() => HttpSignatureKey.DecodeHex(hexKey));

        Assert.AreEqual(message, exception.Message);
    }

    [TestMethod]
    public void DecodeHex_LongerThanCurlsInputLimit_ThrowsTooLong()
    {
        var hexKey = new string('0', HttpSignatureKey.MaximumHexLength + 2);

        var exception = Assert.ThrowsExactly<HttpSignatureException>(() => HttpSignatureKey.DecodeHex(hexKey));

        Assert.AreEqual("httpsig: hex key too long", exception.Message);
    }

    [TestMethod]
    public void DecodeHex_AtCurlsInputLimit_ReturnsHalfAsManyBytes()
    {
        var hexKey = new string('f', HttpSignatureKey.MaximumHexLength);

        var key = HttpSignatureKey.DecodeHex(hexKey);

        Assert.HasCount(HttpSignatureKey.MaximumHexLength / 2, key);
    }
}
