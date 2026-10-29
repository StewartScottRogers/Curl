using System.Buffers.Binary;
using System.Text;

namespace Curl.Extension.EncryptedDns.Wire;

/// <summary>
/// Writes a DNS query message for one host name and record type, as RFC 1035 section 4.1 lays it out: a header
/// (section 4.1.1) with recursion desired and one question, then the question (section 4.1.2) of the name's labels,
/// the QTYPE and QCLASS IN.
/// </summary>
public static class DnsQueryEncoder
{
    private const int HeaderLength = 12;
    private const ushort RecursionDesiredFlags = 0x0100;
    private const ushort InternetClass = 1;
    private const int MaximumLabelLength = 63;
    private const int MaximumNameLength = 255;

    /// <summary>
    /// Encodes the query.
    /// </summary>
    /// <param name="id">The message ID the answer carries back (RFC 1035 section 4.1.1).</param>
    /// <param name="host">The host name, with or without one trailing dot.</param>
    /// <param name="type">The record type asked for.</param>
    /// <returns>The query's bytes.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="host" /> has an empty label or a label over 63 octets, or is over 255 octets as a name
    /// (RFC 1035 section 2.3.4).
    /// </exception>
    public static byte[] Encode(ushort id, string host, DnsRecordType type)
    {
        ArgumentException.ThrowIfNullOrEmpty(host);

        byte[] name = EncodeName(host.EndsWith('.') ? host[..^1] : host);
        byte[] query = new byte[HeaderLength + name.Length + 4];
        BinaryPrimitives.WriteUInt16BigEndian(query, id);
        BinaryPrimitives.WriteUInt16BigEndian(query.AsSpan(2), RecursionDesiredFlags);
        BinaryPrimitives.WriteUInt16BigEndian(query.AsSpan(4), 1);
        name.CopyTo(query, HeaderLength);
        BinaryPrimitives.WriteUInt16BigEndian(query.AsSpan(HeaderLength + name.Length), (ushort)type);
        BinaryPrimitives.WriteUInt16BigEndian(query.AsSpan(HeaderLength + name.Length + 2), InternetClass);
        return query;
    }

    // The name as length-prefixed labels ending in the root's zero octet (RFC 1035 section 3.1).
    private static byte[] EncodeName(string host)
    {
        List<byte> name = [];
        foreach (string label in host.Split('.'))
        {
            byte[] octets = Encoding.ASCII.GetBytes(label);
            if (octets.Length is 0 or > MaximumLabelLength)
            {
                throw new ArgumentException($"The label '{label}' is not 1 to {MaximumLabelLength} octets long.", nameof(host));
            }

            name.Add((byte)octets.Length);
            name.AddRange(octets);
        }

        name.Add(0);
        return name.Count > MaximumNameLength
            ? throw new ArgumentException($"The name is over {MaximumNameLength} octets long.", nameof(host))
            : [.. name];
    }
}
