using System.Formats.Asn1;
using System.Security.Cryptography.X509Certificates;
using System.Text;

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
    private const string CommonName = "2.5.4.3";
    private const int PrintableString = 19;
    private const int Utf8String = 12;
    private const int TeletexString = 20;
    private const int UniversalString = 28;
    private const int BmpString = 30;

    [TestMethod]
    public void Format_OneAttribute_PrintsShortNameEqualsValue()
    {
        Assert.AreEqual("CN=localhost", Format([[Attribute(CommonName, Utf8String, "localhost")]]));
    }

    [TestMethod]
    public void Format_SeveralRelativeNames_JoinsThemWithSemicolonsAndMultipleValuesWithPlus()
    {
        var name = Format(
        [
            [Attribute("2.5.4.6", PrintableString, "GB")],
            [Attribute("2.5.4.8", Utf8String, "Some"), Attribute("2.5.4.10", Utf8String, "Multi")],
            [Attribute("1.2.3.4", Utf8String, "unknown")],
        ]);

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
        Assert.AreEqual("CN=" + expected, Format([[Attribute(CommonName, Utf8String, value)]]));
    }

    [TestMethod]
    public void Format_Utf8String_KeepsItsBytes()
    {
        Assert.AreEqual("O=Café Ünïcode", Format([[Attribute("2.5.4.10", Utf8String, "Café Ünïcode")]]));
    }

    [TestMethod]
    public void Format_OneBytePerCharacterString_ConvertsLatin1ToUtf8()
    {
        Assert.AreEqual("CN=café", Format([[Attribute(CommonName, TeletexString, Encoding.Latin1.GetBytes("café"))]]));
    }

    [TestMethod]
    public void Format_BmpString_ConvertsToUtf8()
    {
        Assert.AreEqual("CN=é€", Format([[Attribute(CommonName, BmpString, Encoding.BigEndianUnicode.GetBytes("é€"))]]));
    }

    [TestMethod]
    public void Format_UniversalString_ConvertsToUtf8AndDropsWhatUtf8CannotHold()
    {
        byte[] content = [0x00, 0x01, 0xF6, 0x00, 0x00, 0x00, 0xD8, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x41];

        Assert.AreEqual("CN=😀A", Format([[Attribute(CommonName, UniversalString, content)]]));
    }

    [TestMethod]
    [DataRow(BmpString, new byte[] { 0x00, 0x41, 0x00 })]
    [DataRow(UniversalString, new byte[] { 0x00, 0x00, 0x41 })]
    public void Format_WideStringOfBrokenLength_CannotBePrinted(int tag, byte[] content)
    {
        Assert.IsNull(Format([[Attribute(CommonName, tag, content)]]));
    }

    [TestMethod]
    public void Format_NotAString_DumpsTheDerInHex()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        writer.WriteInteger(5);

        Assert.AreEqual("CN=#020105", Format([[EncodedAttribute(CommonName, writer.Encode())]]));
    }

    [TestMethod]
    public void Format_ContextSpecificValue_DumpsTheDerInHex()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        writer.WriteOctetString([0x41], new Asn1Tag(TagClass.ContextSpecific, 0));

        Assert.AreEqual("CN=#800141", Format([[EncodedAttribute(CommonName, writer.Encode())]]));
    }

    [TestMethod]
    public void Format_ConstructedValue_DumpsTheDerInHex()
    {
        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            writer.WriteNull();
        }

        Assert.AreEqual("CN=#30020500", Format([[EncodedAttribute(CommonName, writer.Encode())]]));
    }

    [TestMethod]
    public void Format_NotAName_CannotBePrinted()
    {
        Assert.IsNull(OpenSslDistinguishedNameText.Format(new X500DistinguishedName([0x04, 0x01, 0x00])));
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
