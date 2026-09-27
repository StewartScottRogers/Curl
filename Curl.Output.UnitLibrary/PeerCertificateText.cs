// Ported from curl 8.21.0 lib/vtls/x509asn1.c, Copyright (C) Daniel Stenberg,
// <daniel@haxx.se>, et al., under the curl licence (SPDX-License-Identifier: curl).

using System.Globalization;
using System.Text;

namespace Curl.Output;

/// <summary>
/// Prints one certificate the server sent as curl 8.21.0 prints it in <c>%{certs}</c>:
/// the records its <c>Curl_extract_certinfo</c> in <c>lib/vtls/x509asn1.c</c> makes, one
/// <c>label:value</c> line each, ending with the certificate in PEM form (ADR-0047).
/// </summary>
/// <remarks>
/// <para>
/// The records are <c>Subject</c>, <c>Issuer</c>, <c>Version</c> (in hex, <c>2</c> for a
/// version 3 certificate), <c>Serial Number</c>, <c>Signature Algorithm</c>,
/// <c>Start Date</c>, <c>Expire Date</c>, <c>Public Key Algorithm</c>, the key's own
/// records, and <c>Signature</c>. An EC key adds <c>ECC Public Key</c> (its size in bits)
/// and <c>ecPublicKey</c>; an RSA key <c>RSA Public Key</c>, <c>rsa(n)</c> and
/// <c>rsa(e)</c>; a DSA key <c>dsa(p)</c>, <c>dsa(q)</c>, <c>dsa(g)</c> and
/// <c>dsa(pub_key)</c>; a Diffie-Hellman key <c>dh(p)</c>, <c>dh(g)</c> and
/// <c>dh(pub_key)</c>; any other nothing. A value ends at its first NUL character, as
/// curl's C string does, and a line already ending in a line feed gets no second one.
/// </para>
/// <para>
/// A certificate curl cannot read makes curl fail the handshake; this tool has already
/// finished the transfer by the time it prints, so such a certificate prints nothing.
/// </para>
/// </remarks>
public static class PeerCertificateText
{
    private const int PemLineLength = 64;

    /// <summary>Prints one certificate.</summary>
    /// <param name="certificate">The certificate's DER encoding.</param>
    /// <returns>Its lines, each ending in a line feed; empty when curl could not read it.</returns>
    public static string Format(ReadOnlyMemory<byte> certificate)
    {
        var der = certificate.ToArray();
        try
        {
            return Format(der, X509CertificateFields.Parse(der));
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    private static string Format(byte[] der, X509CertificateFields fields)
    {
        var text = new StringBuilder();
        AppendRecord(text, "Subject", DerText.FormatDistinguishedName(der, fields.Subject));
        AppendRecord(text, "Issuer", DerText.FormatDistinguishedName(der, fields.Issuer));
        AppendRecord(text, "Version", FormatVersion(der, fields.Version));
        AppendRecord(text, "Serial Number", DerText.Format(der, fields.SerialNumber));
        AppendRecord(text, "Signature Algorithm", FormatAlgorithm(der, fields.SignatureAlgorithm, out _));
        AppendRecord(text, "Start Date", DerText.Format(der, fields.NotBefore));
        AppendRecord(text, "Expire Date", DerText.Format(der, fields.NotAfter));
        var keyAlgorithm = FormatAlgorithm(der, fields.PublicKeyAlgorithm, out var keyParameters);
        AppendRecord(text, "Public Key Algorithm", keyAlgorithm);
        AppendPublicKey(text, der, keyAlgorithm, keyParameters, fields.PublicKey);
        AppendRecord(text, "Signature", DerText.Format(der, fields.Signature));
        AppendPem(text, der);
        return text.ToString();
    }

    private static void AppendRecord(StringBuilder text, string label, string value)
    {
        var end = value.IndexOf('\0', StringComparison.Ordinal);
        text.Append(label).Append(':').Append(end < 0 ? value : value[..end]);
        if (text[^1] != '\n')
        {
            text.Append('\n');
        }
    }

    // Every content byte of the version INTEGER, shifted in, printed in hex; 0 when absent.
    private static string FormatVersion(byte[] der, DerElement? version)
    {
        uint number = 0;
        if (version is { } present)
        {
            foreach (var octet in der.AsSpan(present.Start, present.Length))
            {
                number = (number << 8) | octet;
            }
        }

        return number.ToString("x", CultureInfo.InvariantCulture);
    }

    // curl's dumpAlgo: the algorithm's name, with the element after it as its parameters,
    // or an empty element at the end when there is none.
    private static string FormatAlgorithm(byte[] der, DerElement algorithm, out DerElement parameters)
    {
        var identifier = DerReader.Read(der, algorithm.Start, algorithm.End);
        parameters = identifier.Next < algorithm.End
            ? DerReader.Read(der, identifier.Next, algorithm.End)
            : new DerElement(0, false, algorithm.End, algorithm.End, algorithm.End);
        return DerText.FormatObjectIdentifier(der.AsSpan(identifier.Start, identifier.Length).ToArray());
    }

    private static void AppendPublicKey(StringBuilder text, byte[] der, string algorithm, DerElement parameters, DerElement publicKey)
    {
        if (string.Equals(algorithm, "ecPublicKey", StringComparison.OrdinalIgnoreCase))
        {
            AppendEcPublicKey(text, der, publicKey);
            return;
        }

        // Past the BIT STRING's unused-bits byte, the key is one element.
        var key = DerReader.Read(der, publicKey.Start + 1, publicKey.End);
        if (string.Equals(algorithm, "rsaEncryption", StringComparison.OrdinalIgnoreCase))
        {
            AppendRsaPublicKey(text, der, key);
        }
        else if (string.Equals(algorithm, "dsa", StringComparison.OrdinalIgnoreCase))
        {
            AppendParameterisedPublicKey(text, der, parameters, key, ["dsa(p)", "dsa(q)", "dsa(g)"], "dsa(pub_key)");
        }
        else if (string.Equals(algorithm, "dhpublicnumber", StringComparison.OrdinalIgnoreCase))
        {
            AppendParameterisedPublicKey(text, der, parameters, key, ["dh(p)", "dh(g)"], "dh(pub_key)");
        }
    }

    // The size is the key's bytes, less the unused-bits and point-format bytes, times four.
    private static void AppendEcPublicKey(StringBuilder text, byte[] der, DerElement publicKey)
    {
        if (publicKey.Length < 2)
        {
            throw new FormatException("The EC public key is too short.");
        }

        AppendRecord(text, "ECC Public Key", ((publicKey.Length - 2) * 4).ToString(CultureInfo.InvariantCulture));
        AppendRecord(text, "ecPublicKey", DerText.Format(der, publicKey));
    }

    // The size counts the modulus's bits from its first set bit; the leading zero bytes are
    // dropped from rsa(n) when that size is over 32.
    private static void AppendRsaPublicKey(StringBuilder text, byte[] der, DerElement key)
    {
        var modulus = DerReader.Read(der, key.Start, key.End);
        var firstSignificant = modulus.Start;
        while (firstSignificant < modulus.End && der[firstSignificant] == 0)
        {
            firstSignificant++;
        }

        var bits = (modulus.End - firstSignificant) * 8;
        if (bits > 0)
        {
            bits -= byte.LeadingZeroCount(der[firstSignificant]);
        }

        var printed = bits > 32 ? modulus.StartingAt(firstSignificant) : modulus;
        AppendRecord(text, "RSA Public Key", bits.ToString(CultureInfo.InvariantCulture));
        AppendRecord(text, "rsa(n)", DerText.Format(der, printed));
        AppendRecord(text, "rsa(e)", DerText.Format(der, DerReader.Read(der, modulus.Next, key.End)));
    }

    // Each parameter in turn while one can be read, then the key once all were; curl stops
    // without complaint at a parameter it cannot read.
    private static void AppendParameterisedPublicKey(
        StringBuilder text,
        byte[] der,
        DerElement parameters,
        DerElement key,
        string[] parameterLabels,
        string keyLabel)
    {
        var position = parameters.Start;
        foreach (var label in parameterLabels)
        {
            if (!DerReader.TryRead(der, position, parameters.End, out var parameter))
            {
                return;
            }

            AppendRecord(text, label, DerText.Format(der, parameter));
            position = parameter.Next;
        }

        AppendRecord(text, keyLabel, DerText.Format(der, key));
    }

    // The whole certificate, as the TLS library handed it over, in 64-character lines.
    private static void AppendPem(StringBuilder text, byte[] der)
    {
        var base64 = Convert.ToBase64String(der);
        text.Append("-----BEGIN CERTIFICATE-----\n");
        for (var index = 0; index < base64.Length; index += PemLineLength)
        {
            text.Append(base64.AsSpan(index, Math.Min(PemLineLength, base64.Length - index))).Append('\n');
        }

        text.Append("-----END CERTIFICATE-----\n");
    }
}
