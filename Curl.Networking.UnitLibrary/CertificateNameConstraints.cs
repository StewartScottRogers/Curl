using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Evaluates the name constraints of the CA certificates in a chain against the server
/// certificate's subject alternative names. Windows' chain engine never evaluates the
/// constraints of a non-self-signed <c>--cacert</c> anchor (it reports only a partial chain),
/// yet curl's Schannel build refuses the chain with <c>CERT_TRUST_HAS_NOT_PERMITTED_NAME_CONSTRAINT</c>
/// or <c>CERT_TRUST_HAS_EXCLUDED_NAME_CONSTRAINT</c> (BL-5375).
/// </summary>
internal static class CertificateNameConstraints
{
    /// <summary>Schannel's <c>CERT_TRUST_HAS_NOT_PERMITTED_NAME_CONSTRAINT</c>.</summary>
    internal const int NotPermitted = 0x4000;

    /// <summary>Schannel's <c>CERT_TRUST_HAS_EXCLUDED_NAME_CONSTRAINT</c>.</summary>
    internal const int Excluded = 0x8000;

    private const string NameConstraintsOid = "2.5.29.30";

    private const string CommonNameOid = "2.5.4.3";

    private const string EmailAddressOid = "1.2.840.113549.1.9.1";

    private const string AlternativeNameOid = "2.5.29.17";

    // An empty directory name is the two bytes of an empty SEQUENCE.
    private const int EmptyNameLength = 2;

    private static readonly Asn1Tag DnsNameTag = new(TagClass.ContextSpecific, 2);

    private static readonly Asn1Tag IpAddressTag = new(TagClass.ContextSpecific, 7);

    private static readonly Asn1Tag EmailTag = new(TagClass.ContextSpecific, 1);

    private static readonly Asn1Tag UriTag = new(TagClass.ContextSpecific, 6);

    private static readonly Asn1Tag DirectoryNameTag = new(TagClass.ContextSpecific, 4, true);

    // The name kinds whose constraint is a primitive string or octet string.
    private static readonly Asn1Tag[] OctetNameTags = [DnsNameTag, IpAddressTag, EmailTag, UriTag];

    /// <summary>
    /// Finds the first name constraint the leaf's names break.
    /// </summary>
    /// <param name="chain">The chain built for the server certificate, leaf first.</param>
    /// <returns><see cref="NotPermitted" />, <see cref="Excluded" />, or 0 when no constraint is broken.</returns>
    internal static int FindViolation(X509Chain chain)
    {
        var elements = chain.ChainElements;
        if (elements.Count < 2)
        {
            return 0;
        }

        try
        {
            List<(Asn1Tag Tag, byte[] Value)>? names = null;
            var end = CountThroughAnchor(chain);
            for (var index = 1; index < end; index++)
            {
                var extension = elements[index].Certificate.Extensions[NameConstraintsOid];
                if (extension is null)
                {
                    continue;
                }

                // The leaf's names are read only once some CA carries constraints (BL-5424).
                names ??= ListNames(elements[0].Certificate);
                var violation = FindViolation(extension, names);
                if (violation != 0)
                {
                    return violation;
                }
            }

            return 0;
        }
        catch (Exception exception) when (exception is AsnContentException or CryptographicException)
        {
            // A server-controlled name or constraint that does not decode is a refused chain (BL-5415).
            return NotPermitted;
        }
    }

    // A certificate the custom trust store lists is the anchor: those above it, sent by the server, do not apply (BL-5444).
    private static int CountThroughAnchor(X509Chain chain)
    {
        var elements = chain.ChainElements;
        var anchor = Enumerable.Range(1, elements.Count - 1)
            .Where(index => chain.ChainPolicy.CustomTrustStore.Any(listed => listed.RawDataMemory.Span.SequenceEqual(elements[index].Certificate.RawDataMemory.Span)))
            .Select(index => index + 1)
            .FirstOrDefault();
        return anchor == 0 ? elements.Count : anchor;
    }

    private static List<(Asn1Tag Tag, byte[] Value)> ListNames(X509Certificate2 leaf)
    {
        var names = new List<(Asn1Tag, byte[])>();
        var extensions = leaf.Extensions.OfType<X509SubjectAlternativeNameExtension>().ToList();
        var dnsNames = extensions.SelectMany(extension => extension.EnumerateDnsNames()).ToList();

        // CryptoAPI checks the subject CN as a DNS name when the leaf has no dNSName SAN (BL-5432).
        names.AddRange(DnsNamesOrCommonNames(leaf, dnsNames).Select(name => (DnsNameTag, System.Text.Encoding.ASCII.GetBytes(name))));
        names.AddRange(extensions.SelectMany(extension => extension.EnumerateIPAddresses()).Select(address => (IpAddressTag, address.GetAddressBytes())));

        // directoryName, rfc822Name and URI names are constrained too; the subject counts as a directoryName (BL-5436)
        // and its emailAddress attributes as rfc822Names (RFC 5280 §4.2.1.10, BL-5804).
        names.AddRange(ListSubjectNames(leaf));
        names.AddRange(ListOtherAlternativeNames(leaf));
        return names;
    }

    private static List<(Asn1Tag Tag, byte[] Value)> ListSubjectNames(X509Certificate2 leaf)
    {
        var names = new List<(Asn1Tag, byte[])>();
        if (leaf.SubjectName.RawData.Length > EmptyNameLength)
        {
            names.Add((DirectoryNameTag, leaf.SubjectName.RawData));
        }

        names.AddRange(ListSubjectEmailAddresses(leaf).Select(address => (EmailTag, System.Text.Encoding.ASCII.GetBytes(address))));
        return names;
    }

    private static List<string> ListSubjectEmailAddresses(X509Certificate2 leaf) =>
        leaf.SubjectName.EnumerateRelativeDistinguishedNames()
            .Where(rdn => !rdn.HasMultipleElements && rdn.GetSingleElementType().Value == EmailAddressOid)
            .Select(rdn => rdn.GetSingleElementValue())
            .OfType<string>()
            .ToList();

    private static List<(Asn1Tag Tag, byte[] Value)> ListOtherAlternativeNames(X509Certificate2 leaf)
    {
        var names = new List<(Asn1Tag, byte[])>();
        var extension = leaf.Extensions[AlternativeNameOid];
        if (extension is null)
        {
            return names;
        }

        var reader = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
        while (reader.HasData)
        {
            var tag = reader.PeekTag();
            if (tag == DirectoryNameTag)
            {
                names.Add((tag, reader.ReadSequence(tag).ReadEncodedValue().ToArray()));
            }
            else if (tag == EmailTag || tag == UriTag)
            {
                names.Add((tag, reader.ReadOctetString(tag)));
            }
            else
            {
                reader.ReadEncodedValue();
            }
        }

        return names;
    }

    private static List<string> DnsNamesOrCommonNames(X509Certificate2 leaf, List<string> dnsNames) =>
        dnsNames.Count > 0 ? dnsNames : ListCommonNames(leaf);

    private static List<string> ListCommonNames(X509Certificate2 leaf) =>
        leaf.SubjectName.EnumerateRelativeDistinguishedNames()
            .Where(rdn => !rdn.HasMultipleElements && rdn.GetSingleElementType().Value == CommonNameOid)
            .Select(rdn => rdn.GetSingleElementValue())
            .OfType<string>()
            .ToList();

    private static int FindViolation(X509Extension extension, List<(Asn1Tag Tag, byte[] Value)> names)
    {
        var constraints = new AsnReader(extension.RawData, AsnEncodingRules.DER).ReadSequence();
        var permitted = new List<(Asn1Tag Tag, byte[] Value)>();
        var excluded = new List<(Asn1Tag Tag, byte[] Value)>();
        while (constraints.HasData)
        {
            var tag = constraints.PeekTag();
            var subtrees = constraints.ReadSequence(tag);
            (tag.TagValue == 0 ? permitted : excluded).AddRange(ReadSubtrees(subtrees));
        }

        return BreaksPermitted(permitted, names) ? NotPermitted : BreaksExcluded(excluded, names) ? Excluded : 0;
    }

    private static List<(Asn1Tag Tag, byte[] Value)> ReadSubtrees(AsnReader subtrees)
    {
        var read = new List<(Asn1Tag, byte[])>();
        while (subtrees.HasData)
        {
            var subtree = subtrees.ReadSequence();
            var tag = subtree.PeekTag();
            if (tag == DirectoryNameTag)
            {
                read.Add((tag, subtree.ReadSequence(tag).ReadEncodedValue().ToArray()));
            }
            else if (OctetNameTags.Contains(tag))
            {
                read.Add((tag, subtree.ReadOctetString(tag)));
            }
        }

        return read;
    }

    private static bool BreaksPermitted(List<(Asn1Tag Tag, byte[] Value)> permitted, List<(Asn1Tag Tag, byte[] Value)> names) =>
        names.Any(name =>
        {
            // Permitted address subtrees of only the other family leave this name not permitted (BL-5433).
            return permitted.Any(subtree => subtree.Tag == name.Tag)
                && !permitted.Any(subtree => Applies(subtree, name) && Matches(subtree.Tag, subtree.Value, name.Value));
        });

    // A constraint applies to a name of its kind; an address constraint only to its own family.
    private static bool Applies((Asn1Tag Tag, byte[] Value) subtree, (Asn1Tag Tag, byte[] Value) name) =>
        subtree.Tag == name.Tag && (subtree.Tag != IpAddressTag || subtree.Value.Length == name.Value.Length * 2);

    private static bool BreaksExcluded(List<(Asn1Tag Tag, byte[] Value)> excluded, List<(Asn1Tag Tag, byte[] Value)> names) =>
        names.Any(name => excluded.Any(subtree => Applies(subtree, name) && Matches(subtree.Tag, subtree.Value, name.Value)));

    private static bool Matches(Asn1Tag tag, byte[] constraint, byte[] name) =>
        tag == DnsNameTag ? DnsMatches(System.Text.Encoding.ASCII.GetString(constraint), System.Text.Encoding.ASCII.GetString(name))
        : tag == IpAddressTag ? IpMatches(constraint, name)
        : tag == DirectoryNameTag ? DirectoryNameMatches(constraint, name)
        : tag == EmailTag ? EmailMatches(System.Text.Encoding.ASCII.GetString(constraint), System.Text.Encoding.ASCII.GetString(name))
        : UriMatches(System.Text.Encoding.ASCII.GetString(constraint), System.Text.Encoding.ASCII.GetString(name));

    // A directoryName constraint matches a name that begins with the same relative names.
    internal static bool DirectoryNameMatches(byte[] constraint, byte[] name)
    {
        var constraintNames = new AsnReader(constraint, AsnEncodingRules.DER).ReadSequence();
        var nameNames = new AsnReader(name, AsnEncodingRules.DER).ReadSequence();
        while (constraintNames.HasData)
        {
            if (!nameNames.HasData || !RelativeNamesEqual(constraintNames.ReadSetOf(), nameNames.ReadSetOf()))
            {
                return false;
            }
        }

        return true;
    }

    // Attribute types are equal and values compare as RFC 5280 section 7.1 says: string type ignored, case folded, spaces folded.
    private static bool RelativeNamesEqual(AsnReader constraint, AsnReader name)
    {
        var constraintAttributes = ReadAttributes(constraint);
        var nameAttributes = ReadAttributes(name);
        return constraintAttributes.Count == nameAttributes.Count
            && constraintAttributes.All(attribute => nameAttributes.Contains(attribute));
    }

    private static List<(string Type, string Value)> ReadAttributes(AsnReader relativeName)
    {
        var attributes = new List<(string, string)>();
        while (relativeName.HasData)
        {
            var pair = relativeName.ReadSequence();
            var type = pair.ReadObjectIdentifier();
            attributes.Add((type, NormalisedValue(pair.ReadEncodedValue())));
        }

        return attributes;
    }

    private static string NormalisedValue(ReadOnlyMemory<byte> encoded)
    {
        var reader = new AsnReader(encoded, AsnEncodingRules.DER);
        var tag = reader.PeekTag();
        if (tag.TagClass != TagClass.Universal || !StringTagValues.Contains(tag.TagValue))
        {
            return Convert.ToHexString(encoded.Span);
        }

        var text = ReadText(reader, tag);
        return string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
    }

    // UniversalString is four bytes per character, big endian (UTF-32BE); every other type the reader decodes itself.
    private static string ReadText(AsnReader reader, Asn1Tag tag)
    {
        if (tag.TagValue != UniversalStringTagValue)
        {
            return reader.ReadCharacterString((UniversalTagNumber)tag.TagValue);
        }

        reader.TryReadPrimitiveCharacterStringBytes(tag, out var bytes);
        return new System.Text.UTF32Encoding(bigEndian: true, byteOrderMark: false).GetString(bytes.Span);
    }

    private const int UniversalStringTagValue = 28;

    // UTF8String, PrintableString, TeletexString, IA5String, UniversalString, BMPString.
    private static readonly int[] StringTagValues = [12, 19, 20, 22, UniversalStringTagValue, 30];

    // A constraint with an @ names one mailbox, otherwise a host, or with a leading dot the hosts below it.
    private static bool EmailMatches(string constraint, string name) =>
        constraint.Contains('@')
            ? name.Equals(constraint, StringComparison.OrdinalIgnoreCase)
            : HostMatches(constraint, name[(name.LastIndexOf('@') + 1)..]);

    private static bool UriMatches(string constraint, string name) =>
        Uri.TryCreate(name, UriKind.Absolute, out var uri) && HostMatches(constraint, uri.Host);

    private static bool HostMatches(string constraint, string host) =>
        constraint.StartsWith('.')
            ? host.EndsWith(constraint, StringComparison.OrdinalIgnoreCase)
            : host.Equals(constraint, StringComparison.OrdinalIgnoreCase);

    private static bool DnsMatches(string constraint, string name)
    {
        if (constraint.Length == 0 || name.Equals(constraint, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var suffix = constraint.StartsWith('.') ? constraint : "." + constraint;
        return name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IpMatches(byte[] constraint, byte[] address)
    {
        var length = address.Length;
        return Enumerable.Range(0, length).All(i => (address[i] & constraint[length + i]) == (constraint[i] & constraint[length + i]));
    }
}
