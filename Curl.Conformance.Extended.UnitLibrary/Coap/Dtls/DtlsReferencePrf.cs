using System.Security.Cryptography;
using System.Text;

namespace Curl.Conformance.Extended.Coap.Dtls;

/// <summary>TLS 1.2's pseudorandom function with SHA-256 (RFC 5246 section 5), which DTLS 1.2 keeps unchanged.</summary>
public static class DtlsReferencePrf
{
    /// <summary>Computes PRF(secret, label, seed) = P_SHA256(secret, label + seed) to <paramref name="length" /> bytes.</summary>
    /// <param name="secret">The secret.</param>
    /// <param name="label">The ASCII label, such as "master secret".</param>
    /// <param name="seed">The seed.</param>
    /// <param name="length">How many bytes to give.</param>
    /// <returns>The first <paramref name="length" /> bytes of P_SHA256.</returns>
    public static byte[] Compute(byte[] secret, string label, byte[] seed, int length)
    {
        byte[] labelAndSeed = [.. Encoding.ASCII.GetBytes(label), .. seed];
        List<byte> output = [];
        byte[] a = labelAndSeed;
        while (output.Count < length)
        {
            a = HMACSHA256.HashData(secret, a);
            byte[] block = [.. a, .. labelAndSeed];
            output.AddRange(HMACSHA256.HashData(secret, block));
        }

        return [.. output.Take(length)];
    }
}
