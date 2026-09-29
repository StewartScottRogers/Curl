using System.Security.Cryptography;
using Curl.Cryptography;

namespace Curl.Protocol.Ssh.KeyExchange;

/// <summary>
/// Where the client's ephemeral key-exchange keys come from. Injected so a test can fix
/// them and so pin the exchange hash and the keys derived from it.
/// </summary>
internal interface ISshEphemeralKeySource
{
    /// <summary>
    /// Creates a key pair on a NIST curve for <c>ecdh-sha2-*</c> (RFC 5656).
    /// </summary>
    /// <param name="curve">The curve.</param>
    /// <returns>The key pair, which the caller disposes.</returns>
    ECDiffieHellman CreateEllipticCurveKey(ECCurve curve);

    /// <summary>
    /// Creates a key pair in a finite-field group for <c>diffie-hellman-*</c> (RFC 4253,
    /// RFC 4419, RFC 8268).
    /// </summary>
    /// <param name="group">The group.</param>
    /// <returns>The key pair, which the caller disposes.</returns>
    FiniteFieldDiffieHellman CreateFiniteFieldKey(FiniteFieldDiffieHellmanGroup group);
}
