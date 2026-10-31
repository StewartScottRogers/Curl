using Curl.Extension.Dane.Wire;

namespace Curl.Extension.Dane.Dnssec;

/// <summary>
/// A DNSKEY's key tag (RFC 4034 appendix B), the 16-bit checksum RRSIG and DS records name a key by, and the
/// DNSKEY RDATA it is computed over.
/// </summary>
public static class DnskeyKeyTag
{
    /// <summary>
    /// Computes <paramref name="key" />'s key tag with appendix B's general method. Algorithm 1 (RSA/MD5), which
    /// appendix B.1 tags differently, is not supported (RFC 8624 section 3.1), so its tag is never needed.
    /// </summary>
    /// <param name="key">The DNSKEY's fields.</param>
    /// <returns>The key tag.</returns>
    public static ushort Compute(DnskeyRecordData key)
    {
        ArgumentNullException.ThrowIfNull(key);
        byte[] rdata = EncodeRdata(key);
        uint sum = 0;
        for (int index = 0; index < rdata.Length; index++)
        {
            sum += (index & 1) == 0 ? (uint)rdata[index] << 8 : rdata[index];
        }

        sum += (sum >> 16) & 0xFFFF;
        return (ushort)sum;
    }

    /// <summary>Encodes <paramref name="key" /> as DNSKEY RDATA (RFC 4034 section 2.1).</summary>
    /// <param name="key">The DNSKEY's fields.</param>
    /// <returns>Flags, protocol, algorithm and public key.</returns>
    internal static byte[] EncodeRdata(DnskeyRecordData key) =>
        [(byte)(key.Flags >> 8), (byte)key.Flags, key.Protocol, key.Algorithm, .. key.PublicKey];
}
