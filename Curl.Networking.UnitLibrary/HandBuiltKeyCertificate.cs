using System.Security.Cryptography.X509Certificates;

using Curl.Tls;

namespace Curl.Networking;

/// <summary>
/// A <c>--cert</c> certificate whose private key the BCL cannot hold - Ed25519, Ed448 or
/// ML-DSA - carried beside it as the hand-built TLS client's signing key (ADR-0301). The
/// certificate itself has no private key, so only the hand-built client can present it.
/// </summary>
/// <param name="certificate">The certificate, copied; the caller still disposes it.</param>
/// <param name="signingKey">The signing key for the certificate's public key.</param>
internal sealed class HandBuiltKeyCertificate(X509Certificate2 certificate, TlsSigningKey signingKey)
    : X509Certificate2(certificate)
{
    /// <summary>The signing key for the certificate's public key.</summary>
    public TlsSigningKey SigningKey { get; } = signingKey;
}
