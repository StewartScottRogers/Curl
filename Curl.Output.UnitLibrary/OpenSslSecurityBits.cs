// Ported from OpenSSL 3.5 crypto/rsa/rsa_lib.c, Copyright The OpenSSL Project Authors,
// under the Apache License 2.0.

namespace Curl.Output;

/// <summary>
/// The security strength OpenSSL 3 gives an RSA key of a modulus size, the <c>secBits</c>
/// of curl's <c>Certificate level</c> line: a port of <c>ossl_ifc_ffc_compute_security_bits</c>,
/// which evaluates the FIPS 140 IG 7.5 formula in fixed point (ADR-0085).
/// </summary>
internal static class OpenSslSecurityBits
{
    private const ulong Scale = 1 << 18;
    private const ulong CubeRootScale = 1 << (2 * 18 / 3);
    private const ulong Log2 = 0x02c5c8;
    private const ulong LogE = 0x05c551;
    private const ulong C1923 = 0x07b126;
    private const ulong C4690 = 0x12c28f;

    private static readonly Dictionary<int, int> CanonicalStrengths = new()
    {
        [2048] = 112,
        [3072] = 128,
        [4096] = 152,
        [6144] = 176,
        [7680] = 192,
        [8192] = 200,
        [15360] = 256,
    };

    /// <summary>Returns the strength of an RSA modulus.</summary>
    /// <param name="modulusBits">The modulus size in bits.</param>
    /// <returns>The strength in bits, as OpenSSL computes it.</returns>
    internal static int ForModulusBits(int modulusBits)
    {
        if (CanonicalStrengths.TryGetValue(modulusBits, out var canonical))
        {
            return canonical;
        }

        if (modulusBits >= 687737)
        {
            return 1200;
        }

        if (modulusBits < 8)
        {
            return 0;
        }

        var cap = modulusBits <= 7680 ? 192 : modulusBits <= 15360 ? 256 : 1200;
        var x = (ulong)modulusBits * Log2;
        ulong lx = LogOfE(x);
        var y = (int)(ushort)((Multiply(C1923, CubeRoot(Multiply(Multiply(x, lx), lx))) - C4690) / Log2);
        y = (y + 4) & ~7;
        return Math.Min(y, cap);
    }

    private static ulong Multiply(ulong a, ulong b) => a * b / Scale;

    // The shifting nth root algorithm, three bits at a time.
    private static ulong CubeRoot(ulong x)
    {
        ulong root = 0;
        for (var shift = 63; shift >= 0; shift -= 3)
        {
            root <<= 1;
            var b = (3 * root * (root + 1)) + 1;
            if ((x >> shift) >= b)
            {
                x -= b << shift;
                root++;
            }
        }

        return root * CubeRootScale;
    }

    // The natural logarithm of a scaled value above one: a base two logarithm, rescaled.
    private static uint LogOfE(ulong value)
    {
        uint result = 0;
        while (value >= 2 * Scale)
        {
            value >>= 1;
            result += (uint)Scale;
        }

        for (var bit = (uint)(Scale / 2); bit != 0; bit /= 2)
        {
            value = Multiply(value, value);
            if (value >= 2 * Scale)
            {
                value >>= 1;
                result += bit;
            }
        }

        return (uint)(result * Scale / LogE);
    }
}
