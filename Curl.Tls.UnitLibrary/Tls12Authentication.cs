namespace Curl.Tls;

/// <summary>How a TLS 1.2 and below suite authenticates the server.</summary>
public enum Tls12Authentication
{
    /// <summary>An RSA certificate: it signs the ServerKeyExchange, or decrypts the RSA pre-master secret.</summary>
    Rsa,

    /// <summary>An ECDSA (or Ed25519, RFC 8422) certificate that signs the ServerKeyExchange.</summary>
    Ecdsa,

    /// <summary>
    /// No certificate: the <c>_anon_</c> suites and the plain <c>SRP_SHA</c> suites (where the
    /// password authenticates both sides) send no Certificate and an unsigned ServerKeyExchange.
    /// </summary>
    Anonymous,

    /// <summary>A DSA certificate that signs the ServerKeyExchange: the <c>DHE_DSS</c> suites.</summary>
    Dss,
}
