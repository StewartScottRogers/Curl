using System.Formats.Asn1;
using System.Numerics;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Networking;

/// <summary>
/// What a certificate revocation list's extensions say about the certificates it answers for
/// (RFC 5280, 5.2): an unrecognised critical extension, a delta list, an indirect list, a list
/// of some reasons only, and the issuing distribution point's name and CA / user scope. A list
/// outside its scope cannot say a certificate is not revoked (STD-113).
/// </summary>
internal sealed class RevocationListScope
{
    private const string AuthorityKeyIdentifier = "2.5.29.35";
    private const string CrlNumber = "2.5.29.20";
    private const string IssuerAlternativeName = "2.5.29.18";
    private const string FreshestCrl = "2.5.29.46";
    private const string AuthorityInformationAccess = "1.3.6.1.5.5.7.1.1";
    private const string IssuingDistributionPoint = "2.5.29.28";
    private const string DeltaCrlIndicator = "2.5.29.27";
    private const string ReasonCode = "2.5.29.21";
    private const string InvalidityDate = "2.5.29.24";
    private const string CertificateIssuer = "2.5.29.29";

    private static readonly Asn1Tag ContextTag0 = new(TagClass.ContextSpecific, 0);
    private static readonly Asn1Tag ContextTag1 = new(TagClass.ContextSpecific, 1);
    private static readonly Asn1Tag ContextTag2 = new(TagClass.ContextSpecific, 2);
    private static readonly Asn1Tag ContextTag4 = new(TagClass.ContextSpecific, 4);
    private static readonly Asn1Tag ContextTag5 = new(TagClass.ContextSpecific, 5);
    private static readonly Asn1Tag ContextTag6 = new(TagClass.ContextSpecific, 6);

    private List<string>? _distributionPointAddresses;
    private bool _onlyContainsUserCerts;
    private bool _onlyContainsCaCerts;

    /// <summary>Gets whether the list is a delta list (RFC 5280, 5.2.4): never the whole answer on its own.</summary>
    public bool IsDelta { get; private set; }

    /// <summary>Gets the list's <c>cRLNumber</c>, or <see langword="null" /> when it has none.</summary>
    public BigInteger? Number { get; private set; }

    /// <summary>Gets the <c>BaseCRLNumber</c> of a delta list, or <see langword="null" /> for a list that is not one.</summary>
    public BigInteger? BaseNumber { get; private set; }

    /// <summary>Gets the value of the list's <c>freshestCRL</c> extension, or <see langword="null" /> when it has none.</summary>
    public byte[]? FreshestCrlValue { get; private set; }

    /// <summary>Gets or sets whether the list can answer for no certificate at all.</summary>
    public bool Unusable { get; set; }

    /// <summary>Reads the list's <c>[0] EXPLICIT Extensions</c>, when the list has them.</summary>
    /// <param name="signed">The reader positioned after the revoked certificates.</param>
    /// <returns>The scope the extensions give.</returns>
    public static RevocationListScope Read(AsnReader signed)
    {
        var scope = new RevocationListScope();
        if (!signed.HasData)
        {
            return scope;
        }

        try
        {
            var extensions = signed.ReadSequence(ContextTag0).ReadSequence();
            while (extensions.HasData)
            {
                scope.ReadListExtension(extensions.ReadSequence());
            }
        }
        catch (AsnContentException)
        {
            scope.Unusable = true;
        }

        return scope;
    }

    /// <summary>Returns whether an entry's extensions make the list unusable.</summary>
    /// <param name="entry">The entry's reader, positioned at its <c>crlEntryExtensions</c>.</param>
    /// <returns><see langword="true" /> for an unrecognised critical extension or a certificate issuer one.</returns>
    public static bool EntryExtensionsAreUnusable(AsnReader entry)
    {
        try
        {
            var extensions = entry.ReadSequence();
            while (extensions.HasData)
            {
                if (IsUnusableEntryExtension(extensions.ReadSequence()))
                {
                    return true;
                }
            }

            return false;
        }
        catch (AsnContentException)
        {
            return true;
        }
    }

    private static bool IsUnusableEntryExtension(AsnReader extension)
    {
        var oid = extension.ReadObjectIdentifier();
        var critical = extension.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean) && extension.ReadBoolean();
        return oid == CertificateIssuer || (critical && oid is not (ReasonCode or InvalidityDate));
    }

    /// <summary>Returns whether the list answers for <paramref name="certificate" /> fetched from <paramref name="distributionPoint" />.</summary>
    /// <param name="certificate">The certificate checked.</param>
    /// <param name="distributionPoint">The address the list came from.</param>
    /// <returns><see langword="true" /> when the scope covers it.</returns>
    public bool Covers(X509Certificate2 certificate, Uri distributionPoint)
    {
        return !Unusable && !IsDelta && AdmitsKindOf(certificate) && NamesAddress(distributionPoint);
    }

    /// <summary>
    /// Returns whether the list is a delta list that updates a base list numbered
    /// <paramref name="baseListNumber" /> and answers for <paramref name="certificate" />'s kind (RFC 5280, 5.2.4).
    /// </summary>
    /// <param name="certificate">The certificate checked.</param>
    /// <param name="baseListNumber">The <c>cRLNumber</c> of the base list, or <see langword="null" /> when it has none.</param>
    /// <returns><see langword="true" /> when the delta list is usable with that base list.</returns>
    public bool CoversAsDelta(X509Certificate2 certificate, BigInteger? baseListNumber) =>
        !Unusable && IsDelta && baseListNumber >= BaseNumber && AdmitsKindOf(certificate);

    private bool AdmitsKindOf(X509Certificate2 certificate)
    {
        var isCa = certificate.Extensions.OfType<X509BasicConstraintsExtension>().Any(constraints => constraints.CertificateAuthority);
        return (!_onlyContainsUserCerts || !isCa) && (!_onlyContainsCaCerts || isCa);
    }

    private bool NamesAddress(Uri distributionPoint) =>
        _distributionPointAddresses is null
        || _distributionPointAddresses.Any(address => Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri == distributionPoint);

    private void ReadListExtension(AsnReader extension)
    {
        var oid = extension.ReadObjectIdentifier();
        var critical = extension.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean) && extension.ReadBoolean();
        var value = extension.ReadOctetString();
        switch (oid)
        {
            case IssuingDistributionPoint:
                ReadIssuingDistributionPoint(value);
                break;
            case CrlNumber:
                Number = new AsnReader(value, AsnEncodingRules.DER).ReadInteger();
                break;
            case DeltaCrlIndicator:
                IsDelta = true;
                BaseNumber = new AsnReader(value, AsnEncodingRules.DER).ReadInteger();
                break;
            case FreshestCrl:
                FreshestCrlValue = value;
                break;
            case AuthorityKeyIdentifier or IssuerAlternativeName or AuthorityInformationAccess:
                break;
            default:
                Unusable |= critical;
                break;
        }
    }

    private void ReadIssuingDistributionPoint(byte[] value)
    {
        var reader = new AsnReader(value, AsnEncodingRules.DER).ReadSequence();
        while (reader.HasData)
        {
            ReadIssuingDistributionPointField(reader);
        }
    }

    private void ReadIssuingDistributionPointField(AsnReader reader)
    {
        var tag = reader.PeekTag();
        if (tag.HasSameClassAndValue(ContextTag0))
        {
            ReadDistributionPointName(reader.ReadSequence(ContextTag0));
        }
        else if (tag.HasSameClassAndValue(ContextTag1))
        {
            _onlyContainsUserCerts = reader.ReadBoolean(ContextTag1);
        }
        else if (tag.HasSameClassAndValue(ContextTag2))
        {
            _onlyContainsCaCerts = reader.ReadBoolean(ContextTag2);
        }
        else if (tag.HasSameClassAndValue(ContextTag4))
        {
            Unusable |= reader.ReadBoolean(ContextTag4);
        }
        else if (tag.HasSameClassAndValue(ContextTag5))
        {
            Unusable |= reader.ReadBoolean(ContextTag5);
        }
        else
        {
            // onlySomeReasons [3], or a field this reader does not know: the list's scope is not known.
            reader.ReadEncodedValue();
            Unusable = true;
        }
    }

    // DistributionPointName: fullName [0] GeneralNames; a nameRelativeToCRLIssuer [1] is not matched.
    private void ReadDistributionPointName(AsnReader name)
    {
        if (!name.PeekTag().HasSameClassAndValue(ContextTag0))
        {
            Unusable = true;
            return;
        }

        var generalNames = name.ReadSequence(ContextTag0);
        _distributionPointAddresses ??= [];
        while (generalNames.HasData)
        {
            if (generalNames.PeekTag().HasSameClassAndValue(ContextTag6))
            {
                _distributionPointAddresses.Add(generalNames.ReadCharacterString(UniversalTagNumber.IA5String, ContextTag6));
            }
            else
            {
                generalNames.ReadEncodedValue();
            }
        }
    }
}
