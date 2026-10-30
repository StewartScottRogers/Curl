using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Curl.Extension.Sigstore;

/// <summary>
/// Verifies an artifact against a Sigstore bundle's message signature in the order <c>cosign verify-blob</c> does
/// (Sigstore client specification, "Verification"): the message digest, the Fulcio certificate's chain to a trusted
/// root at the moment the transparency log recorded the signature with the code-signing extended key usage, the
/// ECDSA P-256 SHA-256 signature over the artifact by the certificate's key, and the identity and issuer the
/// certificate names.
/// </summary>
internal static class SigstoreVerification
{
    private const string CodeSigningOid = "1.3.6.1.5.5.7.3.3";

    /// <summary>Verifies <paramref name="artifact" />.</summary>
    /// <param name="artifact">The downloaded bytes.</param>
    /// <param name="bundle">The bundle.</param>
    /// <param name="roots">The trusted Fulcio CA certificates.</param>
    /// <param name="identity">The identity the certificate must name.</param>
    /// <param name="issuer">The OIDC issuer the certificate must name, or <see langword="null" /> for any.</param>
    /// <returns>Why the artifact fails, or <see langword="null" /> when it verifies.</returns>
    public static string? FailureOf(ReadOnlySpan<byte> artifact, SigstoreBundle bundle, X509Certificate2Collection roots, string identity, string? issuer)
    {
        if (!bundle.DigestMatches(artifact))
        {
            return "the download's SHA-256 is not the bundle's message digest";
        }

        using var certificate = LoadCertificate(bundle.Certificate);
        if (certificate is null || !ChainsToRoot(certificate, roots, bundle.IntegratedTime))
        {
            return "the bundle's certificate does not chain to a trusted Fulcio root for code signing when it was logged";
        }

        return SignatureVerifies(certificate, artifact, bundle.Signature)
            ? IdentityFailureOf(certificate, identity, issuer)
            : "the bundle's signature does not verify with its certificate's key";
    }

    private static string? IdentityFailureOf(X509Certificate2 certificate, string identity, string? issuer)
    {
        if (!SigningCertificateIdentity.NamesOf(certificate).Contains(identity, StringComparer.Ordinal))
        {
            return $"the bundle's certificate does not name the identity {identity}";
        }

        return issuer is null || SigningCertificateIdentity.IssuerOf(certificate) == issuer
            ? null
            : $"the bundle's certificate does not name the issuer {issuer}";
    }

    private static X509Certificate2? LoadCertificate(byte[] der)
    {
        try
        {
            return X509CertificateLoader.LoadCertificate(der);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    private static bool ChainsToRoot(X509Certificate2 certificate, X509Certificate2Collection roots, DateTimeOffset integratedTime)
    {
        using X509Chain chain = new();
        chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        chain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        chain.ChainPolicy.VerificationTime = integratedTime.UtcDateTime;
        chain.ChainPolicy.ApplicationPolicy.Add(new Oid(CodeSigningOid));
        foreach (var root in roots)
        {
            (root.SubjectName.RawData.AsSpan().SequenceEqual(root.IssuerName.RawData) ? chain.ChainPolicy.CustomTrustStore : chain.ChainPolicy.ExtraStore).Add(root);
        }

        return chain.Build(certificate);
    }

    private static bool SignatureVerifies(X509Certificate2 certificate, ReadOnlySpan<byte> artifact, byte[] signature)
    {
        using var key = certificate.GetECDsaPublicKey();
        return key is not null && key.VerifyData(artifact, signature, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
    }
}
