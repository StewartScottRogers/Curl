using System.Formats.Asn1;

namespace Curl.Protocol.Ldap;

/// <summary>
/// Writes the BER elements of the LDAP requests curl sends, with the length form of the
/// reference build <see cref="LdapDialect" /> names (ADR-0166).
/// </summary>
/// <remarks>
/// Primitive elements are written by <see cref="AsnWriter" /> with the shortest length, as
/// both builds write them. Constructed elements are written here, because
/// <see cref="AsnWriter" /> cannot write WinLDAP's five-byte <c>84 00 00 00 nn</c> length.
/// </remarks>
/// <param name="dialect">The build whose length form constructed elements take.</param>
internal sealed class LdapBerWriter(LdapDialect dialect)
{
    /// <summary>The first length octet of WinLDAP's constructed lengths: four length octets follow.</summary>
    private const byte FourOctetLength = 0x84;

    /// <summary>A universal primitive ENUMERATED's identifier octet.</summary>
    private const byte EnumeratedIdentifier = 0x0A;

    /// <summary>Writes an INTEGER.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The element, identifier and length octets included.</returns>
    public static byte[] Integer(int value) => Integer(value, Asn1Tag.Integer);

    /// <summary>Writes an INTEGER with another tag, such as an AbandonRequest's.</summary>
    /// <param name="value">The value.</param>
    /// <param name="tag">The tag; it is written primitive.</param>
    /// <returns>The element, identifier and length octets included.</returns>
    public static byte[] Integer(int value, Asn1Tag tag)
    {
        AsnWriter writer = new(AsnEncodingRules.BER);
        writer.WriteInteger(value, tag);
        return writer.Encode();
    }

    /// <summary>Writes an ENUMERATED: an INTEGER's content under the universal tag 10.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The element, identifier and length octets included.</returns>
    /// <remarks><see cref="AsnWriter" /> writes an ENUMERATED only from an <see cref="Enum" />, so the INTEGER's identifier octet is replaced.</remarks>
    public static byte[] Enumerated(int value)
    {
        byte[] element = Integer(value);
        element[0] = EnumeratedIdentifier;
        return element;
    }

    /// <summary>Writes a BOOLEAN, <c>ff</c> for true as both builds write it.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The element, identifier and length octets included.</returns>
    public static byte[] Boolean(bool value)
    {
        AsnWriter writer = new(AsnEncodingRules.BER);
        writer.WriteBoolean(value);
        return writer.Encode();
    }

    /// <summary>Writes an OCTET STRING, or a primitive element of another tag with the same content rules.</summary>
    /// <param name="tag">The tag, such as <see cref="Asn1Tag.PrimitiveOctetString" />.</param>
    /// <param name="content">The content octets.</param>
    /// <returns>The element, identifier and length octets included.</returns>
    public static byte[] OctetString(Asn1Tag tag, ReadOnlySpan<byte> content)
    {
        AsnWriter writer = new(AsnEncodingRules.BER);
        writer.WriteOctetString(content, tag);
        return writer.Encode();
    }

    /// <summary>Writes a NULL, or a primitive element of another tag with no content.</summary>
    /// <param name="tag">The tag, such as <see cref="Asn1Tag.Null" />.</param>
    /// <returns>The element, identifier and length octets included.</returns>
    public static byte[] Null(Asn1Tag tag)
    {
        AsnWriter writer = new(AsnEncodingRules.BER);
        writer.WriteNull(tag);
        return writer.Encode();
    }

    /// <summary>
    /// Writes a constructed element holding <paramref name="elements" /> in order, with the
    /// dialect's length form.
    /// </summary>
    /// <param name="tag">The tag, such as <see cref="Asn1Tag.Sequence" />; it is written constructed.</param>
    /// <param name="elements">The encoded elements it holds.</param>
    /// <returns>The element, identifier and length octets included.</returns>
    public byte[] Constructed(Asn1Tag tag, params byte[][] elements)
    {
        byte[] identifier = new byte[tag.AsConstructed().CalculateEncodedSize()];
        tag.AsConstructed().Encode(identifier);
        int contentLength = elements.Sum(element => element.Length);
        byte[] length = dialect == LdapDialect.WinLdap ? FourOctetLengthOf(contentLength) : ShortestLengthOf(contentLength);
        return [.. identifier, .. length, .. elements.SelectMany(element => element)];
    }

    /// <summary>Writes a definite length in the long form with four length octets, as WinLDAP does.</summary>
    /// <param name="length">The content length.</param>
    /// <returns>The length octets.</returns>
    internal static byte[] FourOctetLengthOf(int length) =>
        [FourOctetLength, (byte)(length >> 24), (byte)(length >> 16), (byte)(length >> 8), (byte)length];

    /// <summary>Writes a definite length in its shortest form, as <c>libldap</c> does.</summary>
    /// <param name="length">The content length.</param>
    /// <returns>The length octets: one for a length below 128, else 0x80 plus the count of the octets that follow.</returns>
    internal static byte[] ShortestLengthOf(int length)
    {
        if (length < 0x80)
        {
            return [(byte)length];
        }

        byte[] octets = FourOctetLengthOf(length)[1..];
        int leadingZeros = octets.TakeWhile(octet => octet == 0).Count();
        return [(byte)(0x80 | (octets.Length - leadingZeros)), .. octets[leadingZeros..]];
    }
}
