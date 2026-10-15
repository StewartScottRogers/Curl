using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Curl.Tls;

namespace Curl.Conformance.Http3Server;

/// <summary>
/// What the HTTP/3 stand-in's TLS 1.3 server handshake (<see cref="Http3ServerTlsHandshake" />)
/// presents and accepts: its certificate chain and signing key, the <c>quic_transport_parameters</c>
/// bytes it sends, and its preference order of cipher suites, groups and signature schemes.
/// </summary>
public sealed record Http3ServerTlsSettings
{
    /// <summary>The ALPN protocol the stand-in serves, and the only one it accepts.</summary>
    public const string ApplicationProtocol = "h3";

    /// <summary>Gets the DER certificates sent in the Certificate message, the server's own first.</summary>
    public required IReadOnlyList<byte[]> CertificateChain { get; init; }

    /// <summary>Gets the key that signs the CertificateVerify; it must match the first certificate.</summary>
    public required TlsSigningKey SigningKey { get; init; }

    /// <summary>Gets the encoded QUIC transport parameters sent in EncryptedExtensions (RFC 9001 section 8.2).</summary>
    public required byte[] QuicTransportParameters { get; init; }

    /// <summary>Gets the TLS 1.3 cipher suites in preference order: TLS_AES_128_GCM_SHA256 first, then the client's other QUIC suites.</summary>
    public IReadOnlyList<ushort> CipherSuites { get; init; } =
        [Tls13CipherSuite.Aes128GcmSha256.Code, Tls13CipherSuite.Aes256GcmSha384.Code, Tls13CipherSuite.ChaCha20Poly1305Sha256.Code];

    /// <summary>Gets the key exchange groups in preference order: X25519, then secp256r1.</summary>
    public IReadOnlyList<ushort> Groups { get; init; } = [TlsNamedGroup.X25519, TlsNamedGroup.Secp256r1];

    /// <summary>Gets the CertificateVerify signature schemes in preference order, rsa_pss_rsae_sha256 first.</summary>
    public IReadOnlyList<ushort> SignatureSchemes { get; init; } =
    [
        TlsSignatureScheme.RsaPssRsaeSha256,
        TlsSignatureScheme.EcdsaSecp256r1Sha256,
        TlsSignatureScheme.RsaPssRsaeSha384,
        TlsSignatureScheme.RsaPssRsaeSha512,
    ];

    /// <summary>
    /// Returns settings that present <paramref name="certificate" /> - upstream's
    /// <c>test-localhost.pem</c>, as the other TLS stand-ins load it - and sign with its RSA key.
    /// </summary>
    /// <param name="certificate">The certificate, with its RSA private key.</param>
    /// <param name="quicTransportParameters">The encoded QUIC transport parameters to send.</param>
    /// <returns>The settings.</returns>
    /// <exception cref="ArgumentException">The certificate has no RSA private key.</exception>
    public static Http3ServerTlsSettings FromRsaCertificate(X509Certificate2 certificate, byte[] quicTransportParameters)
    {
        ArgumentNullException.ThrowIfNull(certificate);
        ArgumentNullException.ThrowIfNull(quicTransportParameters);
        RSA key = certificate.GetRSAPrivateKey()
            ?? throw new ArgumentException("The certificate has no RSA private key.", nameof(certificate));
        return new()
        {
            CertificateChain = [certificate.RawData],
            SigningKey = new RsaTlsSigningKey(key),
            QuicTransportParameters = quicTransportParameters,
        };
    }
}
