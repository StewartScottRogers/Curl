namespace Curl.Tls;

/// <summary>How a TLS 1.2 and below suite agrees the pre-master secret (RFC 5246 section 7.4.7).</summary>
public enum Tls12KeyExchange
{
    /// <summary>The client encrypts a random pre-master secret to the server's RSA key; no ServerKeyExchange.</summary>
    Rsa,

    /// <summary>Finite-field Diffie-Hellman with the prime and generator the server sends in its ServerKeyExchange.</summary>
    Dhe,

    /// <summary>Elliptic-curve Diffie-Hellman on the named group the server sends in its ServerKeyExchange (RFC 8422).</summary>
    Ecdhe,

    /// <summary>
    /// SRP-6a (RFC 5054): the server sends N, g, the salt and B in its ServerKeyExchange,
    /// the client its A, and the premaster secret derives from the password.
    /// </summary>
    Srp,
}
