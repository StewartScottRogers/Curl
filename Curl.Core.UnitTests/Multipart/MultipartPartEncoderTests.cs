using System.Text;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the edges of <see cref="MultipartPartEncoder" />'s quoted-printable and 7-bit rules that
/// the measured bodies in <see cref="MultipartFormBodyBuilderEncoderTests" /> do not reach, as
/// libcurl 8.21.0's <c>encoder_qp_read</c> and <c>encoder_7bit_read</c> define them.
/// </summary>
[TestClass]
public sealed class MultipartPartEncoderTests
{
    [TestMethod]
    [DataRow("a b", "a b")]
    [DataRow("a ", "a=20")]
    [DataRow("a\t", "a=09")]
    [DataRow("a\t\r\nb", "a=09\r\nb")]
    [DataRow("a\rb", "a=0Db")]
    [DataRow("a\r", "a=0D")]
    [DataRow("a\nb", "a=0Ab")]
    [DataRow("=~\u007f\u0000", "=3D~=7F=00")]
    public void QuotedPrintableEscapesAsCurlDoes(string data, string expected) =>
        Assert.AreEqual(expected, QuotedPrintable(data));

    [TestMethod]
    public void AQuotedPrintableLineMayFillSeventySixColumnsOnlyBeforeALineBreakOrTheEnd()
    {
        string seventySix = new('x', 76);

        Assert.AreEqual(seventySix, QuotedPrintable(seventySix));
        Assert.AreEqual(seventySix + "\r\ny", QuotedPrintable(seventySix + "\r\ny"));
        Assert.AreEqual(new string('x', 75) + "=\r\nxy", QuotedPrintable(seventySix + "y"));
        Assert.AreEqual(new string('x', 74) + "=\r\n=3D", QuotedPrintable(new string('x', 74) + "="));
    }

    [TestMethod]
    public void SevenBitCarriesAsciiAndRefusesAnyByteAboveOneHundredTwentySeven()
    {
        MultipartPartEncoder sevenBit = MultipartPartEncoder.Find("7bit")!;

        CollectionAssert.AreEqual(new byte[] { 0, 0x7F }, sevenBit.Encode([0, 0x7F]));
        Assert.IsNull(sevenBit.Encode([0x41, 0x80]));
        Assert.IsNull(sevenBit.Encode([0xFF]));
    }

    [TestMethod]
    public void OnlyQuotedPrintableLeavesAKnownLengthUnknown()
    {
        MultipartPartEncoder quotedPrintable = MultipartPartEncoder.Find("Quoted-Printable")!;
        MultipartPartEncoder base64 = MultipartPartEncoder.Find("base64")!;

        Assert.IsFalse(quotedPrintable.KnowsEncodedLength(1));
        Assert.IsTrue(quotedPrintable.KnowsEncodedLength(0));
        Assert.IsTrue(base64.KnowsEncodedLength(1));
        Assert.IsFalse(base64.KnowsEncodedLength(null));
        Assert.IsNull(MultipartPartEncoder.Find("base-64"));
    }

    private static string QuotedPrintable(string data) =>
        Encoding.Latin1.GetString(MultipartPartEncoder.Find("quoted-printable")!.Encode(Encoding.Latin1.GetBytes(data))!);
}
