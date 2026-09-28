namespace Curl.Tls;

/// <summary>One entry of a Certificate message's list (RFC 8446 section 4.4.2).</summary>
/// <param name="CertificateData">The DER-encoded X.509 certificate (or raw public key).</param>
/// <param name="Extensions">The entry's extensions, such as a stapled OCSP response.</param>
public sealed record CertificateEntry(byte[] CertificateData, IReadOnlyList<TlsExtension> Extensions);
