using System.Buffers.Binary;

namespace Curl.Extended.Protocol.DnsQuery.Wire;

/// <summary>
/// Encodes a query of one question for any QTYPE and QCLASS (RFC 1035 sections 4.1.1 and 4.1.2), reading the name
/// in presentation format: labels split on dots, <c>\.</c> for a literal character and <c>\DDD</c> for any octet.
/// </summary>
internal static class DnsQueryEncoder
{
    private const int HeaderLength = 12;
    private const int MaximumLabelLength = 63;
    private const int MaximumNameLength = 255;

    /// <summary>Encodes a query.</summary>
    /// <param name="id">The message identifier.</param>
    /// <param name="name">The QNAME, with or without its final dot; <c>.</c> for the root.</param>
    /// <param name="type">The QTYPE.</param>
    /// <param name="recordClass">The QCLASS.</param>
    /// <param name="recursionDesired">Whether to set the RD bit.</param>
    /// <returns>The query's octets, or why the name cannot be encoded.</returns>
    public static DnsOutcome<byte[]> Encode(ushort id, string name, ushort type, ushort recordClass, bool recursionDesired)
    {
        try
        {
            return DnsOutcome<byte[]>.Succeeded(Build(id, EncodeName(name), type, recordClass, recursionDesired));
        }
        catch (FormatException exception)
        {
            return DnsOutcome<byte[]>.Failed(exception.Message);
        }
    }

    private static byte[] Build(ushort id, List<byte> name, ushort type, ushort recordClass, bool recursionDesired)
    {
        byte[] query = new byte[HeaderLength + name.Count + 4];
        Span<byte> span = query;
        BinaryPrimitives.WriteUInt16BigEndian(span, id);
        BinaryPrimitives.WriteUInt16BigEndian(span[2..], recursionDesired ? DnsHeader.RecursionDesiredFlag : (ushort)0);
        BinaryPrimitives.WriteUInt16BigEndian(span[4..], 1);
        name.CopyTo(query, HeaderLength);
        BinaryPrimitives.WriteUInt16BigEndian(span[(HeaderLength + name.Count)..], type);
        BinaryPrimitives.WriteUInt16BigEndian(span[(HeaderLength + name.Count + 2)..], recordClass);
        return query;
    }

    private static List<byte> EncodeName(string name)
    {
        List<byte> wire = [];
        List<byte> label = [];
        int index = name == "." ? 1 : 0;
        while (index < name.Length)
        {
            char character = name[index];
            if (character == '.')
            {
                EndLabel(wire, label);
                index++;
            }
            else
            {
                index = ReadOctet(name, index, label);
            }
        }

        if (label.Count > 0)
        {
            EndLabel(wire, label);
        }

        wire.Add(0);
        return wire.Count > MaximumNameLength ? throw new FormatException("The DNS name is longer than 255 octets.") : wire;
    }

    private static void EndLabel(List<byte> wire, List<byte> label)
    {
        if (label.Count == 0)
        {
            throw new FormatException("The DNS name has an empty label.");
        }

        if (label.Count > MaximumLabelLength)
        {
            throw new FormatException("The DNS name has a label longer than 63 octets.");
        }

        wire.Add((byte)label.Count);
        wire.AddRange(label);
        label.Clear();
    }

    private static int ReadOctet(string name, int index, List<byte> label)
    {
        char character = name[index];
        if (character != '\\')
        {
            label.Add(AsciiOctet(character));
            return index + 1;
        }

        if (index + 3 < name.Length && IsDecimalEscape(name.AsSpan(index + 1, 3), out byte octet))
        {
            label.Add(octet);
            return index + 4;
        }

        if (index + 1 == name.Length)
        {
            throw new FormatException("The DNS name ends in a backslash.");
        }

        label.Add(AsciiOctet(name[index + 1]));
        return index + 2;
    }

    private static bool IsDecimalEscape(ReadOnlySpan<char> digits, out byte octet) =>
        byte.TryParse(digits, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out octet);

    private static byte AsciiOctet(char character) =>
        char.IsAscii(character) ? (byte)character : throw new FormatException("The DNS name holds a character outside ASCII.");
}
