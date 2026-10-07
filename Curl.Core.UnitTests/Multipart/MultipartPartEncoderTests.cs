using System.Text;
using Curl.Testing;

namespace Curl.Core.Multipart;

/// <summary>
/// Pins the edges of <see cref="MultipartPartEncoder" />'s quoted-printable and 7-bit rules that
/// the measured bodies in <see cref="MultipartFormBodyBuilderEncoderTests" /> do not reach, as
/// libcurl 8.21.0's <c>encoder_qp_read</c> and <c>encoder_7bit_read</c> define them.
/// </summary>
[TestClass]
public sealed class MultipartPartEncoderTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    [DataRow("a b", "a b")]
    [DataRow("a ", "a=20")]
    [DataRow("a\t", "a=09")]
    [DataRow("a\t\r\nb", "a=09\r\nb")]
    [DataRow("a\rb", "a=0Db")]
    [DataRow("a\r", "a=0D")]
    [DataRow("a\nb", "a=0Ab")]
    [DataRow("=~\u007f\u0000", "=3D~=7F=00")]
    public void QuotedPrintableEscapesAsCurlDoes(string data, string expected)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("data", data);

        string actual = QuotedPrintable(data);

        diagnostics.Act("encoded", actual);
        diagnostics.Diff("encoded", expected, actual);
        diagnostics.Assert("encoded", expected, actual);
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void AQuotedPrintableLineMayFillSeventySixColumnsOnlyBeforeALineBreakOrTheEnd()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        string seventySix = new('x', 76);
        diagnostics.Arrange("line", "76 x characters");

        string plain = QuotedPrintable(seventySix);
        diagnostics.Act("76 columns", plain.Length);
        diagnostics.Assert("76 columns", seventySix, plain);
        Assert.AreEqual(seventySix, plain);
        string beforeBreak = QuotedPrintable(seventySix + "\r\ny");
        diagnostics.Assert("76 columns then line break", seventySix + "\r\ny", beforeBreak);
        Assert.AreEqual(seventySix + "\r\ny", beforeBreak);
        string softBreak = QuotedPrintable(seventySix + "y");
        diagnostics.Assert("77 columns", new string('x', 75) + "=\r\nxy", softBreak);
        Assert.AreEqual(new string('x', 75) + "=\r\nxy", softBreak);
        string escapedBreak = QuotedPrintable(new string('x', 74) + "=");
        diagnostics.Assert("74 columns then an escape", new string('x', 74) + "=\r\n=3D", escapedBreak);
        Assert.AreEqual(new string('x', 74) + "=\r\n=3D", escapedBreak);
    }

    [TestMethod]
    public void SevenBitCarriesAsciiAndRefusesAnyByteAboveOneHundredTwentySeven()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encoding", "7bit");
        MultipartPartEncoder sevenBit = MultipartPartEncoder.Find("7bit")!;

        byte[]? ascii = sevenBit.Encode([0, 0x7F]);
        diagnostics.Act("ascii", ascii is null ? "null" : $"{ascii.Length} bytes");
        diagnostics.Diff("ascii", new byte[] { 0, 0x7F }, ascii ?? []);
        CollectionAssert.AreEqual(new byte[] { 0, 0x7F }, ascii);
        byte[]? high = sevenBit.Encode([0x41, 0x80]);
        diagnostics.Act("0x41 0x80", high is null ? "null" : "encoded");
        diagnostics.Assert("0x41 0x80", null, high);
        Assert.IsNull(high);
        byte[]? top = sevenBit.Encode([0xFF]);
        diagnostics.Act("0xFF", top is null ? "null" : "encoded");
        diagnostics.Assert("0xFF", null, top);
        Assert.IsNull(top);
    }

    [TestMethod]
    public void OnlyQuotedPrintableLeavesAKnownLengthUnknown()
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("encodings", "Quoted-Printable, base64, 7bit, base-64");
        MultipartPartEncoder quotedPrintable = MultipartPartEncoder.Find("Quoted-Printable")!;
        MultipartPartEncoder base64 = MultipartPartEncoder.Find("base64")!;

        long? qpOne = quotedPrintable.EncodedLength(1);
        diagnostics.Act("quoted-printable of 1", qpOne?.ToString() ?? "null");
        diagnostics.Assert("quoted-printable of 1", null, qpOne);
        Assert.IsNull(quotedPrintable.EncodedLength(1));
        diagnostics.Assert("quoted-printable of 0", 0, quotedPrintable.EncodedLength(0));
        Assert.AreEqual(0, quotedPrintable.EncodedLength(0));
        diagnostics.Assert("base64 of 1", 4, base64.EncodedLength(1));
        Assert.AreEqual(4, base64.EncodedLength(1));
        diagnostics.Assert("base64 of unknown", null, base64.EncodedLength(null));
        Assert.IsNull(base64.EncodedLength(null));
        diagnostics.Assert("7bit of 7", 7, MultipartPartEncoder.Find("7bit")!.EncodedLength(7));
        Assert.AreEqual(7, MultipartPartEncoder.Find("7bit")!.EncodedLength(7));
        diagnostics.Assert("base-64 found", null, MultipartPartEncoder.Find("base-64"));
        Assert.IsNull(MultipartPartEncoder.Find("base-64"));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(56)]
    [DataRow(57)]
    [DataRow(58)]
    [DataRow(114)]
    [DataRow(115)]
    [DataRow(1000)]
    public void Base64EncodedLengthIsTheLengthOfItsSeventySixColumnLines(int dataLength)
    {
        var diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("data length", dataLength);
        byte[] data = new byte[dataLength];
        byte[] encoded = MultipartPartEncoder.Find("base64")!.Encode(data)!;

        string text = Encoding.ASCII.GetString(encoded);
        string expected = Convert.ToBase64String(data, Base64FormattingOptions.InsertLineBreaks);
        diagnostics.Act("encoded length", encoded.Length);
        diagnostics.Diff("encoded text", expected, text);
        Assert.AreEqual(expected, text);
        diagnostics.Assert("EncodedLength", encoded.Length, Base64DataEncoding.EncodedLength(dataLength));
        Assert.AreEqual(encoded.Length, Base64DataEncoding.EncodedLength(dataLength));
    }

    private static string QuotedPrintable(string data) =>
        Encoding.Latin1.GetString(MultipartPartEncoder.Find("quoted-printable")!.Encode(Encoding.Latin1.GetBytes(data))!);
}
