// Ported from curl 8.21.0 lib/vtls/x509asn1.c, Copyright (C) Daniel Stenberg,
// <daniel@haxx.se>, et al., under the curl licence (SPDX-License-Identifier: curl).

namespace Curl.Output;

/// <summary>
/// The parts of an X.509 certificate that curl 8.21.0 prints for <c>%{certs}</c>, found as
/// its <c>Curl_parseX509</c> in <c>lib/vtls/x509asn1.c</c> finds them (ADR-0054).
/// </summary>
/// <param name="version">The version <c>INTEGER</c>; <see langword="null" /> when absent, which means version 1.</param>
/// <param name="serialNumber">The serial number.</param>
/// <param name="signatureAlgorithm">The <c>AlgorithmIdentifier</c> inside the to-be-signed part.</param>
/// <param name="issuer">The issuer <c>Name</c>.</param>
/// <param name="notBefore">The start of the validity period.</param>
/// <param name="notAfter">The end of the validity period.</param>
/// <param name="subject">The subject <c>Name</c>.</param>
/// <param name="publicKeyAlgorithm">The <c>AlgorithmIdentifier</c> of the subject public key.</param>
/// <param name="publicKey">The subject public key <c>BIT STRING</c>.</param>
/// <param name="signature">The signature <c>BIT STRING</c> after the to-be-signed part.</param>
internal sealed class X509CertificateFields(
    DerElement? version,
    DerElement serialNumber,
    DerElement signatureAlgorithm,
    DerElement issuer,
    DerElement notBefore,
    DerElement notAfter,
    DerElement subject,
    DerElement publicKeyAlgorithm,
    DerElement publicKey,
    DerElement signature)
{
    /// <summary>Gets the version <c>INTEGER</c>; <see langword="null" /> when absent, which means version 1.</summary>
    internal DerElement? Version { get; } = version;

    /// <summary>Gets the serial number.</summary>
    internal DerElement SerialNumber { get; } = serialNumber;

    /// <summary>Gets the <c>AlgorithmIdentifier</c> inside the to-be-signed part.</summary>
    internal DerElement SignatureAlgorithm { get; } = signatureAlgorithm;

    /// <summary>Gets the issuer <c>Name</c>.</summary>
    internal DerElement Issuer { get; } = issuer;

    /// <summary>Gets the start of the validity period.</summary>
    internal DerElement NotBefore { get; } = notBefore;

    /// <summary>Gets the end of the validity period.</summary>
    internal DerElement NotAfter { get; } = notAfter;

    /// <summary>Gets the subject <c>Name</c>.</summary>
    internal DerElement Subject { get; } = subject;

    /// <summary>Gets the <c>AlgorithmIdentifier</c> of the subject public key.</summary>
    internal DerElement PublicKeyAlgorithm { get; } = publicKeyAlgorithm;

    /// <summary>Gets the subject public key <c>BIT STRING</c>.</summary>
    internal DerElement PublicKey { get; } = publicKey;

    /// <summary>Gets the signature <c>BIT STRING</c> after the to-be-signed part.</summary>
    internal DerElement Signature { get; } = signature;

    private const int VersionTag = 0;
    private const int IssuerUniqueIdTag = 1;
    private const int SubjectUniqueIdTag = 2;
    private const int ExtensionsTag = 3;

    /// <summary>Finds the fields, checking no more than curl checks.</summary>
    /// <param name="der">The certificate.</param>
    /// <returns>The fields.</returns>
    /// <exception cref="FormatException">curl could not find them.</exception>
    internal static X509CertificateFields Parse(byte[] der)
    {
        var certificate = DerReader.Read(der, 0, der.Length);
        var toBeSigned = DerReader.Read(der, certificate.Start, certificate.End);
        var outerAlgorithm = DerReader.Read(der, toBeSigned.Next, certificate.End);
        var signature = DerReader.Read(der, outerAlgorithm.Next, certificate.End);

        var element = DerReader.Read(der, toBeSigned.Start, toBeSigned.End);
        DerElement? version = null;
        if (element.Tag == VersionTag)
        {
            version = DerReader.Read(der, element.Start, element.End);
            element = DerReader.Read(der, element.Next, toBeSigned.End);
        }

        var serialNumber = element;
        var signatureAlgorithm = DerReader.Read(der, serialNumber.Next, toBeSigned.End);
        var issuer = DerReader.Read(der, signatureAlgorithm.Next, toBeSigned.End);
        var validity = DerReader.Read(der, issuer.Next, toBeSigned.End);
        var notBefore = DerReader.Read(der, validity.Start, validity.End);
        var notAfter = DerReader.Read(der, notBefore.Next, validity.End);
        var subject = DerReader.Read(der, validity.Next, toBeSigned.End);
        var publicKeyInfo = DerReader.Read(der, subject.Next, toBeSigned.End);
        var publicKeyAlgorithm = DerReader.Read(der, publicKeyInfo.Start, publicKeyInfo.End);
        var publicKey = DerReader.Read(der, publicKeyAlgorithm.Next, publicKeyInfo.End);
        CheckOptionalFields(der, publicKeyInfo.Next, toBeSigned.End);

        return new X509CertificateFields(
            version,
            serialNumber,
            signatureAlgorithm,
            issuer,
            notBefore,
            notAfter,
            subject,
            publicKeyAlgorithm,
            publicKey,
            signature);
    }

    // The unique identifiers and extensions are not printed, but curl reads each one that
    // is there, and refuses a certificate whose extensions wrapper holds no readable element.
    private static void CheckOptionalFields(byte[] der, int start, int end)
    {
        var position = start;
        var element = default(DerElement);
        ReadNextWhen(position < end);
        ReadNextWhen(element.Tag == IssuerUniqueIdTag && position < end);
        ReadNextWhen(element.Tag == SubjectUniqueIdTag && position < end);
        if (element.Tag == ExtensionsTag)
        {
            DerReader.Read(der, element.Start, element.End);
        }

        void ReadNextWhen(bool condition)
        {
            if (condition)
            {
                element = DerReader.Read(der, position, end);
                position = element.Next;
            }
        }
    }
}
