using System.Buffers.Binary;
using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Networking;

/// <summary>
/// OpenSSL's subject name hash (<c>X509_NAME_hash</c>, <c>openssl x509 -hash</c>): the first four
/// bytes of the SHA-1 of the name's canonical encoding, read little-endian, as eight lowercase hex
/// digits. It names the files of a hashed CA directory (<c>--capath</c>, <c>SSL_CERT_DIR</c>) that
/// the OpenSSL build reads (ADR-0836, CGR-111).
/// </summary>
internal static class OpenSslSubjectNameHash
{
    // ASN1_MASK_CANON: the string types OpenSSL turns into a canonical UTF8String.
    private static readonly Dictionary<int, Encoding> CanonicalStringEncodings = new()
    {
        [(int)UniversalTagNumber.UTF8String] = Encoding.UTF8,
        [(int)UniversalTagNumber.PrintableString] = Encoding.Latin1,
        [(int)UniversalTagNumber.T61String] = Encoding.Latin1,
        [(int)UniversalTagNumber.IA5String] = Encoding.Latin1,
        [(int)UniversalTagNumber.VisibleString] = Encoding.Latin1,
        [(int)UniversalTagNumber.UniversalString] = new UTF32Encoding(bigEndian: true, byteOrderMark: false),
        [(int)UniversalTagNumber.BMPString] = Encoding.BigEndianUnicode,
    };

    /// <summary>Computes the hash OpenSSL names a certificate file for in a hashed CA directory.</summary>
    /// <param name="name">The certificate's subject.</param>
    /// <returns>Eight lowercase hex digits.</returns>
    public static string Of(X500DistinguishedName name)
    {
        var digest = SHA1.HashData(CanonicalEncoding(name.RawData));
        return BinaryPrimitives.ReadUInt32LittleEndian(digest).ToString("x8", CultureInfo.InvariantCulture);
    }

    // x509_name_canon: each relative distinguished name's DER SET, its string values made
    // canonical, one after another without the outer SEQUENCE.
    private static byte[] CanonicalEncoding(byte[] distinguishedName)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        var names = new AsnReader(distinguishedName, AsnEncodingRules.BER).ReadSequence();
        while (names.HasData)
        {
            var attributes = names.ReadSetOf();
            using (writer.PushSetOf())
            {
                while (attributes.HasData)
                {
                    WriteCanonicalAttribute(writer, attributes.ReadSequence());
                }
            }
        }

        return writer.Encode();
    }

    private static void WriteCanonicalAttribute(AsnWriter writer, AsnReader attribute)
    {
        using (writer.PushSequence())
        {
            writer.WriteEncodedValue(attribute.ReadEncodedValue().Span);
            var value = attribute.ReadEncodedValue().Span;
            var tag = Asn1Tag.Decode(value, out _);
            if (tag.TagClass == TagClass.Universal && CanonicalStringEncodings.TryGetValue(tag.TagValue, out var encoding))
            {
                AsnDecoder.ReadEncodedValue(value, AsnEncodingRules.BER, out var contentOffset, out var contentLength, out _);
                writer.WriteCharacterString(UniversalTagNumber.UTF8String, CanonicalText(encoding.GetString(value.Slice(contentOffset, contentLength))));
            }
            else
            {
                writer.WriteEncodedValue(value);
            }
        }
    }

    // asn1_string_canon: leading and trailing spaces dropped, each run of spaces inside made
    // one space, ASCII letters made lowercase.
    private static string CanonicalText(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var word in text.Split(" \t\n\v\f\r".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
        {
            builder.Append(builder.Length == 0 ? string.Empty : " ").Append(word);
        }

        for (var index = 0; index < builder.Length; index++)
        {
            builder[index] = char.IsAsciiLetterUpper(builder[index]) ? char.ToLowerInvariant(builder[index]) : builder[index];
        }

        return builder.ToString();
    }
}
