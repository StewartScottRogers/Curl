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

    /// <summary>
    /// Fills <paramref name="privateKey" /> with an X25519 private key for
    /// <c>curve25519-sha256</c> (RFC 8731); the caller zeroes it when done.
    /// </summary>
    /// <param name="privateKey">The <see cref="X25519.KeySize" /> bytes to fill.</param>
    void CreateX25519PrivateKey(Span<byte> privateKey);

    /// <summary>
    /// Creates an ML-KEM key pair (FIPS 203) for the <c>mlkem*</c> hybrid methods.
    /// </summary>
    /// <param name="parameterSet">ML-KEM-768 or ML-KEM-1024.</param>
    /// <returns>The key pair, which the caller disposes.</returns>
    MlKem CreateMlKemKey(MlKemParameterSet parameterSet);

    /// <summary>
    /// Fills an sntrup761 key pair for the <c>sntrup761x25519-sha512</c> hybrid method; the
    /// caller zeroes the secret key when done.
    /// </summary>
    /// <param name="publicKey">The <see cref="Sntrup761.PublicKeySize" /> bytes to fill.</param>
    /// <param name="secretKey">The <see cref="Sntrup761.SecretKeySize" /> bytes to fill.</param>
    void CreateSntrup761KeyPair(Span<byte> publicKey, Span<byte> secretKey);
}
