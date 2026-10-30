using Curl.Cryptography;
using Curl.Extension.Abstractions;
using Curl.Extension.Abstractions.Certificates;

namespace Curl.Extension.MlDsaCertificates;

/// <summary>
/// The <c>--mldsa-verify</c> check (ADR-1126) on a chain curl accepted: the server's certificate must be signed with
/// ML-DSA, and every certificate signed with ML-DSA must name the next certificate the server sent as its issuer (or,
/// at the end of the chain, be self-issued), whose key is an ML-DSA key of the same parameter set (RFC 9881 section 4)
/// under which the signature verifies over the DER <c>tbsCertificate</c> with an empty context (RFC 9881 section 3,
/// FIPS 204 algorithm 3). Links signed with any other algorithm are left to curl's own verification.
/// </summary>
internal sealed class MlDsaCertificateCheckHook : ICertificateCheckHook
{
    /// <inheritdoc />
    public ExtensionOption Option => MlDsaCertificatesExtension.Option;

    /// <inheritdoc />
    public ValueTask<string?> RefusalOfAsync(IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates, string host, int? port, CancellationToken cancellationToken) =>
        ValueTask.FromResult(RefusalOf(peerCertificates));

    /// <summary>Checks a chain, the server's own certificate first.</summary>
    /// <param name="peerCertificates">The DER encoding of every certificate the server sent.</param>
    /// <returns><see langword="null" /> when the chain passes, otherwise why it does not.</returns>
    internal static string? RefusalOf(IReadOnlyList<ReadOnlyMemory<byte>> peerCertificates)
    {
        CertificateFields[] chain = new CertificateFields[peerCertificates.Count];
        for (int index = 0; index < chain.Length; index++)
        {
            if (CertificateFields.Read(peerCertificates[index]) is not { } certificate)
            {
                return $"--mldsa-verify: certificate {index} of the server's chain is not a DER X.509 certificate";
            }

            chain[index] = certificate;
        }

        if (chain.Length == 0 || MlDsaAlgorithmIdentifiers.ParameterSetOf(chain[0].SignatureAlgorithm.Oid) is null)
        {
            return "--mldsa-verify: the server's certificate is not signed with ML-DSA (RFC 9881)";
        }

        return chain.Select((_, index) => RefusalOfLink(chain, index)).FirstOrDefault(refusal => refusal is not null);
    }

    private static string? RefusalOfLink(CertificateFields[] chain, int index)
    {
        CertificateFields certificate = chain[index];
        if (MlDsaAlgorithmIdentifiers.ParameterSetOf(certificate.SignatureAlgorithm.Oid) is not { } parameterSet)
        {
            return null;
        }

        if (certificate.SignatureAlgorithm.HasParameters || certificate.SignatureAlgorithm != certificate.ToBeSignedSignatureAlgorithm)
        {
            return $"--mldsa-verify: certificate {index}'s ML-DSA signature algorithm has parameters or differs from its tbsCertificate's (RFC 9881 section 2, RFC 5280 section 4.1.1.2)";
        }

        return IssuerOf(chain, index) is { } issuer
            ? RefusalOfSignature(certificate, issuer, parameterSet, index)
            : $"--mldsa-verify: the issuer of certificate {index} is not in the server's chain, so its ML-DSA signature cannot be verified";
    }

    private static CertificateFields? IssuerOf(CertificateFields[] chain, int index)
    {
        if (index + 1 < chain.Length)
        {
            return chain[index + 1];
        }

        return chain[index].IsSelfIssued ? chain[index] : null;
    }

    private static string? RefusalOfSignature(CertificateFields certificate, CertificateFields issuer, MlDsaParameterSet parameterSet, int index)
    {
        string name = MlDsaAlgorithmIdentifiers.NameOf(parameterSet);
        if (!issuer.Subject.Span.SequenceEqual(certificate.Issuer.Span))
        {
            return $"--mldsa-verify: certificate {index}'s issuer name is not the subject of the certificate after it";
        }

        if (MlDsaAlgorithmIdentifiers.ParameterSetOf(issuer.KeyAlgorithm) != parameterSet || issuer.PublicKey.Length != MlDsa.GetPublicKeySize(parameterSet))
        {
            return $"--mldsa-verify: the issuer of certificate {index} has no {name} public key (RFC 9881 section 4)";
        }

        bool verifies = certificate.Signature.Length == MlDsa.GetSignatureSize(parameterSet)
            && MlDsa.VerifyData(parameterSet, issuer.PublicKey.Span, certificate.ToBeSigned.Span, [], certificate.Signature.Span);
        return verifies ? null : $"--mldsa-verify: the {name} signature of certificate {index} does not verify (FIPS 204 algorithm 3)";
    }
}
