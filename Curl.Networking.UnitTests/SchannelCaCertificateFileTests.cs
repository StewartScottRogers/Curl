using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Testing;

namespace Curl.Networking;

/// <summary>
/// <see cref="SchannelCaCertificateFile" /> against the <c>--cacert</c> texts measured with curl
/// 8.22.0 Schannel (BL-5884, ADR-1396): which are refused with exit 77, with which line, and which
/// are read.
/// </summary>
[TestClass]
public sealed class SchannelCaCertificateFileTests
{
    private const string Begin = "-----BEGIN CERTIFICATE-----";

    private const string End = "-----END CERTIFICATE-----";

    private const string Name = "root.pem";

    private const string NotFound = "schannel: failed to extract certificate from CA file 'root.pem': Cannot find the requested object.";

    private const string EncodeOrDecodeError = "schannel: failed to extract certificate from CA file 'root.pem': An error occurred during encode or decode operation.";

    private const string NotFormatted = "schannel: CA file 'root.pem' is not correctly formatted";

    private static readonly string s_base64 = CreateCertificateBase64();

    /// <summary>Gets or sets the MSTest context, which carries each test's diagnostics.</summary>
    public TestContext TestContext { get; set; } = null!;

    private static string Pem => $"{Begin}\n{s_base64}\n{End}\n";

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow("", DisplayName = "empty file")]
    [DataRow("this is not a certificate\n", DisplayName = "plain text")]
    [DataRow("-----BEGIN TRUSTED CERTIFICATE-----\nAAAA\n-----END TRUSTED CERTIFICATE-----\n", DisplayName = "trusted certificate block")]
    [DataRow("-----BEGIN CERTIFICATE-----", DisplayName = "begin marker at the end of the text")]
    [DataRow("-----BEGIN CERTIFICATE-----AAAA\n-----END CERTIFICATE-----\n", DisplayName = "base64 on the begin line")]
    public void Read_WithNoBlockCurlReads_ReturnsNoCertificate(string text)
    {
        Diagnostics.Arrange("text", text);

        var certificates = SchannelCaCertificateFile.Read(text, Name);

        Diagnostics.Act("certificates", certificates.Count);
        Diagnostics.Assert("certificates", 0, certificates.Count);
        Assert.IsEmpty(certificates);
    }

    [TestMethod]
    [DataRow("plain", DisplayName = "plain block")]
    [DataRow("crlf", DisplayName = "CRLF line ends")]
    [DataRow("unpadded", DisplayName = "padding removed")]
    [DataRow("spaced", DisplayName = "spaces and blank lines in the body")]
    [DataRow("after padding", DisplayName = "text after the padding")]
    [DataRow("lone character", DisplayName = "a lone trailing character")]
    [DataRow("surrounded", DisplayName = "text before the begin and after the end marker")]
    [DataRow("trailing bytes", DisplayName = "bytes after the certificate's DER")]
    public void Read_WithACertificateBlockWindowsDecodes_ReturnsTheCertificate(string variant)
    {
        var text = variant switch
        {
            "plain" => Pem,
            "crlf" => Pem.Replace("\n", "\r\n", StringComparison.Ordinal),
            "unpadded" => $"{Begin}\n{s_base64.TrimEnd('=')}\n{End}\n",
            "spaced" => $"{Begin}\n\n {s_base64[..10]}\t{s_base64[10..]}\n\n{End}\n",
            "after padding" => $"{Begin}\n{PaddedBase64()}!!!!AAAA\n{End}\n",
            "lone character" => $"{Begin}\n{UnpaddedBase64()}A\n{End}\n",
            "surrounded" => $"text {Begin}\n{s_base64}\n{End} text\n",
            _ => $"{Begin}\n{Convert.ToBase64String([.. Convert.FromBase64String(s_base64), 1, 2, 3])}\n{End}\n",
        };
        Diagnostics.Arrange("variant", variant);

        var certificates = SchannelCaCertificateFile.Read(text, Name);

        Diagnostics.Act("certificates", certificates.Count);
        Diagnostics.Assert("certificates", 1, certificates.Count);
        Assert.HasCount(1, certificates);
    }

    [TestMethod]
    public void Read_WithTwoBlocks_ReturnsBothInFileOrder()
    {
        var other = CreateCertificateBase64();
        var text = $"{Pem}{Begin}\n{other}\n{End}\n";
        Diagnostics.Arrange("blocks", 2);

        var certificates = SchannelCaCertificateFile.Read(text, Name);

        Diagnostics.Act("certificates", certificates.Count);
        Diagnostics.Assert("second", other, Convert.ToBase64String(certificates[1].RawData));
        Assert.AreEqual(s_base64, Convert.ToBase64String(certificates[0].RawData));
        Assert.AreEqual(other, Convert.ToBase64String(certificates[1].RawData));
    }

    [TestMethod]
    public void Read_WithAnEndMarkerInsideTheBeginLine_EndsTheBodyThereAndGoesOnAfterTheEndLine()
    {
        var text = $"{Begin}\n{s_base64}{End}\n{Begin}\n{s_base64}\n{End}\n";
        Diagnostics.Arrange("text", text);

        var certificates = SchannelCaCertificateFile.Read(text, Name);

        Diagnostics.Act("certificates", certificates.Count);
        Diagnostics.Assert("certificates", 1, certificates.Count);
        Assert.HasCount(1, certificates);
    }

    [TestMethod]
    [DataRow("!!!!notbase64!!!!", DisplayName = "rootbad64: not base64")]
    [DataRow("AAAA", DisplayName = "rootgarb: base64 of no certificate")]
    [DataRow("AA", DisplayName = "two characters")]
    [DataRow("A", DisplayName = "one character")]
    [DataRow("AA==AAAA", DisplayName = "padding before more base64")]
    [DataRow("Proc-Type: 4,ENCRYPTED", DisplayName = "a header line")]
    public void Read_WithABlockThatYieldsNoCertificate_RefusesTheFileAsNotFound(string body)
    {
        var text = Pem + $"{Begin}\n{body}\n{End}\n";
        Diagnostics.Arrange("body", body);

        var exception = Assert.ThrowsExactly<SchannelCaCertificateFileException>(() => SchannelCaCertificateFile.Read(text, Name));

        Diagnostics.Act("message", exception.Message);
        Diagnostics.Assert("message", NotFound, exception.Message);
        Assert.AreEqual(NotFound, exception.Message);
        Assert.IsInstanceOfType<CryptographicException>(exception);
    }

    [TestMethod]
    public void Read_WithABadBlockBeforeAGoodOne_RefusesTheFile()
    {
        var text = $"{Begin}\nAAAA\n{End}\n" + Pem;
        Diagnostics.Arrange("text", text);

        var exception = Assert.ThrowsExactly<SchannelCaCertificateFileException>(() => SchannelCaCertificateFile.Read(text, Name));

        Diagnostics.Act("message", exception.Message);
        Diagnostics.Assert("message", NotFound, exception.Message);
        Assert.AreEqual(NotFound, exception.Message);
    }

    [TestMethod]
    [DataRow("\n", DisplayName = "empty body")]
    [DataRow("\n \t\n\n", DisplayName = "white space only")]
    [DataRow("\n==\n", DisplayName = "padding only")]
    public void Read_WithAnEmptyBody_RefusesTheFileAsAnEncodeOrDecodeError(string body)
    {
        var text = $"{Begin}{body}{End}\n";
        Diagnostics.Arrange("body", body);

        var exception = Assert.ThrowsExactly<SchannelCaCertificateFileException>(() => SchannelCaCertificateFile.Read(text, Name));

        Diagnostics.Act("message", exception.Message);
        Diagnostics.Assert("message", EncodeOrDecodeError, exception.Message);
        Assert.AreEqual(EncodeOrDecodeError, exception.Message);
    }

    [TestMethod]
    [DataRow("lone begin line", DisplayName = "roottrunc: a lone begin line")]
    [DataRow("cr only", DisplayName = "CR-only line ends")]
    public void Read_WithABeginLineAndNoEndLineAfterIt_RefusesTheFileAsNotCorrectlyFormatted(string variant)
    {
        var text = variant == "lone begin line" ? Pem + Begin + "\n" : Pem.Replace("\n", "\r", StringComparison.Ordinal);
        Diagnostics.Arrange("variant", variant);

        var exception = Assert.ThrowsExactly<SchannelCaCertificateFileException>(() => SchannelCaCertificateFile.Read(text, Name));

        Diagnostics.Act("message", exception.Message);
        Diagnostics.Assert("message", NotFormatted, exception.Message);
        Assert.AreEqual(NotFormatted, exception.Message);
    }

    // The certificate's DER with zero bytes after it, up to a length leaving the given remainder
    // over three, so its base64 ends in padding (remainder 1 or 2) or in none (remainder 0).
    private static string PaddedBase64() => Base64WithRemainder(1);

    private static string UnpaddedBase64() => Base64WithRemainder(0);

    private static string Base64WithRemainder(int remainder)
    {
        var der = Convert.FromBase64String(s_base64).ToList();
        while (der.Count % 3 != remainder)
        {
            der.Add(0);
        }

        return Convert.ToBase64String([.. der]);
    }

    private static string CreateCertificateBase64()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest("CN=Schannel CA file test", key, HashAlgorithmName.SHA256);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return Convert.ToBase64String(certificate.RawData);
    }
}
