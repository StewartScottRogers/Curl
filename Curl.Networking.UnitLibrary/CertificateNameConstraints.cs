using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// Evaluates the name constraints of the CA certificates in a chain against the names of the server
/// certificate and, in the OpenSSL build, of the intermediates below them. Windows' chain engine never
/// evaluates the constraints of a non-self-signed <c>--cacert</c> anchor (it reports only a partial chain),
/// yet curl's Schannel build refuses the chain with <c>CERT_TRUST_HAS_NOT_PERMITTED_NAME_CONSTRAINT</c>
/// or <c>CERT_TRUST_HAS_EXCLUDED_NAME_CONSTRAINT</c> (BL-5375).
/// </summary>
internal static class CertificateNameConstraints
{
    /// <summary>
    /// Schannel's <c>CERT_TRUST_HAS_NOT_SUPPORTED_NAME_CONSTRAINT</c>: here only the OpenSSL build's
    /// refusal of a subject CN holding a NUL, or of a subject emailAddress that is not an IA5String
    /// (<c>X509_V_ERR_UNSUPPORTED_NAME_SYNTAX</c>).
    /// </summary>
    internal const int UnsupportedNameSyntax = 0x1000;

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

    // Every name kind constraints are checked for.
    private static readonly Asn1Tag[] ConstrainedNameTags = [DirectoryNameTag, .. OctetNameTags];

    // The kinds the Schannel build's names take from here: its DNS names and addresses are listed apart.
    private static readonly Asn1Tag[] SchannelOtherNameTags = [DirectoryNameTag, EmailTag, UriTag];

    /// <summary>
    /// Finds the first name constraint a certificate's names break. The Schannel build checks the server
    /// certificate's names only: it reads the CAs from the leaf upwards and, for each, checks every name
    /// against the permitted subtrees before any against the excluded ones. The OpenSSL build checks the
    /// certificates from the top of the chain down, a self-issued one only when it is the server's own, each
    /// against the CAs above it from the top down, and takes the names one at a time, each against the
    /// permitted then the excluded subtrees: the subject, its emailAddresses, then the alternative names in
    /// the order the certificate holds them, and last, for the server certificate, its subject CNs.
    /// </summary>
    /// <param name="chain">The chain built for the server certificate, leaf first.</param>
    /// <param name="matchesSchannelBuild">
    /// Whether every subject CN of a leaf with no dNSName is checked as a DNS name, as curl's Schannel
    /// build (CryptoAPI) does; otherwise only a CN that looks like a host name is, as in the OpenSSL build.
    /// Subject CNs and emailAddresses in a multi-valued relative name are read in the OpenSSL build only.
    /// </param>
    /// <returns>
    /// <see cref="NotPermitted" />, <see cref="Excluded" />, <see cref="UnsupportedNameSyntax" /> (the
    /// OpenSSL build only), or 0 when no constraint is broken.
    /// </returns>
    internal static int FindViolation(X509Chain chain, bool matchesSchannelBuild = false)
    {
        if (chain.ChainElements.Count < 2)
        {
            return 0;
        }

        try
        {
            var certificates = OpenSslVerifyResult.CertificatesOf(chain)[..CountThroughAnchor(chain)];
            return matchesSchannelBuild
                ? FindViolationAgainst(certificates.Skip(1), () => ReadSchannelNames(certificates[0]))
                : FindOpenSslViolation(certificates);
        }
        catch (Exception exception) when (exception is AsnContentException or CryptographicException)
        {
            // A server-controlled name or constraint that does not decode is a refused chain (BL-5415).
            return NotPermitted;
        }
    }

    // OpenSSL's check_name_constraints takes the certificates from the top of the chain (or the listed anchor)
    // down, a self-issued one only when it is the server's own, and checks each against the CAs above it, the
    // highest first; the first refusal is its answer (BL-5950, BL-5953).
    private static int FindOpenSslViolation(X509Certificate2[] certificates) =>
        Enumerable.Range(0, certificates.Length)
            .Reverse()
            .Where(index => index == 0 || !OpenSslVerifyResult.IsSelfIssued(certificates[index]))
            .Select(index => FindViolationAgainst(
                certificates.Skip(index + 1).Reverse(), () => ReadOpenSslNames(certificates[index], isServerCertificate: index == 0)))
            .FirstOrDefault(violation => violation != 0);

    // The first violation of the authorities' constraints, taken in the order given. The names are read only
    // once some authority carries constraints (BL-5424).
    private static int FindViolationAgainst(IEnumerable<X509Certificate2> authorities, Func<CheckedNames> readNames)
    {
        var constraints = authorities.Select(authority => authority.Extensions[NameConstraintsOid]).OfType<X509Extension>().ToList();
        if (constraints.Count == 0)
        {
            return 0;
        }

        var names = readNames();
        return constraints.Select(extension => FindViolation(extension, names)).FirstOrDefault(violation => violation != 0);
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

    // A certificate's names in the groups checked one after another (all of them at once in the Schannel build,
    // one name each in the OpenSSL build), the subject CNs the OpenSSL build checks after them, one by one, and
    // whether the names stop short at a subject emailAddress the OpenSSL build refuses to check.
    private sealed record CheckedNames(
        List<List<(Asn1Tag Tag, byte[] Value)>> CheckedTogether, List<string> OpenSslCommonNames, bool StopsAtUnsupportedEmailAddress = false);

    // Both builds check the subject CN as a DNS name when the leaf has no dNSName SAN (BL-5432, BL-5829).
    private static CheckedNames ReadSchannelNames(X509Certificate2 leaf)
    {
        var dnsNames = ListDnsNames(leaf);
        return new CheckedNames(
            [ListSchannelNames(leaf, dnsNames.Count > 0 ? dnsNames : ListSingleValuedAttributes(leaf, CommonNameOid))], []);
    }

    private static List<string> ListDnsNames(X509Certificate2 certificate) =>
        certificate.Extensions.OfType<X509SubjectAlternativeNameExtension>()
            .SelectMany(extension => extension.EnumerateDnsNames())
            .ToList();

    // OpenSSL's NAME_CONSTRAINTS_check takes the subject's directoryName, then each subject emailAddress, and
    // stops at one that is not an IA5String, before any alternative name or CN (BL-5942). It takes the
    // alternative names in the extension's order and answers for the first name it refuses (BL-5950). Only the
    // server certificate's CNs are checked, and only when it has no dNSName (BL-5953).
    private static CheckedNames ReadOpenSslNames(X509Certificate2 certificate, bool isServerCertificate)
    {
        var checksCommonNames = isServerCertificate && ListDnsNames(certificate).Count == 0;
        var encodedEmailAddresses = ListEveryEncodedAttribute(certificate, EmailAddressOid);
        var emailAddresses = encodedEmailAddresses.TakeWhile(IsIa5String).SelectMany(value => DecodeCharacterString(value)).ToList();
        var subjectNames = ListSubjectNames(certificate, emailAddresses);
        return emailAddresses.Count < encodedEmailAddresses.Count
            ? new CheckedNames(CheckOneByOne(subjectNames), [], StopsAtUnsupportedEmailAddress: true)
            : new CheckedNames(
                CheckOneByOne(subjectNames.Concat(ListAlternativeNames(certificate, ConstrainedNameTags))),
                checksCommonNames ? ListEveryCommonName(certificate) : []);
    }

    private static List<List<(Asn1Tag Tag, byte[] Value)>> CheckOneByOne(IEnumerable<(Asn1Tag Tag, byte[] Value)> names) =>
        names.Select(name => new List<(Asn1Tag Tag, byte[] Value)> { name }).ToList();

    private static bool IsIa5String(byte[] encoded) =>
        Asn1Tag.Decode(encoded, out _).HasSameClassAndValue(new Asn1Tag(UniversalTagNumber.IA5String));

    private static List<(Asn1Tag Tag, byte[] Value)> ListSchannelNames(X509Certificate2 leaf, List<string> dnsNames)
    {
        var names = dnsNames.Select(DnsName).ToList();
        names.AddRange(leaf.Extensions.OfType<X509SubjectAlternativeNameExtension>()
            .SelectMany(extension => extension.EnumerateIPAddresses())
            .Select(address => (IpAddressTag, address.GetAddressBytes())));

        // directoryName, rfc822Name and URI names are constrained too; the subject counts as a directoryName (BL-5436)
        // and its emailAddress attributes as rfc822Names (RFC 5280 Â§4.2.1.10, BL-5804).
        names.AddRange(ListSubjectNames(leaf, ListSingleValuedAttributes(leaf, EmailAddressOid)));
        names.AddRange(ListAlternativeNames(leaf, SchannelOtherNameTags));
        return names;
    }

    private static (Asn1Tag Tag, byte[] Value) DnsName(string name) => (DnsNameTag, System.Text.Encoding.ASCII.GetBytes(name));

    private static List<(Asn1Tag Tag, byte[] Value)> ListSubjectNames(X509Certificate2 leaf, List<string> emailAddresses)
    {
        var names = new List<(Asn1Tag, byte[])>();
        if (leaf.SubjectName.RawData.Length > EmptyNameLength)
        {
            names.Add((DirectoryNameTag, leaf.SubjectName.RawData));
        }

        names.AddRange(emailAddresses.Select(address => (EmailTag, System.Text.Encoding.ASCII.GetBytes(address))));
        return names;
    }

    // The alternative names of the kinds asked for, in the order the extension holds them.
    private static List<(Asn1Tag Tag, byte[] Value)> ListAlternativeNames(X509Certificate2 leaf, Asn1Tag[] kinds)
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
            if (kinds.Contains(tag))
            {
                names.Add((tag, ReadNameValue(reader, tag)));
            }
            else
            {
                reader.ReadEncodedValue();
            }
        }

        return names;
    }

    // A directoryName is an explicitly tagged Name; every other constrained kind is a primitive string or octet string.
    private static byte[] ReadNameValue(AsnReader reader, Asn1Tag tag) =>
        tag == DirectoryNameTag ? reader.ReadSequence(tag).ReadEncodedValue().ToArray() : reader.ReadOctetString(tag);

    // OpenSSL's NAME_CONSTRAINTS_check_CN takes the CNs in order and stops at the first it refuses. It
    // drops trailing NULs, refuses a NUL inside a CN whatever the constraints' kinds, and checks a CN
    // against DNS constraints only when it looks like a host name (cn2dnsid in crypto/x509/v3_ncons.c).
    private static int FindCommonNameViolation(
        List<(Asn1Tag Tag, byte[] Value)> permitted, List<(Asn1Tag Tag, byte[] Value)> excluded, List<string> commonNames) =>
        commonNames
            .Select(commonName => commonName.TrimEnd('\0'))
            .Select(commonName =>
                commonName.Contains('\0') ? UnsupportedNameSyntax
                : LooksLikeHostName(commonName) ? FindViolation(permitted, excluded, [DnsName(commonName)])
                : 0)
            .FirstOrDefault(violation => violation != 0);

    // At least two dot-separated labels of letters, digits, '_' and '-', none empty or edged with '-'.
    private static bool LooksLikeHostName(string commonName)
    {
        var labels = commonName.Split('.');
        return labels.Length > 1 && labels.All(IsHostNameLabel);
    }

    private static bool IsHostNameLabel(string label) =>
        label.Length > 0
        && label[0] != '-'
        && label[^1] != '-'
        && label.All(character => char.IsAsciiLetterOrDigit(character) || character is '_' or '-');

    private static List<string> ListSingleValuedAttributes(X509Certificate2 leaf, string oid) =>
        leaf.SubjectName.EnumerateRelativeDistinguishedNames()
            .Where(rdn => !rdn.HasMultipleElements && rdn.GetSingleElementType().Value == oid)
            .Select(rdn => rdn.GetSingleElementValue())
            .OfType<string>()
            .ToList();

    /// <summary>
    /// The subject's CN values in the order they appear, those in a multi-valued relative name included,
    /// as OpenSSL reads them. A NUL is kept, a character the string type does not allow does not throw,
    /// and a value that is not a character string is left out.
    /// </summary>
    /// <param name="certificate">The certificate whose subject is read.</param>
    /// <returns>The CN values.</returns>
    internal static List<string> ListEveryCommonName(X509Certificate2 certificate) => ListEveryAttribute(certificate, CommonNameOid);

    private static List<string> ListEveryAttribute(X509Certificate2 certificate, string oid) =>
        ListEveryEncodedAttribute(certificate, oid).SelectMany(value => DecodeCharacterString(value)).ToList();

    // OpenSSL's NAME_CONSTRAINTS_check and its CN check read every attribute of a type, multi-valued relative names included (BL-5940).
    private static List<byte[]> ListEveryEncodedAttribute(X509Certificate2 certificate, string oid)
    {
        var values = new List<byte[]>();
        var relativeNames = new AsnReader(certificate.SubjectName.RawData, AsnEncodingRules.BER).ReadSequence();
        while (relativeNames.HasData)
        {
            var attributes = relativeNames.ReadSetOf();
            while (attributes.HasData)
            {
                var pair = attributes.ReadSequence();
                if (pair.ReadObjectIdentifier() == oid)
                {
                    values.Add(pair.ReadEncodedValue().ToArray());
                }
            }
        }

        return values;
    }

    /// <summary>
    /// Decodes an encoded subject attribute value without validating its characters.
    /// </summary>
    /// <param name="encoded">The value's tag, length and content.</param>
    /// <returns>The decoded string alone, or nothing for a value that is not a character string.</returns>
    internal static string[] DecodeCharacterString(ReadOnlySpan<byte> encoded)
    {
        var tag = AsnDecoder.ReadEncodedValue(encoded, AsnEncodingRules.BER, out var offset, out var length, out _);
        if (tag.TagClass != TagClass.Universal || !CharacterStringTagValues.Contains(tag.TagValue))
        {
            return [];
        }

        System.Text.Encoding encoding = tag.TagValue switch
        {
            UniversalStringTagValue => new System.Text.UTF32Encoding(bigEndian: true, byteOrderMark: false),
            BmpStringTagValue => System.Text.Encoding.BigEndianUnicode,
            _ => System.Text.Encoding.UTF8,
        };
        return [encoding.GetString(encoded.Slice(offset, length))];
    }

    private static int FindViolation(X509Extension extension, CheckedNames names)
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

        var violation = names.CheckedTogether
            .Select(together => FindViolation(permitted, excluded, together))
            .FirstOrDefault(found => found != 0);
        return violation != 0 ? violation
            : names.StopsAtUnsupportedEmailAddress ? UnsupportedNameSyntax
            : FindCommonNameViolation(permitted, excluded, names.OpenSslCommonNames);
    }

    private static int FindViolation(
        List<(Asn1Tag Tag, byte[] Value)> permitted, List<(Asn1Tag Tag, byte[] Value)> excluded, List<(Asn1Tag Tag, byte[] Value)> names) =>
        BreaksPermitted(permitted, names) ? NotPermitted : BreaksExcluded(excluded, names) ? Excluded : 0;

    private static List<(Asn1Tag Tag, byte[] Value)> ReadSubtrees(AsnReader subtrees)
    {
        var read = new List<(Asn1Tag, byte[])>();
        while (subtrees.HasData)
        {
            var subtree = subtrees.ReadSequence();
            var tag = subtree.PeekTag();
            if (ConstrainedNameTags.Contains(tag))
            {
                read.Add((tag, ReadNameValue(subtree, tag)));
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

    private const int BmpStringTagValue = 30;

    // StringTagValues and NumericString and VisibleString, which a subject attribute may use too.
    private static readonly int[] CharacterStringTagValues = [12, 18, 19, 20, 22, 26, UniversalStringTagValue, BmpStringTagValue];

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
