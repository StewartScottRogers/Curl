// Ported from curl 8.21.0 lib/vtls/x509asn1.c, Copyright (C) Daniel Stenberg,
// <daniel@haxx.se>, et al., under the curl licence (SPDX-License-Identifier: curl).

using System.Globalization;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Prints one primitive ASN.1 element as curl 8.21.0's <c>ASN1tostr</c> in
/// <c>lib/vtls/x509asn1.c</c> does, and a distinguished name as its <c>encodeDN</c> does,
/// for <see cref="PeerCertificateText" /> (ADR-0047).
/// </summary>
/// <remarks>
/// A value curl cannot print throws <see cref="FormatException" />, which makes curl give
/// up on the whole certificate. Where curl's C string would carry a character a .NET string
/// cannot (a lone UTF-16 surrogate, a code point past U+10FFFF, or an invalid UTF-8
/// sequence in a <c>UTF8String</c>), U+FFFD takes its place.
/// </remarks>
internal static class DerText
{
    private const int BooleanTag = 1;
    private const int IntegerTag = 2;
    private const int BitStringTag = 3;
    private const int OctetStringTag = 4;
    private const int NullTag = 5;
    private const int ObjectIdentifierTag = 6;
    private const int EnumeratedTag = 10;
    private const int Utf8StringTag = 12;
    private const int NumericStringTag = 18;
    private const int PrintableStringTag = 19;
    private const int TeletexStringTag = 20;
    private const int Ia5StringTag = 22;
    private const int UtcTimeTag = 23;
    private const int GeneralizedTimeTag = 24;
    private const int VisibleStringTag = 26;
    private const int UniversalStringTag = 28;
    private const int BmpStringTag = 30;

    private static readonly Dictionary<int, Func<byte[], string>> Printers = new()
    {
        [BooleanTag] = FormatBoolean,
        [IntegerTag] = FormatInteger,
        [EnumeratedTag] = FormatInteger,
        [BitStringTag] = FormatBits,
        [OctetStringTag] = FormatOctets,
        [NullTag] = _ => "\0",
        [ObjectIdentifierTag] = FormatObjectIdentifier,
        [UtcTimeTag] = FormatUtcTime,
        [GeneralizedTimeTag] = FormatGeneralizedTime,
        [Utf8StringTag] = Encoding.UTF8.GetString,
        [NumericStringTag] = content => DecodeCharacters(content, 1),
        [PrintableStringTag] = content => DecodeCharacters(content, 1),
        [TeletexStringTag] = content => DecodeCharacters(content, 1),
        [Ia5StringTag] = content => DecodeCharacters(content, 1),
        [VisibleStringTag] = content => DecodeCharacters(content, 1),
        [UniversalStringTag] = content => DecodeCharacters(content, 4),
        [BmpStringTag] = content => DecodeCharacters(content, 2),
    };

    /// <summary>
    /// The object identifier names curl 8.21.0 knows, from <c>OIDtable</c>; any other
    /// prints in dotted form.
    /// </summary>
    private static readonly Dictionary<string, string> ObjectIdentifierNames = new(StringComparer.Ordinal)
    {
        ["1.2.840.10040.4.1"] = "dsa",
        ["1.2.840.10040.4.3"] = "dsa-with-sha1",
        ["1.2.840.10045.2.1"] = "ecPublicKey",
        ["1.2.840.10045.3.0.1"] = "c2pnb163v1",
        ["1.2.840.10045.4.1"] = "ecdsa-with-SHA1",
        ["1.2.840.10045.4.3.1"] = "ecdsa-with-SHA224",
        ["1.2.840.10045.4.3.2"] = "ecdsa-with-SHA256",
        ["1.2.840.10045.4.3.3"] = "ecdsa-with-SHA384",
        ["1.2.840.10045.4.3.4"] = "ecdsa-with-SHA512",
        ["1.2.840.10046.2.1"] = "dhpublicnumber",
        ["1.2.840.113549.1.1.1"] = "rsaEncryption",
        ["1.2.840.113549.1.1.2"] = "md2WithRSAEncryption",
        ["1.2.840.113549.1.1.4"] = "md5WithRSAEncryption",
        ["1.2.840.113549.1.1.5"] = "sha1WithRSAEncryption",
        ["1.2.840.113549.1.1.10"] = "RSASSA-PSS",
        ["1.2.840.113549.1.1.14"] = "sha224WithRSAEncryption",
        ["1.2.840.113549.1.1.11"] = "sha256WithRSAEncryption",
        ["1.2.840.113549.1.1.12"] = "sha384WithRSAEncryption",
        ["1.2.840.113549.1.1.13"] = "sha512WithRSAEncryption",
        ["1.2.840.113549.2.2"] = "md2",
        ["1.2.840.113549.2.5"] = "md5",
        ["1.3.14.3.2.26"] = "sha1",
        ["2.5.4.3"] = "CN",
        ["2.5.4.4"] = "SN",
        ["2.5.4.5"] = "serialNumber",
        ["2.5.4.6"] = "C",
        ["2.5.4.7"] = "L",
        ["2.5.4.8"] = "ST",
        ["2.5.4.9"] = "streetAddress",
        ["2.5.4.10"] = "O",
        ["2.5.4.11"] = "OU",
        ["2.5.4.12"] = "title",
        ["2.5.4.13"] = "description",
        ["2.5.4.17"] = "postalCode",
        ["2.5.4.41"] = "name",
        ["2.5.4.42"] = "givenName",
        ["2.5.4.43"] = "initials",
        ["2.5.4.44"] = "generationQualifier",
        ["2.5.4.45"] = "X500UniqueIdentifier",
        ["2.5.4.46"] = "dnQualifier",
        ["2.5.4.65"] = "pseudonym",
        ["1.2.840.113549.1.9.1"] = "emailAddress",
        ["2.5.4.72"] = "role",
        ["2.5.29.17"] = "subjectAltName",
        ["2.5.29.18"] = "issuerAltName",
        ["2.5.29.19"] = "basicConstraints",
        ["2.16.840.1.101.3.4.2.4"] = "sha224",
        ["2.16.840.1.101.3.4.2.1"] = "sha256",
        ["2.16.840.1.101.3.4.2.2"] = "sha384",
        ["2.16.840.1.101.3.4.2.3"] = "sha512",
        ["1.2.840.113549.1.9.2"] = "unstructuredName",
    };

    /// <summary>
    /// Prints a primitive element by its tag number: a boolean, integer, bit or octet
    /// string, null (one NUL character), object identifier, time, or character string.
    /// </summary>
    /// <param name="der">The buffer.</param>
    /// <param name="element">The element.</param>
    /// <returns>The text.</returns>
    /// <exception cref="FormatException">The element is constructed, of another type, or malformed.</exception>
    internal static string Format(byte[] der, DerElement element)
    {
        if (element.IsConstructed || !Printers.TryGetValue(element.Tag, out var print))
        {
            throw Unprintable();
        }

        return print(der.AsSpan(element.Start, element.Length).ToArray());
    }

    /// <summary>
    /// Prints a distinguished name as <c>encodeDN</c> does: every attribute in the order
    /// encoded, as <c>name=value</c>, joined by <c>/</c> before a name that begins with more
    /// than two capital letters and <c>, </c> before any other.
    /// </summary>
    /// <param name="der">The buffer.</param>
    /// <param name="name">The <c>Name</c> element.</param>
    /// <returns>The text.</returns>
    /// <exception cref="FormatException">An attribute is malformed.</exception>
    internal static string FormatDistinguishedName(byte[] der, DerElement name)
    {
        var text = new StringBuilder();
        var added = false;
        for (var position = name.Start; position < name.End;)
        {
            var relativeName = DerReader.Read(der, position, name.End);
            for (var inner = relativeName.Start; inner < relativeName.End;)
            {
                var attribute = DerReader.Read(der, inner, relativeName.End);
                AppendAttribute(text, der, attribute, added);
                added = true;
                inner = attribute.Next;
            }

            position = relativeName.Next;
        }

        return text.ToString();
    }

    /// <summary>
    /// Prints object identifier content as <c>OID2str</c> does: curl's name for it, or its
    /// dotted form; nothing for empty content.
    /// </summary>
    /// <param name="content">The content bytes.</param>
    /// <returns>The text.</returns>
    /// <exception cref="FormatException">The content is not a valid encoding.</exception>
    internal static string FormatObjectIdentifier(byte[] content)
    {
        if (content.Length == 0)
        {
            return string.Empty;
        }

        var dotted = EncodeObjectIdentifier(content);
        return ObjectIdentifierNames.GetValueOrDefault(dotted, dotted);
    }

    private static void AppendAttribute(StringBuilder text, byte[] der, DerElement attribute, bool added)
    {
        var type = DerReader.Read(der, attribute.Start, attribute.End);
        var value = DerReader.Read(der, type.Next, attribute.End);
        var typeName = Format(der, type);
        if (typeName.Length == 0)
        {
            throw Unprintable();
        }

        if (added)
        {
            text.Append(CountLeadingCapitals(typeName) > 2 ? "/" : ", ");
        }

        text.Append(typeName).Append('=').Append(Format(der, value));
    }

    private static int CountLeadingCapitals(string text)
    {
        var count = 0;
        while (count < text.Length && char.IsAsciiLetterUpper(text[count]))
        {
            count++;
        }

        return count;
    }

    private static string FormatBoolean(byte[] content)
    {
        if (content.Length != 1 || content[0] is not (0x00 or 0xFF))
        {
            throw Unprintable();
        }

        return content[0] == 0 ? "FALSE" : "TRUE";
    }

    // Up to four bytes, a signed number: decimal when its magnitude is under 10000, else
    // 0x and the 32-bit two's complement in lower-case hex. Longer, the bytes.
    private static string FormatInteger(byte[] content)
    {
        if (content.Length == 0)
        {
            throw Unprintable();
        }

        if (content.Length > 4)
        {
            return FormatOctets(content);
        }

        var unsignedValue = (content[0] & 0x80) != 0 ? uint.MaxValue : 0;
        foreach (var octet in content)
        {
            unsignedValue = (unsignedValue << 8) | octet;
        }

        return FormatSmallInteger(unsignedValue);
    }

    private static string FormatSmallInteger(uint unsignedValue)
    {
        var signedValue = unchecked((int)unsignedValue);
        return signedValue is > -10000 and < 10000
            ? signedValue.ToString(CultureInfo.InvariantCulture)
            : "0x" + unsignedValue.ToString("x", CultureInfo.InvariantCulture);
    }

    // The first byte counts the unused bits, which must be under eight, and is not printed.
    private static string FormatBits(byte[] content)
    {
        if (content.Length == 0 || content[0] > 7)
        {
            throw Unprintable();
        }

        return FormatOctets(content[1..]);
    }

    private static string FormatOctets(byte[] content)
    {
        var text = new StringBuilder(content.Length * 3);
        foreach (var octet in content)
        {
            text.Append(octet.ToString("x2", CultureInfo.InvariantCulture)).Append(':');
        }

        return text.ToString();
    }

    // Each character is 'width' big-endian bytes holding one code point.
    private static string DecodeCharacters(byte[] content, int width)
    {
        if (content.Length % width != 0)
        {
            throw Unprintable();
        }

        var text = new StringBuilder(content.Length / width);
        for (var index = 0; index < content.Length; index += width)
        {
            var codePoint = 0;
            foreach (var octet in content.AsSpan(index, width))
            {
                codePoint = (codePoint << 8) | octet;
            }

            text.Append(ToCharacters(codePoint));
        }

        return text.ToString();
    }

    // curl encodes up to 0x1FFFFF as UTF-8 and refuses the certificate beyond that.
    private static string ToCharacters(int codePoint)
    {
        if (codePoint >= 0x200000)
        {
            throw Unprintable();
        }

        return Rune.TryCreate(codePoint, out var rune) ? rune.ToString() : "�";
    }

    private static string EncodeObjectIdentifier(byte[] content)
    {
        var text = new StringBuilder();
        var position = 0;
        uint first = content[position++];
        var root = first <= 80 ? first / 40 : 2;
        text.Append(root.ToString(CultureInfo.InvariantCulture));
        var second = (first & 0x80) == 0 ? first : ReadWideSecondArc(content, ref position, first & 0x7F);
        text.Append('.').Append((second - (root * 40)).ToString(CultureInfo.InvariantCulture));
        while (position < content.Length)
        {
            text.Append('.').Append(ReadArc(content, ref position).ToString(CultureInfo.InvariantCulture));
        }

        return text.ToString();
    }

    // The first byte's low seven bits start the second arc, which may run on; curl refuses
    // a leading 0x80 and an arc that would pass 32 bits.
    private static uint ReadWideSecondArc(byte[] content, ref int position, uint arc)
    {
        if (arc == 0)
        {
            throw Unprintable();
        }

        uint octet;
        do
        {
            if ((arc & 0xFE000000) != 0 || position >= content.Length)
            {
                throw Unprintable();
            }

            octet = content[position++];
            arc = (arc << 7) | (octet & 0x7F);
        }
        while ((octet & 0x80) != 0);

        return arc;
    }

    private static uint ReadArc(byte[] content, ref int position)
    {
        uint arc = 0;
        uint octet = 0;
        do
        {
            if (((octet & 0x80) != 0 && arc == 0) || (arc & 0xFE000000) != 0 || position == content.Length)
            {
                throw Unprintable();
            }

            octet = content[position++];
            arc = (arc << 7) | (octet & 0x7F);
        }
        while ((octet & 0x80) != 0);

        return arc;
    }

    // YYMMDDhhmm[ss] then Z (GMT) or the rest as written; 50 to 99 are 19xx.
    private static string FormatUtcTime(byte[] content)
    {
        var digits = CountDigits(content, 0);
        if (!IsUtcTimeLayout(digits, content.Length))
        {
            throw Unprintable();
        }

        var seconds = digits == 12 ? Ascii(content, 10, 2) : "00";
        var zone = content[digits] == 'Z' ? "GMT" : Ascii(content, digits, content.Length - digits);
        var century = content[0] >= '5' ? "19" : "20";
        return $"{century}{Ascii(content, 0, 2)}-{Ascii(content, 2, 2)}-{Ascii(content, 4, 2)} {Ascii(content, 6, 2)}:{Ascii(content, 8, 2)}:{seconds} {zone}";
    }

    // Ten or twelve digits, then at least one byte of zone.
    private static bool IsUtcTimeLayout(int digits, int length) => digits is 10 or 12 && digits < length;

    // YYYYMMDDhh[mm[ss]][.fraction] then nothing, Z (GMT), an offset after "UTC", or the rest.
    private static string FormatGeneralizedTime(byte[] content)
    {
        var digits = CountDigits(content, 0);
        var seconds = FormatGeneralizedTimeSeconds(content, digits);
        var (fraction, zoneStart) = ReadGeneralizedTimeFraction(content, digits);
        return $"{Ascii(content, 0, 4)}-{Ascii(content, 4, 2)}-{Ascii(content, 6, 2)} {Ascii(content, 8, 2)}:{Ascii(content, 10, 2)}:{seconds}{fraction}{FormatGeneralizedTimeZone(content, zoneStart)}";
    }

    // Twelve digits have no seconds, thirteen one digit of them, fourteen two.
    private static string FormatGeneralizedTimeSeconds(byte[] content, int digits) => (digits - 12) switch
    {
        0 => "00",
        1 => "0" + Ascii(content, digits - 1, 1),
        2 => Ascii(content, digits - 2, 2),
        _ => throw Unprintable(),
    };

    // A '.' or ',' must be followed by digits; they are printed after a '.' without their
    // trailing zeros, and not at all when only zeros.
    private static (string Fraction, int ZoneStart) ReadGeneralizedTimeFraction(byte[] content, int start)
    {
        if (!StartsFraction(content, start))
        {
            return (string.Empty, start);
        }

        var fractionDigits = CountDigits(content, start + 1);
        if (fractionDigits == 0)
        {
            throw Unprintable();
        }

        return (FormatFraction(Ascii(content, start + 1, fractionDigits).TrimEnd('0')), start + 1 + fractionDigits);
    }

    private static bool StartsFraction(byte[] content, int position) =>
        position < content.Length && (content[position] == '.' || content[position] == ',');

    private static string FormatFraction(string significantDigits) =>
        significantDigits.Length == 0 ? string.Empty : "." + significantDigits;

    private static string FormatGeneralizedTimeZone(byte[] content, int start)
    {
        if (start >= content.Length)
        {
            return string.Empty;
        }

        var rest = Ascii(content, start, content.Length - start);
        return content[start] switch
        {
            (byte)'Z' => " GMT",
            (byte)'+' or (byte)'-' => " UTC" + rest,
            _ => " " + rest,
        };
    }

    private static int CountDigits(byte[] content, int start)
    {
        var end = start;
        while (end < content.Length && char.IsAsciiDigit((char)content[end]))
        {
            end++;
        }

        return end - start;
    }

    private static string Ascii(byte[] content, int start, int length) =>
        Encoding.Latin1.GetString(content, start, length);

    private static FormatException Unprintable() =>
        new("The certificate holds a value curl cannot print.");
}
