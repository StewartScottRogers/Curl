using System.Formats.Asn1;
using System.Numerics;
using Curl.Protocol.Ldap.Fakes;
using Curl.Testing;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Pins <see cref="LdapBerWriter" />: shortest primitives on both dialects, WinLDAP's
/// five-byte constructed lengths, libldap's shortest ones, and that <see cref="AsnReader" />
/// reads back each element type the LDAP requests use.
/// </summary>
[TestClass]
public sealed class LdapBerWriterTests
{
    public TestContext TestContext { get; set; } = null!;

    private TestDiagnostics Diagnostics => TestDiagnostics.For(TestContext);

    [TestMethod]
    [DataRow(0, "02 01 00")]
    [DataRow(1, "02 01 01")]
    [DataRow(127, "02 01 7f")]
    [DataRow(128, "02 02 00 80")]
    [DataRow(-1, "02 01 ff")]
    public void Integer_WritesTheShortestTwosComplement(int value, string expected)
    {
        Diagnostics.Arrange("value", value);

        byte[] actual = LdapBerWriter.Integer(value);

        Diagnostics.Bytes("integer", actual);
        Diagnostics.Act("encoded", Convert.ToHexString(actual));
        Diagnostics.Diff("integer", Hex.Bytes(expected), actual);
        CollectionAssert.AreEqual(Hex.Bytes(expected), actual);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(3)]
    [DataRow(300)]
    [DataRow(-129)]
    [DataRow(int.MaxValue)]
    public void Integer_RoundTripsThroughAsnReader(int value)
    {
        Diagnostics.Arrange("value", value);
        byte[] encoded = LdapBerWriter.Integer(value);
        Diagnostics.Bytes("integer", encoded);
        var reader = new AsnReader(encoded, AsnEncodingRules.BER);

        BigInteger decoded = reader.ReadInteger();

        Diagnostics.Act("decoded", decoded);
        Diagnostics.Assert("decoded", new BigInteger(value), decoded);
        Assert.AreEqual(new BigInteger(value), decoded);
        Assert.IsFalse(reader.HasData);
    }

    [TestMethod]
    public void OctetString_UniversalTag_RoundTripsThroughAsnReader()
    {
        byte[] content = [.. "cn=u,dc=x"u8];
        Diagnostics.Arrange("content", "cn=u,dc=x");

        byte[] element = LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, content);

        Diagnostics.Bytes("element", element);
        Diagnostics.Act("encoded", Convert.ToHexString(element));
        Diagnostics.Diff("element", Hex.Bytes("04 09 63 6e 3d 75 2c 64 63 3d 78"), element);
        CollectionAssert.AreEqual(Hex.Bytes("04 09 63 6e 3d 75 2c 64 63 3d 78"), element);
        CollectionAssert.AreEqual(content, new AsnReader(element, AsnEncodingRules.BER).ReadOctetString());
    }

    [TestMethod]
    public void OctetString_ContextTagZero_WritesSimpleAuthenticationAndRoundTrips()
    {
        var tag = new Asn1Tag(TagClass.ContextSpecific, 0);
        Diagnostics.Arrange("tag", tag);

        byte[] element = LdapBerWriter.OctetString(tag, "secret"u8);

        Diagnostics.Bytes("element", element);
        Diagnostics.Act("encoded", Convert.ToHexString(element));
        Diagnostics.Diff("element", Hex.Bytes("80 06 73 65 63 72 65 74"), element);
        CollectionAssert.AreEqual(Hex.Bytes("80 06 73 65 63 72 65 74"), element);
        CollectionAssert.AreEqual("secret"u8.ToArray(), new AsnReader(element, AsnEncodingRules.BER).ReadOctetString(tag));
    }

    [TestMethod]
    public void OctetString_LongContent_WritesTheShortestLongFormLength()
    {
        Diagnostics.Arrange("content length", 200);

        byte[] element = LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, new byte[200]);

        Diagnostics.Bytes("element", element);
        Diagnostics.Act("header", Convert.ToHexString(element[..3]));
        Diagnostics.Diff("header", Hex.Bytes("04 81 c8"), element[..3]);
        CollectionAssert.AreEqual(Hex.Bytes("04 81 c8"), element[..3]);
    }

    [TestMethod]
    public void Null_ApplicationTagTwo_WritesTheUnbindRequestAndRoundTrips()
    {
        var tag = new Asn1Tag(TagClass.Application, 2);
        Diagnostics.Arrange("tag", tag);

        byte[] element = LdapBerWriter.Null(tag);

        Diagnostics.Bytes("element", element);
        Diagnostics.Act("encoded", Convert.ToHexString(element));
        Diagnostics.Diff("element", Hex.Bytes("42 00"), element);
        CollectionAssert.AreEqual(Hex.Bytes("42 00"), element);
        var reader = new AsnReader(element, AsnEncodingRules.BER);
        reader.ReadNull(tag);
        Assert.IsFalse(reader.HasData);
    }

    [TestMethod]
    public void Constructed_WinLdap_WritesAFiveByteLength()
    {
        Diagnostics.Arrange("dialect", LdapDialect.WinLdap);

        byte[] element = new LdapBerWriter(LdapDialect.WinLdap).Constructed(Asn1Tag.Sequence, LdapBerWriter.Integer(1));

        Diagnostics.Bytes("element", element);
        Diagnostics.Act("encoded", Convert.ToHexString(element));
        Diagnostics.Diff("element", Hex.Bytes("30 84 00 00 00 03 02 01 01"), element);
        CollectionAssert.AreEqual(Hex.Bytes("30 84 00 00 00 03 02 01 01"), element);
    }

    [TestMethod]
    public void Constructed_OpenLdap_WritesTheShortestLength()
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);

        byte[] element = new LdapBerWriter(LdapDialect.OpenLdap).Constructed(Asn1Tag.Sequence, LdapBerWriter.Integer(1));

        Diagnostics.Bytes("element", element);
        Diagnostics.Act("encoded", Convert.ToHexString(element));
        Diagnostics.Diff("element", Hex.Bytes("30 03 02 01 01"), element);
        CollectionAssert.AreEqual(Hex.Bytes("30 03 02 01 01"), element);
    }

    [TestMethod]
    public void Constructed_ApplicationTag_IsWrittenConstructed()
    {
        Diagnostics.Arrange("dialect", LdapDialect.OpenLdap);

        byte[] element = new LdapBerWriter(LdapDialect.OpenLdap).Constructed(new Asn1Tag(TagClass.Application, 0));

        Diagnostics.Bytes("element", element);
        Diagnostics.Act("encoded", Convert.ToHexString(element));
        Diagnostics.Diff("element", Hex.Bytes("60 00"), element);
        CollectionAssert.AreEqual(Hex.Bytes("60 00"), element);
    }

    [TestMethod]
    [DataRow(LdapDialect.WinLdap)]
    [DataRow(LdapDialect.OpenLdap)]
    public void Constructed_RoundTripsThroughAsnReader(LdapDialect dialect)
    {
        Diagnostics.Arrange("dialect", dialect);
        var writer = new LdapBerWriter(dialect);
        byte[] element = writer.Constructed(
            Asn1Tag.Sequence,
            LdapBerWriter.Integer(7),
            writer.Constructed(new Asn1Tag(TagClass.Application, 0), LdapBerWriter.OctetString(Asn1Tag.PrimitiveOctetString, new byte[300])));
        Diagnostics.Bytes("element", element);

        var reader = new AsnReader(element, AsnEncodingRules.BER);
        AsnReader sequence = reader.ReadSequence();
        Assert.IsFalse(reader.HasData);
        BigInteger id = sequence.ReadInteger();
        AsnReader inner = sequence.ReadSequence(new Asn1Tag(TagClass.Application, 0));
        byte[] octets = inner.ReadOctetString();

        Diagnostics.Act("message id", id);
        Diagnostics.Act("octet count", octets.Length);
        Diagnostics.Assert("message id", new BigInteger(7), id);
        Diagnostics.Assert("octet count", 300, octets.Length);
        Assert.AreEqual(new BigInteger(7), id);
        Assert.HasCount(300, octets);
        Assert.IsFalse(sequence.HasData);
    }

    [TestMethod]
    [DataRow(0, "00")]
    [DataRow(127, "7f")]
    [DataRow(128, "81 80")]
    [DataRow(255, "81 ff")]
    [DataRow(256, "82 01 00")]
    [DataRow(0x10000, "83 01 00 00")]
    [DataRow(0x1000000, "84 01 00 00 00")]
    public void ShortestLengthOf_WritesTheFewestOctets(int length, string expected)
    {
        Diagnostics.Arrange("length", length);

        byte[] actual = LdapBerWriter.ShortestLengthOf(length);

        Diagnostics.Bytes("length octets", actual);
        Diagnostics.Act("encoded", Convert.ToHexString(actual));
        Diagnostics.Diff("length octets", Hex.Bytes(expected), actual);
        CollectionAssert.AreEqual(Hex.Bytes(expected), actual);
    }

    [TestMethod]
    [DataRow(0, "0a 01 00")]
    [DataRow(3, "0a 01 03")]
    [DataRow(128, "0a 02 00 80")]
    public void Enumerated_WritesAnIntegersContentUnderTagTen(int value, string expected)
    {
        Diagnostics.Arrange("value", value);

        byte[] actual = LdapBerWriter.Enumerated(value);

        Diagnostics.Bytes("enumerated", actual);
        Diagnostics.Act("encoded", Convert.ToHexString(actual));
        Diagnostics.Diff("enumerated", Hex.Bytes(expected), actual);
        CollectionAssert.AreEqual(Hex.Bytes(expected), actual);
    }

    [TestMethod]
    [DataRow(false, "01 01 00")]
    [DataRow(true, "01 01 ff")]
    public void Boolean_WritesZeroOrFf(bool value, string expected)
    {
        Diagnostics.Arrange("value", value);

        byte[] actual = LdapBerWriter.Boolean(value);

        Diagnostics.Bytes("boolean", actual);
        Diagnostics.Act("encoded", Convert.ToHexString(actual));
        Diagnostics.Diff("boolean", Hex.Bytes(expected), actual);
        CollectionAssert.AreEqual(Hex.Bytes(expected), actual);
    }

    [TestMethod]
    public void Integer_ApplicationTagSixteen_WritesAnAbandonRequestsContent()
    {
        Diagnostics.Arrange("value", 2);

        byte[] actual = LdapBerWriter.Integer(2, new Asn1Tag(TagClass.Application, 16));

        Diagnostics.Bytes("integer", actual);
        Diagnostics.Act("encoded", Convert.ToHexString(actual));
        Diagnostics.Diff("integer", Hex.Bytes("50 01 02"), actual);
        CollectionAssert.AreEqual(Hex.Bytes("50 01 02"), actual);
    }

    [TestMethod]
    [DataRow(0, "84 00 00 00 00")]
    [DataRow(0x1f, "84 00 00 00 1f")]
    [DataRow(0x01020304, "84 01 02 03 04")]
    public void FourOctetLengthOf_AlwaysWritesFourLengthOctets(int length, string expected)
    {
        Diagnostics.Arrange("length", length);

        byte[] actual = LdapBerWriter.FourOctetLengthOf(length);

        Diagnostics.Bytes("length octets", actual);
        Diagnostics.Act("encoded", Convert.ToHexString(actual));
        Diagnostics.Diff("length octets", Hex.Bytes(expected), actual);
        CollectionAssert.AreEqual(Hex.Bytes(expected), actual);
    }
}
