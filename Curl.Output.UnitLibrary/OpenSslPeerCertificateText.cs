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
/// Key</c>, <c>rsa(n)</c> and <c>rsa(e)</c>, and <c>Signature</c>.
/// </para>
/// <para>
/// An extension is printed as <c>X509V3_EXT_print</c> prints it for the key and
/// subject key identifiers, basic constraints, key usage, extended key usage and the
/// alternative names; any other, and one whose value cannot be read, as
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
    private const string AuthorityKeyIdentifier = "2.5.29.35";
    private const string ExtendedKeyUsage = "2.5.29.37";

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
    };

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
        if (keyAlgorithm == RsaEncryption)
        {
            AppendRsaPublicKey(text, certificate.PublicKey.EncodedKeyValue.RawData);
        }

        AppendRecord(text, "Signature", FormatSignature(certificate.RawData));
        PeerCertificateText.AppendPem(text, certificate.RawData);
        return text.ToString();
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
        var names = KeyUsageNames.Where((_, bit) => bit / 8 < bits.Length && (bits[bit / 8] & (0x80 >> (bit % 8))) != 0);
        return string.Join(", ", names);
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
    private static List<string> ReadGeneralNames(AsnReader names)
    {
        var sequence = names.PeekTag().HasSameClassAndValue(Asn1Tag.Sequence) ? names.ReadSequence() : names;
        var values = new List<string>();
        while (sequence.HasData)
        {
            values.Add(FormatGeneralName(sequence));
        }

        return values;
    }

    private static string FormatGeneralName(AsnReader names)
    {
        var tag = names.PeekTag();
        return tag.TagValue switch
        {
            1 => "email:" + Encoding.Latin1.GetString(names.ReadOctetString(tag)),
            2 => "DNS:" + Encoding.Latin1.GetString(names.ReadOctetString(tag)),
            4 => "DirName:" + FormatOneLineName(names.ReadSequence(tag).ReadEncodedValue().ToArray()),
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
