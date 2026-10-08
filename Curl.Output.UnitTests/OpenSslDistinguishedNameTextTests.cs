using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;
using System.Text;

using Curl.Testing;

namespace Curl.Output;

/// <summary>
/// Pins <see cref="OpenSslDistinguishedNameText"/> to OpenSSL 3.5.7's
/// <c>X509_NAME_print_ex</c> with curl's flags, the same printer as
/// <c>openssl x509 -nameopt oneline,-esc_msb,-space_eq,sep_semi_plus_space</c>, which printed
/// the quoted, escaped, unknown-type and UTF-8 cases below on 2026-09-27 (BL-356 Notes).
/// </summary>
[TestClass]
public sealed class OpenSslDistinguishedNameTextTests
{
    public TestContext TestContext { get; set; } = null!;

    private const string CommonName = "2.5.4.3";
    private const int PrintableString = 19;
    private const int Utf8String = 12;
    private const int TeletexString = 20;
    private const int UniversalString = 28;
    private const int BmpString = 30;

    [TestMethod]
    public void Format_OneAttribute_PrintsShortNameEqualsValue()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("attribute", "CN (UTF8String) localhost");
        string? actual = Format([[Attribute(CommonName, Utf8String, "localhost")]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "CN=localhost", actual);
        Assert.AreEqual("CN=localhost", actual);
    }

    [TestMethod]
    public void Format_SeveralRelativeNames_JoinsThemWithSemicolonsAndMultipleValuesWithPlus()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("relative names", "C=GB (PrintableString); ST=Some + O=Multi (UTF8String); 1.2.3.4=unknown");
        var name = Format(
        [
            [Attribute("2.5.4.6", PrintableString, "GB")],
            [Attribute("2.5.4.8", Utf8String, "Some"), Attribute("2.5.4.10", Utf8String, "Multi")],
            [Attribute("1.2.3.4", Utf8String, "unknown")],
        ]);

        diagnostics.Act("name", name);
        diagnostics.Assert("name", "C=GB; ST=Some + O=Multi; 1.2.3.4=unknown", name);
        Assert.AreEqual("C=GB; ST=Some + O=Multi; 1.2.3.4=unknown", name);
    }

    [TestMethod]
    [DataRow(" Leading, and; special \"q\" back #x", "\" Leading, and; special \\\"q\\\" back #x\"")]
    [DataRow("#hash<gt>", "\"#hash<gt>\"")]
    [DataRow("trail ", "\"trail \"")]
    [DataRow("a+b", "\"a+b\"")]
    [DataRow("#", "#")]
    [DataRow(" ", "\" \"")]
    [DataRow("mid dle#", "mid dle#")]
    [DataRow("back\\slash", "back\\\\slash")]
    [DataRow("\u0001ctl\u007F", "\\01ctl\\7F")]
    public void Format_SpecialCharacters_QuotesOrEscapesAsOpenSsl(string value, string expected)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("CN value", value);
        string? actual = Format([[Attribute(CommonName, Utf8String, value)]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "CN=" + expected, actual);
        Assert.AreEqual("CN=" + expected, actual);
    }

    [TestMethod]
    public void Format_Utf8String_KeepsItsBytes()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("attribute", "O (UTF8String) Caf\u00e9 \u00dcn\u00efcode");
        string? actual = Format([[Attribute("2.5.4.10", Utf8String, "Café Ünïcode")]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "O=Café Ünïcode", actual);
        Assert.AreEqual("O=Café Ünïcode", actual);
    }

    [TestMethod]
    public void Format_OneBytePerCharacterString_ConvertsLatin1ToUtf8()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("attribute", "CN (TeletexString, Latin-1) caf\u00e9");
        string? actual = Format([[Attribute(CommonName, TeletexString, Encoding.Latin1.GetBytes("café"))]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "CN=café", actual);
        Assert.AreEqual("CN=café", actual);
    }

    [TestMethod]
    public void Format_BmpString_ConvertsToUtf8()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("attribute", "CN (BMPString) \u00e9\u20ac");
        string? actual = Format([[Attribute(CommonName, BmpString, Encoding.BigEndianUnicode.GetBytes("é€"))]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "CN=é€", actual);
        Assert.AreEqual("CN=é€", actual);
    }

    [TestMethod]
    public void Format_UniversalString_ConvertsToUtf8AndDropsWhatUtf8CannotHold()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] content = [0x00, 0x01, 0xF6, 0x00, 0x00, 0x00, 0xD8, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x41];
        diagnostics.Arrange("UniversalString content", Convert.ToHexString(content));

        string? actual = Format([[Attribute(CommonName, UniversalString, content)]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "CN=😀A", actual);
        Assert.AreEqual("CN=😀A", actual);
    }

    [TestMethod]
    [DataRow(BmpString, new byte[] { 0x00, 0x41, 0x00 })]
    [DataRow(UniversalString, new byte[] { 0x00, 0x00, 0x41 })]
    public void Format_WideStringOfBrokenLength_CannotBePrinted(int tag, byte[] content)
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        diagnostics.Arrange("tag", tag);
        diagnostics.Arrange("content", Convert.ToHexString(content));
        string? actual = Format([[Attribute(CommonName, tag, content)]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", null, actual);
        Assert.IsNull(actual);
    }

    [TestMethod]
    public void Format_NotAString_DumpsTheDerInHex()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        AsnWriter writer = new(AsnEncodingRules.DER);
        writer.WriteInteger(5);

        diagnostics.Arrange("DER value", Convert.ToHexString(writer.Encode()));
        string? actual = Format([[EncodedAttribute(CommonName, writer.Encode())]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "CN=#020105", actual);
        Assert.AreEqual("CN=#020105", actual);
    }

    [TestMethod]
    public void Format_ContextSpecificValue_DumpsTheDerInHex()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        AsnWriter writer = new(AsnEncodingRules.DER);
        writer.WriteOctetString([0x41], new Asn1Tag(TagClass.ContextSpecific, 0));

        diagnostics.Arrange("DER value", Convert.ToHexString(writer.Encode()));
        string? actual = Format([[EncodedAttribute(CommonName, writer.Encode())]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "CN=#800141", actual);
        Assert.AreEqual("CN=#800141", actual);
    }

    [TestMethod]
    public void Format_ConstructedValue_DumpsTheDerInHex()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteNull();
        }

        diagnostics.Arrange("DER value", Convert.ToHexString(writer.Encode()));
        string? actual = Format([[EncodedAttribute(CommonName, writer.Encode())]]);
        diagnostics.Act("name", actual);
        diagnostics.Assert("name", "CN=#30020500", actual);
        Assert.AreEqual("CN=#30020500", actual);
    }

    [TestMethod]
    public void Format_NotAName_CannotBePrinted()
    {
        TestDiagnostics diagnostics = TestDiagnostics.For(TestContext);
        byte[] der = [0x04, 0x01, 0x00];
        diagnostics.Arrange("name DER", Convert.ToHexString(der));

        string? actual = OpenSslDistinguishedNameText.Format(new X500DistinguishedName(der));

        diagnostics.Act("name", actual);
        diagnostics.Assert("name", null, actual);
        Assert.IsNull(actual);
    }

    private static string? Format(byte[][][] relativeNames)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            foreach (var attributes in relativeNames)
            {
                using (writer.PushSetOf())
                {
                    foreach (var attribute in attributes)
                    {
                        writer.WriteEncodedValue(attribute);
                    }
                }
            }
        }

        return OpenSslDistinguishedNameText.Format(new X500DistinguishedName(writer.Encode()));
    }

    private static byte[] Attribute(string type, int tag, string value)
    {
        return Attribute(type, tag, Encoding.UTF8.GetBytes(value));
    }

    private static byte[] Attribute(string type, int tag, byte[] content)
    {
        // The string types are written by hand: AsnWriter will not put octets under their tags.
        return EncodedAttribute(type, [(byte)tag, (byte)content.Length, .. content]);
    }

    private static byte[] EncodedAttribute(string type, byte[] encodedValue)
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteObjectIdentifier(type);
            writer.WriteEncodedValue(encodedValue);
        }

        return writer.Encode();
    }
}
