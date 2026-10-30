using System.Security.Cryptography;
using System.Text.Json;

namespace Curl.Extension.Dpop;

/// <summary>
/// The private key a DPoP proof is signed with (RFC 9449 §4.2): its JWS <c>alg</c>, its public key as a JWK (RFC 7517)
/// for the proof's <c>jwk</c> header, and the signature over a JWS signing input (RFC 7515 §5.1).
/// </summary>
internal abstract class DpopKey
{
    /// <summary>Gets the JWS algorithm the key signs with, such as <c>ES256</c> (RFC 7518 §3.1).</summary>
    public abstract string Algorithm { get; }

    /// <summary>Writes the public key as a JWK object, its members in RFC 7638 §3.2's lexicographic order and no private member.</summary>
    /// <param name="writer">Where the object goes.</param>
    public abstract void WriteJwk(Utf8JsonWriter writer);

    /// <summary>Signs a JWS signing input.</summary>
    /// <param name="signingInput">The ASCII of <c>BASE64URL(header) || '.' || BASE64URL(payload)</c>.</param>
    /// <returns>The JWS signature bytes (RFC 7518 §3.3 for RSA, §3.4 for ECDSA).</returns>
    public abstract byte[] Sign(byte[] signingInput);

    /// <summary>Reads the first EC (P-256, P-384, P-521) or RSA private key in PEM text (RFC 7468).</summary>
    /// <param name="pem">The PEM text: <c>EC PRIVATE KEY</c>, <c>RSA PRIVATE KEY</c> or <c>PRIVATE KEY</c>.</param>
    /// <returns>The key.</returns>
    /// <exception cref="ArgumentException">The text holds no key .NET reads.</exception>
    /// <exception cref="CryptographicException">The key is public only, on an unsupported curve, or malformed.</exception>
    public static DpopKey FromPem(string pem)
    {
        var ecdsa = ECDsa.Create();
        try
        {
            ecdsa.ImportFromPem(pem);
            return new EcDpopKey(ecdsa);
        }
        catch (ArgumentException)
        {
            ecdsa.Dispose();
        }
        catch (CryptographicException)
        {
            ecdsa.Dispose();
        }

        var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pem);
            return new RsaDpopKey(rsa);
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }
}
