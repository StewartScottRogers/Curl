namespace Curl.Cryptography;

/// <summary>
/// The brainpool curves <see cref="BrainpoolEcdh" /> and <see cref="BrainpoolEcdsa" /> work
/// on: the random ("r1") curves of RFC 5639 section 3 that TLS 1.3 names
/// <c>brainpoolP256r1tls13</c>, <c>brainpoolP384r1tls13</c> and <c>brainpoolP512r1tls13</c>
/// (RFC 8734), and TLS 1.2 and X.509 name <c>brainpoolP256r1</c> to <c>brainpoolP512r1</c>
/// (RFC 7027).
/// </summary>
public enum BrainpoolCurve
{
    /// <summary>brainpoolP256r1: a 256-bit prime field, 32-byte scalars and coordinates.</summary>
    BrainpoolP256r1,

    /// <summary>brainpoolP384r1: a 384-bit prime field, 48-byte scalars and coordinates.</summary>
    BrainpoolP384r1,

    /// <summary>brainpoolP512r1: a 512-bit prime field, 64-byte scalars and coordinates.</summary>
    BrainpoolP512r1,
}
