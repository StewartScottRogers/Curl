using System.Text;

namespace Curl.Conformance.Extended.DnsQuery.Reference;

/// <summary>Writes a domain name as RFC 1035 section 3.1 lays it out: each label as a length octet and its octets, then the zero-length root label.</summary>
public static class DnsReferenceName
{
    /// <summary>Writes <paramref name="name" /> uncompressed.</summary>
    /// <param name="name">The name, with or without its final dot; <c>.</c> is the root.</param>
    /// <returns>The name's octets.</returns>
    public static byte[] Encode(string name) =>
        [.. name.Split('.', StringSplitOptions.RemoveEmptyEntries).SelectMany(label => (byte[])[(byte)label.Length, .. Encoding.ASCII.GetBytes(label)]), 0];
}
