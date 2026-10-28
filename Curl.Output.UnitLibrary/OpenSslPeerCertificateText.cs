// Ported from curl 8.22.0 lib/vtls/openssl.c (ossl_certchain, X509V3_ext, pubkey_show),
// Copyright (C) Daniel Stenberg, <daniel@haxx.se>, et al., under the curl licence
// (SPDX-License-Identifier: curl), with the OpenSSL 3 printers it calls.

using System.Formats.Asn1;
using System.Globalization;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Prints one certificate the server sent as curl's OpenSSL build prints it in
/// <c>%{certs}</c>: the records <c>ossl_certchain</c> in <c>lib/vtls/openssl.c</c> makes,
/// one <c>label:value</c> line each, ending with the certificate in PEM form (CGR-091,
/// ADR-0054). Measured against curl 8.18.0 with OpenSSL 3.5.5 on Linux (BL-3620).
/// </summary>
/// <remarks>
/// <para>
/// The records are <c>Subject</c> and <c>Issuer</c> (<c>XN_FLAG_ONELINE</c>),
/// <c>Version</c> (in hex), <c>Serial Number</c> (lower-case hex), <c>Signature
/// Algorithm</c>, <c>Public Key Algorithm</c>, one record per extension named by its
/// OpenSSL long name, <c>Start date</c>, <c>Expire date</c>, an RSA key's <c>RSA Public
/// Key</c>, <c>rsa(n)</c> and <c>rsa(e)</c> (or <c>dsa(...)</c> or <c>dh(...)</c> for a DSA or DH key), and <c>Signature</c>.
/// </para>
/// <para>
/// An extension is printed as <c>X509V3_EXT_print</c> prints it for the key and
/// subject key identifiers, basic constraints, key usage, extended key usage, the
/// alternative names, CRL distribution points, authority and subject information access,
/// certificate policies, name and policy constraints, inhibit any policy, signed
/// certificate timestamps and the Netscape type and comment; any other, and one whose value cannot be read, as
/// <c>ASN1_STRING_print</c> prints its raw value, with a dot for each byte that is not
/// printable.
/// </para>
/// </remarks>
internal static class OpenSslPeerCertificateText
{
    private const string RsaEncryption = "1.2.840.113549.1.1.1";
    private const string SubjectKeyIdentifier = "2.5.29.14";
    private const string KeyUsage = "2.5.29.15";
    private const string SubjectAlternativeName = "2.5.29.17";
    private const string IssuerAlternativeName = "2.5.29.18";
    private const string BasicConstraints = "2.5.29.19";
    private const string NameConstraints = "2.5.29.30";
    private const string CrlDistributionPoints = "2.5.29.31";
    private const string CertificatePolicies = "2.5.29.32";
    private const string AuthorityKeyIdentifier = "2.5.29.35";
    private const string PolicyConstraints = "2.5.29.36";
    private const string ExtendedKeyUsage = "2.5.29.37";
    private const string FreshestCrl = "2.5.29.46";
    private const string InhibitAnyPolicy = "2.5.29.54";
    private const string AuthorityInformationAccess = "1.3.6.1.5.5.7.1.1";
    private const string SubjectInformationAccess = "1.3.6.1.5.5.7.1.11";
    private const string SignedCertificateTimestamps = "1.3.6.1.4.1.11129.2.4.2";
    private const string NetscapeCertType = "2.16.840.1.113730.1.1";
    private const string NetscapeComment = "2.16.840.1.113730.1.13";
    private const string DsaEncryption = "1.2.840.10040.4.1";
    private const string DhKeyAgreement = "1.2.840.113549.1.3.1";
    private const string CpsQualifier = "1.3.6.1.5.5.7.2.1";
    private const string UserNoticeQualifier = "1.3.6.1.5.5.7.2.2";

    // OBJ_obj2txt's long names for the key algorithms; any other prints dotted.
    private static readonly Dictionary<string, string> KeyAlgorithmNames = new(StringComparer.Ordinal)
    {
        [RsaEncryption] = "rsaEncryption",
        ["1.2.840.113549.1.1.10"] = "rsassaPss",
        ["1.2.840.10045.2.1"] = "id-ecPublicKey",
        ["1.2.840.10040.4.1"] = "dsaEncryption",
        ["1.2.840.113549.1.3.1"] = "dhKeyAgreement",
        ["1.2.840.10046.2.1"] = "X9.42 DH",
        ["1.3.101.112"] = "ED25519",
        ["1.3.101.113"] = "ED448",
    };

    private static readonly Dictionary<string, string> ExtensionNames = new(StringComparer.Ordinal)
    {
        [SubjectKeyIdentifier] = "X509v3 Subject Key Identifier",
        [KeyUsage] = "X509v3 Key Usage",
        [SubjectAlternativeName] = "X509v3 Subject Alternative Name",
        [IssuerAlternativeName] = "X509v3 Issuer Alternative Name",
        [BasicConstraints] = "X509v3 Basic Constraints",
        [AuthorityKeyIdentifier] = "X509v3 Authority Key Identifier",
        [ExtendedKeyUsage] = "X509v3 Extended Key Usage",
        [NameConstraints] = "X509v3 Name Constraints",
        [CrlDistributionPoints] = "X509v3 CRL Distribution Points",
        [CertificatePolicies] = "X509v3 Certificate Policies",
        [PolicyConstraints] = "X509v3 Policy Constraints",
        [FreshestCrl] = "X509v3 Freshest CRL",
        [InhibitAnyPolicy] = "X509v3 Inhibit Any Policy",
        [AuthorityInformationAccess] = "Authority Information Access",
        [SubjectInformationAccess] = "Subject Information Access",
        [SignedCertificateTimestamps] = "CT Precertificate SCTs",
        [NetscapeCertType] = "Netscape Cert Type",
        [NetscapeComment] = "Netscape Comment",
    };

    // The X509V3_EXT_print printers ported; any other extension prints as its raw value.
    private static readonly Dictionary<string, Func<byte[], string>> ExtensionPrinters = new(StringComparer.Ordinal)
    {
        [SubjectKeyIdentifier] = value => ColonHex(new AsnReader(value, AsnEncodingRules.BER).ReadOctetString()),
        [KeyUsage] = FormatKeyUsage,
        [SubjectAlternativeName] = FormatGeneralNames,
        [IssuerAlternativeName] = FormatGeneralNames,
        [BasicConstraints] = FormatBasicConstraints,
        [AuthorityKeyIdentifier] = FormatAuthorityKeyIdentifier,
        [ExtendedKeyUsage] = FormatExtendedKeyUsage,
        [NameConstraints] = FormatNameConstraints,
        [CrlDistributionPoints] = FormatDistributionPoints,
        [FreshestCrl] = FormatDistributionPoints,
        [CertificatePolicies] = FormatCertificatePolicies,
        [PolicyConstraints] = FormatPolicyConstraints,
        [InhibitAnyPolicy] = value => new AsnReader(value, AsnEncodingRules.BER).ReadInteger().ToString(CultureInfo.InvariantCulture),
        [AuthorityInformationAccess] = FormatAccessDescriptions,
        [SubjectInformationAccess] = FormatAccessDescriptions,
        [SignedCertificateTimestamps] = FormatSignedCertificateTimestamps,
        [NetscapeCertType] = FormatNetscapeCertType,
        [NetscapeComment] = value => new AsnReader(value, AsnEncodingRules.BER).ReadCharacterString(UniversalTagNumber.IA5String),
    };

    // The names X509V3_EXT_print gives the ids of an authority or subject information access.
    private static readonly Dictionary<string, string> AccessMethodNames = new(StringComparer.Ordinal)
    {
        ["1.3.6.1.5.5.7.48.1"] = "OCSP",
        ["1.3.6.1.5.5.7.48.2"] = "CA Issuers",
        ["1.3.6.1.5.5.7.48.3"] = "AD Time Stamping",
        ["1.3.6.1.5.5.7.48.4"] = "ad dvcs",
        ["1.3.6.1.5.5.7.48.5"] = "CA Repository",
    };

    // The NetscapeCertType bits in order, as v3_netscape's table names them.
    private static readonly string[] NetscapeCertTypeNames =
    [
        "SSL Client", "SSL Server", "S/MIME", "Object Signing", "Unused", "SSL CA", "S/MIME CA", "Object Signing CA",
    ];

    // The ReasonFlags bits in order, as v3_crld.c names them.
    private static readonly string[] ReasonNames =
    [
        "Unused", "Key Compromise", "CA Compromise", "Affiliation Changed", "Superseded",
        "Cessation Of Operation", "Certificate Hold", "Privilege Withdrawn", "AA Compromise",
    ];

    private static readonly Dictionary<string, string> KeyPurposeNames = new(StringComparer.Ordinal)
    {
        ["1.3.6.1.5.5.7.3.1"] = "TLS Web Server Authentication",
        ["1.3.6.1.5.5.7.3.2"] = "TLS Web Client Authentication",
        ["1.3.6.1.5.5.7.3.3"] = "Code Signing",
        ["1.3.6.1.5.5.7.3.4"] = "E-mail Protection",
        ["1.3.6.1.5.5.7.3.8"] = "Time Stamping",
        ["1.3.6.1.5.5.7.3.9"] = "OCSP Signing",
        ["2.5.29.37.0"] = "Any Extended Key Usage",
    };

    // The KeyUsage bits in order, as v3_bitst.c names them.
    private static readonly string[] KeyUsageNames =
    [
        "Digital Signature", "Non Repudiation", "Key Encipherment", "Data Encipherment",
        "Key Agreement", "Certificate Sign", "CRL Sign", "Encipher Only", "Decipher Only",
    ];

    /// <summary>Prints one certificate.</summary>
    /// <param name="certificate">The certificate's DER encoding.</param>
    /// <returns>Its lines, each ending in a line feed; empty when it cannot be read.</returns>
    internal static string Format(ReadOnlyMemory<byte> certificate)
    {
        try
        {
            using var x509 = X509CertificateLoader.LoadCertificate(certificate.Span);
            return Format(x509);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }

    private static string Format(X509Certificate2 certificate)
    {
        var text = new StringBuilder();
        AppendRecord(text, "Subject", OpenSslDistinguishedNameText.FormatOneLine(certificate.SubjectName) ?? string.Empty);
        AppendRecord(text, "Issuer", OpenSslDistinguishedNameText.FormatOneLine(certificate.IssuerName) ?? string.Empty);
        AppendRecord(text, "Version", (certificate.Version - 1).ToString("x", CultureInfo.InvariantCulture));
        AppendRecord(text, "Serial Number", FormatSerialNumber(certificate.SerialNumberBytes.ToArray()));
        var signatureAlgorithm = certificate.SignatureAlgorithm.Value!;
        AppendRecord(text, "Signature Algorithm", OpenSslCertificateText.SignatureAlgorithmNames.GetValueOrDefault(signatureAlgorithm, signatureAlgorithm));
        var keyAlgorithm = certificate.PublicKey.Oid.Value!;
        AppendRecord(text, "Public Key Algorithm", KeyAlgorithmNames.GetValueOrDefault(keyAlgorithm, keyAlgorithm));
        foreach (var extension in certificate.Extensions)
        {
            var oid = extension.Oid!.Value!;
            AppendRecord(text, ExtensionNames.GetValueOrDefault(oid, oid), FormatExtension(oid, extension.RawData));
        }

        AppendRecord(text, "Start date", OpenSslCertificateText.FormatTime(certificate.NotBefore));
        AppendRecord(text, "Expire date", OpenSslCertificateText.FormatTime(certificate.NotAfter));
        AppendPublicKeyRecords(text, certificate.PublicKey, keyAlgorithm);
        AppendRecord(text, "Signature", FormatSignature(certificate.RawData));
        PeerCertificateText.AppendPem(text, certificate.RawData);
        return text.ToString();
    }

    private static void AppendPublicKeyRecords(StringBuilder text, PublicKey key, string keyAlgorithm)
    {
        if (keyAlgorithm == RsaEncryption)
        {
            AppendRsaPublicKey(text, key.EncodedKeyValue.RawData);
        }
        else if (keyAlgorithm is DsaEncryption or DhKeyAgreement)
        {
            AppendFiniteFieldKey(text, key, keyAlgorithm == DsaEncryption);
        }
    }

    private static void AppendRecord(StringBuilder text, string label, string value)
    {
        PeerCertificateText.AppendRecord(text, label, value);
    }

    // The INTEGER's magnitude in lower-case hex, after a minus sign when it is negative.
    private static string FormatSerialNumber(byte[] serialNumber)
    {
        var value = new BigInteger(serialNumber, isBigEndian: true);
        var magnitude = BigInteger.Abs(value).ToByteArray(isUnsigned: true, isBigEndian: true);
        return (value.Sign < 0 ? "-" : string.Empty) + Convert.ToHexStringLower(magnitude);
    }

    private static string FormatExtension(string oid, byte[] value)
    {
        try
        {
            return ExtensionPrinters.TryGetValue(oid, out var print) ? print(value) : FormatRawString(value);
        }
        catch (AsnContentException)
        {
            return FormatRawString(value);
        }
    }

    // ASN1_STRING_print: every byte, with a dot for one past '~' or a control other than CR and LF.
    private static string FormatRawString(byte[] value)
    {
        var text = new StringBuilder(value.Length);
        foreach (var octet in value)
        {
            text.Append(octet > '~' || (octet < ' ' && octet != '\n' && octet != '\r') ? '.' : (char)octet);
        }

        return text.ToString();
    }

    // OPENSSL_buf2hexstr: upper-case hex pairs joined by colons.
    private static string ColonHex(byte[] bytes)
    {
        return string.Join(':', bytes.Select(octet => octet.ToString("X2", CultureInfo.InvariantCulture)));
    }

    private static string FormatKeyUsage(byte[] value)
    {
        var bits = new AsnReader(value, AsnEncodingRules.BER).ReadBitString(out _);
        return string.Join(", ", BitNames(bits, KeyUsageNames));
    }

    // The names of the bits that are set, a bit being counted from the most significant of the first byte.
    private static IEnumerable<string> BitNames(ReadOnlyMemory<byte> bits, string[] names)
    {
        var set = bits.ToArray();
        return names.Where((_, bit) => bit / 8 < set.Length && (set[bit / 8] & (0x80 >> (bit % 8))) != 0);
    }

    private static string FormatNetscapeCertType(byte[] value)
    {
        return string.Join(", ", BitNames(new AsnReader(value, AsnEncodingRules.BER).ReadBitString(out _), NetscapeCertTypeNames));
    }

    private static string FormatBasicConstraints(byte[] value)
    {
        var sequence = new AsnReader(value, AsnEncodingRules.BER).ReadSequence();
        var isAuthority = sequence.HasData && sequence.PeekTag().HasSameClassAndValue(Asn1Tag.Boolean) && sequence.ReadBoolean();
        var text = "CA:" + (isAuthority ? "TRUE" : "FALSE");
        return sequence.HasData ? text + ", pathlen:" + sequence.ReadInteger().ToString(CultureInfo.InvariantCulture) : text;
    }

    // i2v_AUTHORITY_KEYID's values, one per line: the key identifier, named keyid only when
    // an issuer or serial number follows it, the issuer's names and the serial number.
    private static string FormatAuthorityKeyIdentifier(byte[] value)
    {
        var sequence = new AsnReader(value, AsnEncodingRules.BER).ReadSequence();
        var keyIdentifier = NextIsContextTag(sequence, 0) ? ColonHex(sequence.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 0))) : null;
        var values = NextIsContextTag(sequence, 1) ? ReadGeneralNames(sequence.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 1))) : [];
        if (sequence.HasData)
        {
            values.Add("serial:" + ColonHex(sequence.ReadIntegerBytes(new Asn1Tag(TagClass.ContextSpecific, 2)).ToArray()));
        }

        if (keyIdentifier is not null)
        {
            values.Insert(0, values.Count > 0 ? "keyid:" + keyIdentifier : keyIdentifier);
        }

        return string.Join('\n', values);
    }

    private static bool NextIsContextTag(AsnReader sequence, int tagValue)
    {
        return sequence.HasData && sequence.PeekTag().HasSameClassAndValue(new Asn1Tag(TagClass.ContextSpecific, tagValue));
    }

    private static string FormatExtendedKeyUsage(byte[] value)
    {
        var sequence = new AsnReader(value, AsnEncodingRules.BER).ReadSequence();
        var names = new List<string>();
        while (sequence.HasData)
        {
            var purpose = sequence.ReadObjectIdentifier();
            names.Add(KeyPurposeNames.GetValueOrDefault(purpose, purpose));
        }

        return string.Join(", ", names);
    }

    private static string FormatGeneralNames(byte[] value)
    {
        return string.Join(", ", ReadGeneralNames(new AsnReader(value, AsnEncodingRules.BER)));
    }

    // i2v_GENERAL_NAMES over the GeneralNames the reader holds.
    private static List<string> ReadGeneralNames(AsnReader names, bool asPrinted = false)
    {
        var sequence = names.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence) ? names.ReadSequence() : names;
        var values = new List<string>();
        while (sequence.HasData)
        {
            values.Add(FormatGeneralName(sequence, asPrinted));
        }

        return values;
    }

    // asPrinted selects GENERAL_NAME_print, which names a directory with X509_NAME_print_ex,
    // over i2v_GENERAL_NAME, which uses X509_NAME_oneline.
    private static string FormatGeneralName(AsnReader names, bool asPrinted = false)
    {
        var tag = names.PeekTag();
        return tag.TagValue switch
        {
            1 => "email:" + Encoding.Latin1.GetString(names.ReadOctetString(tag)),
            2 => "DNS:" + Encoding.Latin1.GetString(names.ReadOctetString(tag)),
            4 => "DirName:" + FormatDirectoryName(names.ReadSequence(tag).ReadEncodedValue().ToArray(), asPrinted),
            6 => "URI:" + Encoding.Latin1.GetString(names.ReadOctetString(tag)),
            7 => "IP Address:" + FormatIpAddress(names.ReadOctetString(tag)),
            8 => "Registered ID:" + names.ReadObjectIdentifier(tag),
            _ => FormatUnsupportedName(names),
        };
    }

    private static string FormatUnsupportedName(AsnReader names)
    {
        var tagValue = names.PeekTag().TagValue;
        names.ReadEncodedValue();
        return tagValue switch
        {
            0 => "othername:<unsupported>",
            3 => "X400Name:<unsupported>",
            _ => "EdiPartyName:<unsupported>",
        };
    }

    // ossl_ipaddr_to_asc: dotted IPv4, IPv6 as eight upper-case hex groups, else <invalid>.
    private static string FormatIpAddress(byte[] address)
    {
        return address.Length switch
        {
            4 => string.Join('.', address),
            16 => string.Join(':', Enumerable.Range(0, 8).Select(group => ((address[group * 2] << 8) | address[(group * 2) + 1]).ToString("X", CultureInfo.InvariantCulture))),
            _ => "<invalid>",
        };
    }

    private static string FormatDirectoryName(byte[] name, bool asPrinted)
    {
        return asPrinted ? string.Concat(OpenSslDistinguishedNameText.FormatOneLine(new X500DistinguishedName(name))) : FormatOneLineName(name);
    }

    private static string TrimLineFeed(StringBuilder text)
    {
        return text.ToString().TrimEnd('\n');
    }

    // i2r_crldp: each distribution point's name, reasons and CRL issuer, over several lines.
    private static string FormatDistributionPoints(byte[] value)
    {
        var points = new AsnReader(value, AsnEncodingRules.BER).ReadSequence();
        var text = new StringBuilder();
        while (points.HasData)
        {
            AppendDistributionPoint(text, points.ReadSequence());
        }

        return TrimLineFeed(text);
    }

    private static void AppendDistributionPoint(StringBuilder text, AsnReader point)
    {
        var context0 = new Asn1Tag(TagClass.ContextSpecific, 0);
        var context1 = new Asn1Tag(TagClass.ContextSpecific, 1);
        if (NextIsContextTag(point, 0))
        {
            var name = point.ReadSequence(context0);
            if (NextIsContextTag(name, 0))
            {
                AppendPrintedNames(text, "Full Name:", name.ReadSequence(context0));
            }
            else
            {
                text.Append("Relative Name:\n  ").Append(FormatRelativeName(name.ReadSequence(context1))).Append('\n');
            }
        }

        if (NextIsContextTag(point, 1))
        {
            var reasons = BitNames(point.ReadBitString(out _, context1), ReasonNames).ToArray();
            text.Append("Reasons:\n  ").Append(reasons.Length == 0 ? "<none>" : string.Join(", ", reasons)).Append('\n');
        }

        if (NextIsContextTag(point, 2))
        {
            AppendPrintedNames(text, "CRL Issuer:", point.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 2)));
        }
    }

    private static void AppendPrintedNames(StringBuilder text, string heading, AsnReader names)
    {
        text.Append(heading).Append('\n');
        foreach (var name in ReadGeneralNames(names, asPrinted: true))
        {
            text.Append("  ").Append(name).Append('\n');
        }
    }

    // A RelativeDistinguishedName's attributes, printed as a name of that one RDN.
    private static string FormatRelativeName(AsnReader attributes)
    {
        var writer = new AsnWriter(AsnEncodingRules.BER);
        using (writer.PushSequence())
        using (writer.PushSetOf())
        {
            while (attributes.HasData)
            {
                writer.WriteEncodedValue(attributes.ReadEncodedValue().Span);
            }
        }

        return FormatDirectoryName(writer.Encode(), asPrinted: true);
    }

    // i2r_certpol: each policy's id, then its CPS and user notice qualifiers indented under it.
    private static string FormatCertificatePolicies(byte[] value)
    {
        var policies = new AsnReader(value, AsnEncodingRules.BER).ReadSequence();
        var text = new StringBuilder();
        while (policies.HasData)
        {
            var policy = policies.ReadSequence();
            var id = policy.ReadObjectIdentifier();
            text.Append("Policy: ").Append(id == "2.5.29.32.0" ? "X509v3 Any Policy" : id).Append('\n');
            if (policy.HasData)
            {
                AppendPolicyQualifiers(text, policy.ReadSequence());
            }
        }

        return TrimLineFeed(text);
    }

    private static void AppendPolicyQualifiers(StringBuilder text, AsnReader qualifiers)
    {
        while (qualifiers.HasData)
        {
            var qualifier = qualifiers.ReadSequence();
            var id = qualifier.ReadObjectIdentifier();
            if (id == CpsQualifier)
            {
                text.Append("  CPS: ").Append(qualifier.ReadCharacterString(UniversalTagNumber.IA5String)).Append('\n');
            }
            else if (id == UserNoticeQualifier)
            {
                text.Append("  User Notice:\n");
                AppendUserNotice(text, qualifier.ReadSequence());
            }
            else
            {
                text.Append("  Unknown Qualifier: ").Append(id).Append('\n');
            }
        }
    }

    // print_notice: the organization and notice numbers, then the explicit text.
    private static void AppendUserNotice(StringBuilder text, AsnReader notice)
    {
        if (notice.HasData && notice.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence))
        {
            AppendNoticeReference(text, notice.ReadSequence());
        }

        if (notice.HasData)
        {
            text.Append("    Explicit Text: ").Append(ReadDisplayText(notice)).Append('\n');
        }
    }

    private static void AppendNoticeReference(StringBuilder text, AsnReader reference)
    {
        text.Append("    Organization: ").Append(ReadDisplayText(reference)).Append('\n');
        var numbers = new List<string>();
        var numberReader = reference.ReadSequence();
        while (numberReader.HasData)
        {
            numbers.Add(numberReader.ReadInteger().ToString(CultureInfo.InvariantCulture));
        }

        if (numbers.Count > 0)
        {
            text.Append("    Number").Append(numbers.Count > 1 ? "s" : string.Empty).Append(": ").Append(string.Join(", ", numbers)).Append('\n');
        }
    }

    private static string ReadDisplayText(AsnReader reader)
    {
        return reader.ReadCharacterString((UniversalTagNumber)reader.PeekTag().TagValue);
    }

    // do_i2r_name_constraints: the permitted then the excluded subtrees' bases; minimum and maximum are not printed.
    private static string FormatNameConstraints(byte[] value)
    {
        var sequence = new AsnReader(value, AsnEncodingRules.BER).ReadSequence();
        var text = new StringBuilder();
        if (NextIsContextTag(sequence, 0))
        {
            AppendSubtrees(text, "Permitted:", sequence.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 0)));
        }

        if (NextIsContextTag(sequence, 1))
        {
            AppendSubtrees(text, "Excluded:", sequence.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, 1)));
        }

        return TrimLineFeed(text);
    }

    private static void AppendSubtrees(StringBuilder text, string heading, AsnReader subtrees)
    {
        text.Append(heading).Append('\n');
        while (subtrees.HasData)
        {
            var subtree = subtrees.ReadSequence();
            var addressTag = new Asn1Tag(TagClass.ContextSpecific, 7);
            var name = subtree.PeekTag().HasSameClassAndValue(addressTag)
                ? "IP:" + FormatAddressAndMask(subtree.ReadOctetString(addressTag))
                : FormatGeneralName(subtree, asPrinted: true);
            text.Append("  ").Append(name).Append('\n');
        }
    }

    // print_nc_ipadd: an address then its mask, as the two halves of the value.
    private static string FormatAddressAndMask(byte[] value)
    {
        return value.Length is 8 or 32 ? FormatIpAddress(value[..(value.Length / 2)]) + "/" + FormatIpAddress(value[(value.Length / 2)..]) : "<invalid>";
    }

    private static string FormatPolicyConstraints(byte[] value)
    {
        var sequence = new AsnReader(value, AsnEncodingRules.BER).ReadSequence();
        var values = new List<string>();
        if (NextIsContextTag(sequence, 0))
        {
            values.Add("Require Explicit Policy:" + sequence.ReadInteger(new Asn1Tag(TagClass.ContextSpecific, 0)).ToString(CultureInfo.InvariantCulture));
        }

        if (NextIsContextTag(sequence, 1))
        {
            values.Add("Inhibit Policy Mapping:" + sequence.ReadInteger(new Asn1Tag(TagClass.ContextSpecific, 1)).ToString(CultureInfo.InvariantCulture));
        }

        return string.Join(", ", values);
    }

    // i2v_AUTHORITY_INFO_ACCESS: one line per access description, its method then its location.
    private static string FormatAccessDescriptions(byte[] value)
    {
        var descriptions = new AsnReader(value, AsnEncodingRules.BER).ReadSequence();
        var lines = new List<string>();
        while (descriptions.HasData)
        {
            var description = descriptions.ReadSequence();
            var method = description.ReadObjectIdentifier();
            lines.Add(AccessMethodNames.GetValueOrDefault(method, method) + " - " + FormatGeneralName(description));
        }

        return string.Join('\n', lines);
    }

    // i2r_SCT_LIST: each timestamp of the TLS-encoded list, as SCT_print writes it.
    private static string FormatSignedCertificateTimestamps(byte[] value)
    {
        var list = new AsnReader(value, AsnEncodingRules.BER).ReadOctetString();
        if (list.Length < 2 || ((list[0] << 8) | list[1]) != list.Length - 2)
        {
            throw new AsnContentException();
        }

        var timestamps = new List<string>();
        var position = 2;
        while (position < list.Length)
        {
            if (position + 2 > list.Length)
            {
                throw new AsnContentException();
            }

            var length = (list[position] << 8) | list[position + 1];
            position += 2;
            if (position + length > list.Length)
            {
                throw new AsnContentException();
            }

            timestamps.Add(FormatTimestamp(list.AsSpan(position, length)));
            position += length;
        }

        return string.Join('\n', timestamps);
    }

    // SCT_print: a version 1 timestamp field by field; any other version as its bytes.
    private static string FormatTimestamp(ReadOnlySpan<byte> timestamp)
    {
        const string heading = "Signed Certificate Timestamp:\n    Version   : ";
        var rowIndent = new string(' ', 16);
        if (timestamp.Length == 0 || timestamp[0] != 0)
        {
            return heading + "unknown\n" + rowIndent + HexRows(timestamp);
        }

        var signatureStart = FindSignatureStart(timestamp);
        var extensionLength = signatureStart - 43;
        var time = ReadTimestampTime(timestamp.Slice(33, 8));
        var algorithm = SignatureAlgorithmName(timestamp[signatureStart], timestamp[signatureStart + 1]);
        return heading + "v1 (0x0)"
            + "\n    Log ID    : " + HexRows(timestamp.Slice(1, 32))
            + string.Create(CultureInfo.InvariantCulture, $"\n    Timestamp : {time:MMM} {time.Day,2} {time:HH:mm:ss}.{time.Millisecond:D3} {time.Year} GMT")
            + "\n    Extensions: " + (extensionLength == 0 ? "none" : HexRows(timestamp.Slice(43, extensionLength)))
            + "\n    Signature : " + algorithm
            + "\n" + rowIndent + HexRows(timestamp[(signatureStart + 4)..]);
    }

    // Where a version 1 timestamp's signature algorithm begins, once its parts fill the timestamp exactly.
    private static int FindSignatureStart(ReadOnlySpan<byte> timestamp)
    {
        if (timestamp.Length < 47)
        {
            throw new AsnContentException();
        }

        var signatureStart = 43 + ((timestamp[41] << 8) | timestamp[42]);
        if (signatureStart + 4 > timestamp.Length || signatureStart + 4 + ((timestamp[signatureStart + 2] << 8) | timestamp[signatureStart + 3]) != timestamp.Length)
        {
            throw new AsnContentException();
        }

        return signatureStart;
    }

    private static DateTime ReadTimestampTime(ReadOnlySpan<byte> milliseconds)
    {
        var sinceEpoch = (long)System.Buffers.Binary.BinaryPrimitives.ReadUInt64BigEndian(milliseconds);
        return sinceEpoch is < 0 or > 253402300799999 ? throw new AsnContentException() : DateTime.UnixEpoch.AddMilliseconds(sinceEpoch);
    }

    private static string SignatureAlgorithmName(byte hash, byte signature)
    {
        return (hash, signature) switch
        {
            (4, 3) => "ecdsa-with-SHA256",
            (4, 1) => "sha256WithRSAEncryption",
            _ => "unknown",
        };
    }

    // BIO_hex_string at width 16: upper-case pairs joined by colons, a line break after each full row.
    private static string HexRows(ReadOnlySpan<byte> bytes)
    {
        var text = new StringBuilder();
        for (var index = 0; index < bytes.Length; index++)
        {
            text.Append(bytes[index].ToString("X2", CultureInfo.InvariantCulture));
            if (index < bytes.Length - 1)
            {
                text.Append(index % 16 == 15 ? ":\n" + new string(' ', 16) : ":");
            }
        }

        return text.ToString();
    }

    // X509_NAME_oneline: /SN=value for every attribute, a byte outside ' '..'~' as \xHH.
    private static string FormatOneLineName(byte[] name)
    {
        var text = new StringBuilder();
        var relativeNames = new AsnReader(name, AsnEncodingRules.BER).ReadSequence();
        while (relativeNames.HasData)
        {
            var attributes = relativeNames.ReadSetOf();
            while (attributes.HasData)
            {
                var attribute = attributes.ReadSequence();
                var type = attribute.ReadObjectIdentifier();
                var encoded = attribute.ReadEncodedValue();
                AsnDecoder.ReadEncodedValue(encoded.Span, AsnEncodingRules.BER, out var contentOffset, out var contentLength, out _);
                text.Append('/').Append(OpenSslDistinguishedNameText.ShortNames.GetValueOrDefault(type, type)).Append('=');
                AppendEscaped(text, encoded.Span.Slice(contentOffset, contentLength));
            }
        }

        return text.ToString();
    }

    private static void AppendEscaped(StringBuilder text, ReadOnlySpan<byte> value)
    {
        foreach (var octet in value)
        {
            if (octet < ' ' || octet > '~')
            {
                text.Append(CultureInfo.InvariantCulture, $"\\x{octet:X2}");
            }
            else
            {
                text.Append((char)octet);
            }
        }
    }

    // pubkey_show: the modulus's size in bits, then n and e as BN_print writes them.
    private static void AppendRsaPublicKey(StringBuilder text, byte[] key)
    {
        BigInteger modulus;
        BigInteger exponent;
        try
        {
            var sequence = new AsnReader(key, AsnEncodingRules.BER).ReadSequence();
            modulus = sequence.ReadInteger();
            exponent = sequence.ReadInteger();
        }
        catch (AsnContentException)
        {
            return;
        }

        AppendRecord(text, "RSA Public Key", modulus.GetBitLength().ToString(CultureInfo.InvariantCulture));
        AppendRecord(text, "rsa(n)", FormatBigNumber(modulus));
        AppendRecord(text, "rsa(e)", FormatBigNumber(exponent));
    }

    // get_pkey_dsa and get_pkey_dh: pubkey_show pushes p, q, g and pub_key, a number the key
    // does not hold (a PKCS #3 DH key has no q) as an empty value.
    private static void AppendFiniteFieldKey(StringBuilder text, PublicKey key, bool isDsa)
    {
        BigInteger? q = null;
        BigInteger p;
        BigInteger g;
        BigInteger publicValue;
        try
        {
            var parameters = new AsnReader(key.EncodedParameters!.RawData, AsnEncodingRules.BER).ReadSequence();
            p = parameters.ReadInteger();
            if (isDsa)
            {
                q = parameters.ReadInteger();
            }

            g = parameters.ReadInteger();
            publicValue = new AsnReader(key.EncodedKeyValue.RawData, AsnEncodingRules.BER).ReadInteger();
        }
        catch (AsnContentException)
        {
            return;
        }

        var type = isDsa ? "dsa" : "dh";
        AppendRecord(text, type + "(p)", FormatBigNumber(p));
        AppendRecord(text, type + "(q)", q is null ? string.Empty : FormatBigNumber(q.Value));
        AppendRecord(text, type + "(g)", FormatBigNumber(g));
        AppendRecord(text, type + "(pub_key)", FormatBigNumber(publicValue));
    }

    // BN_print: upper-case hex without leading zeros, 0 for zero, a minus sign when negative.
    private static string FormatBigNumber(BigInteger number)
    {
        var digits = Convert.ToHexString(BigInteger.Abs(number).ToByteArray(isUnsigned: true, isBigEndian: true)).TrimStart('0');
        return (number.Sign < 0 ? "-" : string.Empty) + (digits.Length == 0 ? "0" : digits);
    }

    // Every byte of the signature BIT STRING as lower-case hex, each followed by a colon.
    private static string FormatSignature(byte[] certificate)
    {
        var sequence = new AsnReader(certificate, AsnEncodingRules.BER).ReadSequence();
        sequence.ReadEncodedValue();
        sequence.ReadEncodedValue();
        var signature = sequence.ReadBitString(out _);
        return string.Concat(signature.Select(octet => octet.ToString("x2", CultureInfo.InvariantCulture) + ":"));
    }
}
