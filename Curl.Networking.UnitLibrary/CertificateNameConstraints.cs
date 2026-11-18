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

    private static readonly Asn1Tag DnsNameTag = new(TagClass.ContextSpecific, 2);

    private static readonly Asn1Tag IpAddressTag = new(TagClass.ContextSpecific, 7);

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
            var names = ListNames(elements[0].Certificate);
            for (var index = 1; index < elements.Count; index++)
            {
                var violation = FindViolation(elements[index].Certificate, names);
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

    private static List<(Asn1Tag Tag, byte[] Value)> ListNames(X509Certificate2 leaf)
    {
        var names = new List<(Asn1Tag, byte[])>();
        var extension = leaf.Extensions.OfType<X509SubjectAlternativeNameExtension>().FirstOrDefault();
        if (extension is null)
        {
            return names;
        }

        names.AddRange(extension.EnumerateDnsNames().Select(name => (DnsNameTag, System.Text.Encoding.ASCII.GetBytes(name))));
        names.AddRange(extension.EnumerateIPAddresses().Select(address => (IpAddressTag, address.GetAddressBytes())));
        return names;
    }

    private static int FindViolation(X509Certificate2 authority, List<(Asn1Tag Tag, byte[] Value)> names)
    {
        var extension = authority.Extensions[NameConstraintsOid];
        if (extension is null)
        {
            return 0;
        }

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
            if (tag == DnsNameTag || tag == IpAddressTag)
            {
                read.Add((tag, subtree.ReadOctetString(tag)));
            }
        }

        return read;
    }

    private static bool BreaksPermitted(List<(Asn1Tag Tag, byte[] Value)> permitted, List<(Asn1Tag Tag, byte[] Value)> names) =>
        names.Any(name =>
        {
            var ofKind = permitted.Where(subtree => Applies(subtree, name)).ToList();
            return ofKind.Count > 0 && !ofKind.Any(subtree => Matches(subtree.Tag, subtree.Value, name.Value));
        });

    // A constraint applies to a name of its kind; an address constraint only to its own family.
    private static bool Applies((Asn1Tag Tag, byte[] Value) subtree, (Asn1Tag Tag, byte[] Value) name) =>
        subtree.Tag == name.Tag && (subtree.Tag == DnsNameTag || subtree.Value.Length == name.Value.Length * 2);

    private static bool BreaksExcluded(List<(Asn1Tag Tag, byte[] Value)> excluded, List<(Asn1Tag Tag, byte[] Value)> names) =>
        names.Any(name => excluded.Any(subtree => Applies(subtree, name) && Matches(subtree.Tag, subtree.Value, name.Value)));

    private static bool Matches(Asn1Tag tag, byte[] constraint, byte[] name) =>
        tag == DnsNameTag ? DnsMatches(System.Text.Encoding.ASCII.GetString(constraint), System.Text.Encoding.ASCII.GetString(name))
        : IpMatches(constraint, name);

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
