namespace Curl.Tls;

/// <summary>The certificate the client presents when the server asks for one, and the key that signs its CertificateVerify.</summary>
/// <param name="CertificateChain">The DER certificates, leaf first.</param>
/// <param name="SigningKey">The leaf's private key.</param>
public sealed record TlsClientCertificate(IReadOnlyList<byte[]> CertificateChain, TlsSigningKey SigningKey);
