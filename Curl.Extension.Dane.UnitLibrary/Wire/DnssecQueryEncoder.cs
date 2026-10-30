using System.Buffers.Binary;
using System.Text;

namespace Curl.Extension.Dane.Wire;

/// <summary>
/// Writes a DNSSEC-aware DNS query for one name and record type: a header (RFC 1035 section 4.1.1) with recursion
/// desired, optionally CD (RFC 4035 section 3.2.2), one question and one additional record; the question (section
/// 4.1.2) of the name's labels, the QTYPE and QCLASS IN; then an EDNS0 OPT record (RFC 6891 section 6.1.2) with the
/// root as its name, a UDP payload size of 1232 octets, extended RCODE and version 0, the DO bit set (RFC 4035
/// section 3.2.1) and no options.
/// </summary>
public static class DnssecQueryEncoder
{
    /// <summary>The UDP payload size the OPT record advertises: the DNS flag day 2020 default.</summary>
    public const ushort UdpPayloadSize = 1232;

    private const int HeaderLength = 12;
    private const int OptRecordLength = 11;
    private const ushort RecursionDesiredFlag = 0x0100;
    private const ushort CheckingDisabledFlag = 0x0010;
    private const ushort InternetClass = 1;
    private const ushort DnssecOkFlag = 0x8000;
    private const int MaximumLabelLength = 63;
    private const int MaximumNameLength = 255;

    /// <summary>
    /// Encodes the query.
    /// </summary>
    /// <param name="id">The message ID the response carries back (RFC 1035 section 4.1.1).</param>
    /// <param name="name">The name asked about, with or without one trailing dot; "." is the root.</param>
    /// <param name="type">The QTYPE.</param>
    /// <param name="checkingDisabled">Whether to set CD, asking the resolver to return data it could not validate.</param>
    /// <returns>The query's bytes.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="name" /> has an empty label or a label over 63 octets, or is over 255 octets as a name
    /// (RFC 1035 section 2.3.4).
    /// </exception>
    public static byte[] Encode(ushort id, string name, DnssecRecordType type, bool checkingDisabled)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);

        byte[] labels = EncodeName(name.EndsWith('.') ? name[..^1] : name);
        byte[] query = new byte[HeaderLength + labels.Length + 4 + OptRecordLength];
        Span<byte> span = query;
        BinaryPrimitives.WriteUInt16BigEndian(span, id);
        BinaryPrimitives.WriteUInt16BigEndian(span[2..], checkingDisabled ? (ushort)(RecursionDesiredFlag | CheckingDisabledFlag) : RecursionDesiredFlag);
        BinaryPrimitives.WriteUInt16BigEndian(span[4..], 1);
        BinaryPrimitives.WriteUInt16BigEndian(span[10..], 1);
        labels.CopyTo(span[HeaderLength..]);
        Span<byte> question = span[(HeaderLength + labels.Length)..];
        BinaryPrimitives.WriteUInt16BigEndian(question, (ushort)type);
        BinaryPrimitives.WriteUInt16BigEndian(question[2..], InternetClass);
        WriteOptRecord(question[4..]);
        return query;
    }

    // RFC 6891 section 6.1.2: root NAME, TYPE 41, CLASS the payload size, TTL the extended RCODE, version and flags
    // (DO first, RFC 4035 section 3.2.1), RDLENGTH 0. The array starts zeroed, so the root and the zero fields are there.
    private static void WriteOptRecord(Span<byte> record)
    {
        BinaryPrimitives.WriteUInt16BigEndian(record[1..], (ushort)DnssecRecordType.Opt);
        BinaryPrimitives.WriteUInt16BigEndian(record[3..], UdpPayloadSize);
        BinaryPrimitives.WriteUInt16BigEndian(record[7..], DnssecOkFlag);
    }

    // The name as length-prefixed labels ending in the root's zero octet (RFC 1035 section 3.1).
    private static byte[] EncodeName(string name)
    {
        List<byte> wire = [];
        foreach (string label in name.Length == 0 ? [] : name.Split('.'))
        {
            if (!TryAppendLabel(wire, label))
            {
                throw new ArgumentException($"The label '{label}' is not 1 to {MaximumLabelLength} octets long.", nameof(name));
            }
        }

        wire.Add(0);
        return wire.Count > MaximumNameLength
            ? throw new ArgumentException($"The name is over {MaximumNameLength} octets long.", nameof(name))
            : [.. wire];
    }

    // Appends one label with its length octet, or returns false when it is empty or over 63 octets.
    private static bool TryAppendLabel(List<byte> wire, string label)
    {
        byte[] octets = Encoding.ASCII.GetBytes(label);
        if (octets.Length is 0 or > MaximumLabelLength)
        {
            return false;
        }

        wire.Add((byte)octets.Length);
        wire.AddRange(octets);
        return true;
    }
}
