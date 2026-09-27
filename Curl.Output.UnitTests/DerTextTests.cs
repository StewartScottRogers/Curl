namespace Curl.Output;

/// <summary>
/// Pins that <see cref="DerText" /> prints each ASN.1 value as curl 8.21.0's
/// <c>ASN1tostr</c> and <c>encodeDN</c> in <c>lib/vtls/x509asn1.c</c> do. The values
/// outside the measured loopback chain follow that source.
/// </summary>
[TestClass]
public sealed class DerTextTests
{
    [TestMethod]
    [DataRow(new byte[] { 0x01, 0x01, 0xFF }, "TRUE")]
    [DataRow(new byte[] { 0x01, 0x01, 0x00 }, "FALSE")]
    [DataRow(new byte[] { 0x02, 0x01, 0x07 }, "7")]
    [DataRow(new byte[] { 0x02, 0x01, 0xFF }, "-1")]
    [DataRow(new byte[] { 0x02, 0x02, 0x27, 0x0F }, "9999")]
    [DataRow(new byte[] { 0x02, 0x02, 0x27, 0x10 }, "0x2710")]
    [DataRow(new byte[] { 0x02, 0x02, 0xD8, 0xF1 }, "-9999")]
    [DataRow(new byte[] { 0x02, 0x02, 0xD8, 0xF0 }, "0xffffd8f0")]
    [DataRow(new byte[] { 0x02, 0x05, 0x01, 0x02, 0x03, 0x04, 0x05 }, "01:02:03:04:05:")]
    [DataRow(new byte[] { 0x0A, 0x01, 0x03 }, "3")]
    [DataRow(new byte[] { 0x03, 0x03, 0x00, 0xAB, 0x01 }, "ab:01:")]
    [DataRow(new byte[] { 0x04, 0x02, 0x01, 0xFF }, "01:ff:")]
    [DataRow(new byte[] { 0x05, 0x00 }, "\0")]
    [DataRow(new byte[] { 0x0C, 0x02, 0xC3, 0xA9 }, "é")]
    [DataRow(new byte[] { 0x12, 0x02, 0x34, 0x32 }, "42")]
    [DataRow(new byte[] { 0x13, 0x02, 0x44, 0x72 }, "Dr")]
    [DataRow(new byte[] { 0x14, 0x01, 0xE9 }, "é")]
    [DataRow(new byte[] { 0x16, 0x01, 0x40 }, "@")]
    [DataRow(new byte[] { 0x1A, 0x01, 0x76 }, "v")]
    [DataRow(new byte[] { 0x1C, 0x04, 0x00, 0x01, 0xF6, 0x00 }, "\U0001F600")]
    [DataRow(new byte[] { 0x1E, 0x02, 0x00, 0xE9 }, "é")]
    [DataRow(new byte[] { 0x1E, 0x02, 0xD8, 0x00 }, "�")]
    [DataRow(new byte[] { 0x1C, 0x04, 0x00, 0x11, 0x00, 0x00 }, "�")]
    public void Format_PrimitiveValue_PrintsAsCurl(byte[] der, string expected)
    {
        Assert.AreEqual(expected, Format(der));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x30, 0x00 }, DisplayName = "constructed")]
    [DataRow(new byte[] { 0x07, 0x00 }, DisplayName = "type curl does not print")]
    [DataRow(new byte[] { 0x01, 0x01, 0x01 }, DisplayName = "boolean neither 00 nor FF")]
    [DataRow(new byte[] { 0x01, 0x02, 0x00, 0x00 }, DisplayName = "boolean of two bytes")]
    [DataRow(new byte[] { 0x02, 0x00 }, DisplayName = "empty integer")]
    [DataRow(new byte[] { 0x03, 0x00 }, DisplayName = "empty bit string")]
    [DataRow(new byte[] { 0x03, 0x02, 0x08, 0x00 }, DisplayName = "eight unused bits")]
    [DataRow(new byte[] { 0x1E, 0x01, 0x00 }, DisplayName = "odd BMPString")]
    [DataRow(new byte[] { 0x1C, 0x04, 0x00, 0x20, 0x00, 0x00 }, DisplayName = "code point past 0x1FFFFF")]
    public void Format_ValueCurlCannotPrint_ThrowsFormatException(byte[] der)
    {
        Assert.ThrowsExactly<FormatException>(() => Format(der));
    }

    [TestMethod]
    [DataRow(new byte[] { }, "")]
    [DataRow(new byte[] { 0x55, 0x04, 0x03 }, "CN")]
    [DataRow(new byte[] { 0x2A, 0x03 }, "1.2.3")]
    [DataRow(new byte[] { 0x51 }, "2.1")]
    [DataRow(new byte[] { 0x88, 0x37 }, "2.999")]
    [DataRow(new byte[] { 0x2A, 0x86, 0x48, 0x86, 0xF7, 0x0D, 0x01, 0x01, 0x0A }, "RSASSA-PSS")]
    public void FormatObjectIdentifier_ValidContent_PrintsNameOrDottedForm(byte[] content, string expected)
    {
        Assert.AreEqual(expected, DerText.FormatObjectIdentifier(content));
    }

    [TestMethod]
    [DataRow(new byte[] { 0x80, 0x01 }, DisplayName = "second arc padded")]
    [DataRow(new byte[] { 0x88 }, DisplayName = "second arc cut short")]
    [DataRow(new byte[] { 0x8F, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, DisplayName = "second arc past 32 bits")]
    [DataRow(new byte[] { 0x2A, 0x80, 0x01 }, DisplayName = "later arc padded")]
    [DataRow(new byte[] { 0x2A, 0x81 }, DisplayName = "later arc cut short")]
    [DataRow(new byte[] { 0x2A, 0x8F, 0xFF, 0xFF, 0xFF, 0xFF, 0x7F }, DisplayName = "later arc past 32 bits")]
    public void FormatObjectIdentifier_ContentCurlRefuses_ThrowsFormatException(byte[] content)
    {
        Assert.ThrowsExactly<FormatException>(() => DerText.FormatObjectIdentifier(content));
    }

    [TestMethod]
    [DataRow("260927051908Z", "2026-09-27 05:19:08 GMT")]
    [DataRow("5009270519Z", "1950-09-27 05:19:00 GMT")]
    [DataRow("2609270519+0100", "2026-09-27 05:19:00 +0100")]
    public void Format_UtcTime_PrintsAsCurl(string time, string expected)
    {
        Assert.AreEqual(expected, Format(Der.Element(0x17, Der.Ascii(time))));
    }

    [TestMethod]
    [DataRow("26092705190Z", DisplayName = "eleven digits")]
    [DataRow("260927051908", DisplayName = "no zone")]
    public void Format_UtcTimeCurlRefuses_ThrowsFormatException(string time)
    {
        Assert.ThrowsExactly<FormatException>(() => Format(Der.Element(0x17, Der.Ascii(time))));
    }

    [TestMethod]
    [DataRow("21260903051907Z", "2126-09-03 05:19:07 GMT")]
    [DataRow("212609030519Z", "2126-09-03 05:19:00 GMT")]
    [DataRow("2126090305191Z", "2126-09-03 05:19:01 GMT")]
    [DataRow("21260903051907.500Z", "2126-09-03 05:19:07.5 GMT")]
    [DataRow("21260903051907,000", "2126-09-03 05:19:07")]
    [DataRow("21260903051907", "2126-09-03 05:19:07")]
    [DataRow("21260903051907+0100", "2126-09-03 05:19:07 UTC+0100")]
    [DataRow("21260903051907-0500", "2126-09-03 05:19:07 UTC-0500")]
    [DataRow("21260903051907X", "2126-09-03 05:19:07 X")]
    public void Format_GeneralizedTime_PrintsAsCurl(string time, string expected)
    {
        Assert.AreEqual(expected, Format(Der.Element(0x18, Der.Ascii(time))));
    }

    [TestMethod]
    [DataRow("21260903051Z", DisplayName = "eleven digits")]
    [DataRow("21260903051907.Z", DisplayName = "fraction without digits")]
    public void Format_GeneralizedTimeCurlRefuses_ThrowsFormatException(string time)
    {
        Assert.ThrowsExactly<FormatException>(() => Format(Der.Element(0x18, Der.Ascii(time))));
    }

    [TestMethod]
    public void FormatDistinguishedName_Attributes_JoinsWithCommaOrSlashByTheNamesCapitals()
    {
        var name = Der.Sequenced(
            Der.Element(Der.Set, Der.Sequenced(Der.Oid("2.5.4.3"), Der.Utf8("a")), Der.Sequenced(Der.Oid("2.5.4.10"), Der.Utf8("b"))),
            Der.Element(Der.Set, Der.Sequenced(Der.Oid("1.2.840.113549.1.1.10"), Der.Utf8("c"))),
            Der.Element(Der.Set, Der.Sequenced(Der.Oid("1.2.840.113549.1.9.1"), Der.Utf8("d"))),
            Der.Element(Der.Set, Der.Sequenced(Der.Oid("0.9.2342.19200300.100.1.1"), Der.Utf8("e"))));

        Assert.AreEqual("CN=a, O=b/RSASSA-PSS=c, emailAddress=d, 0.9.2342.19200300.100.1.1=e", FormatName(name));
    }

    [TestMethod]
    public void FormatDistinguishedName_Empty_PrintsNothing()
    {
        Assert.AreEqual(string.Empty, FormatName(Der.Sequenced()));
    }

    [TestMethod]
    public void FormatDistinguishedName_AttributeTypeWithEmptyName_ThrowsFormatException()
    {
        var name = Der.Sequenced(Der.Element(Der.Set, Der.Sequenced(Der.Element(0x06), Der.Utf8("a"))));

        Assert.ThrowsExactly<FormatException>(() => FormatName(name));
    }

    private static string Format(byte[] der) => DerText.Format(der, DerReader.Read(der, 0, der.Length));

    private static string FormatName(byte[] der) => DerText.FormatDistinguishedName(der, DerReader.Read(der, 0, der.Length));
}
