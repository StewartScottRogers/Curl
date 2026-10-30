using System.Buffers.Binary;
using System.Globalization;
using System.Net;
using System.Text;

namespace Curl.Extension.WireGuard.Tunnel;

/// <summary>
/// The DNS messages a name lookup through the tunnel sends and reads (RFC 1035 section 4.1), each framed by the
/// two-byte length prefix of DNS over TCP (RFC 1035 section 4.2.2, RFC 7766).
/// </summary>
internal static class WireGuardDnsMessage
{
    /// <summary>The DNS server's TCP port.</summary>
    public const int Port = 53;

    /// <summary>The type of an IPv4 host address record.</summary>
    public const ushort AddressType = 1;

    /// <summary>The type of an IPv6 host address record (RFC 3596).</summary>
    public const ushort Ipv6AddressType = 28;

    private const int HeaderLength = 12;

    private const ushort InternetClass = 1;

    private const ushort RecursionDesired = 0x0100;

    private const ushort ResponseFlag = 0x8000;

    private const ushort ResponseCodeMask = 0x000F;

    private const int CompressionPointer = 0xC0;

    /// <summary>Builds a recursive query for <paramref name="name" />'s records of <paramref name="type" />, with its TCP length prefix.</summary>
    /// <param name="id">The query's ID, which the answer repeats.</param>
    /// <param name="name">The host name; an international name is sent as its A-label (RFC 5891).</param>
    /// <param name="type">The record type asked for.</param>
    /// <returns>The length prefix and the message.</returns>
    /// <exception cref="ArgumentException"><paramref name="name" /> is not a valid host name.</exception>
    public static byte[] EncodeQuery(ushort id, string name, ushort type)
    {
        string[] labels = new IdnMapping().GetAscii(name.TrimEnd('.')).Split('.');
        int nameLength = labels.Sum(label => label.Length + 1) + 1;
        byte[] framed = new byte[2 + HeaderLength + nameLength + 4];
        Span<byte> message = framed.AsSpan(2);
        BinaryPrimitives.WriteUInt16BigEndian(framed, (ushort)message.Length);
        BinaryPrimitives.WriteUInt16BigEndian(message, id);
        BinaryPrimitives.WriteUInt16BigEndian(message[2..], RecursionDesired);
        BinaryPrimitives.WriteUInt16BigEndian(message[4..], 1);
        int offset = HeaderLength;
        foreach (string label in labels)
        {
            message[offset++] = (byte)label.Length;
            offset += Encoding.ASCII.GetBytes(label, message[offset..]);
        }

        offset++;
        BinaryPrimitives.WriteUInt16BigEndian(message[offset..], type);
        BinaryPrimitives.WriteUInt16BigEndian(message[(offset + 2)..], InternetClass);
        return framed;
    }

    /// <summary>
    /// Gives the addresses of <paramref name="type" /> in the answer section of <paramref name="response" />, a
    /// message without its length prefix; any other record (a CNAME the server followed) is skipped.
    /// </summary>
    /// <param name="response">The DNS message.</param>
    /// <param name="id">The ID of the query it must answer.</param>
    /// <param name="type">The record type asked for.</param>
    /// <returns>The addresses in answer order, empty for another ID, a query, an error code or a malformed message.</returns>
    public static IReadOnlyList<IPAddress> AddressesIn(ReadOnlySpan<byte> response, ushort id, ushort type)
    {
        List<IPAddress> addresses = [];
        if (!IsAnswerTo(response, id))
        {
            return addresses;
        }

        int offset = AfterQuestions(response);
        for (int answer = BinaryPrimitives.ReadUInt16BigEndian(response[6..]); answer > 0 && offset >= 0; answer--)
        {
            offset = ReadRecord(response, offset, type, addresses);
        }

        return addresses;
    }

    /// <summary>Gives the offset of the answer section, past every question's name, type and class.</summary>
    /// <returns>The offset, or -1 when a question runs past the message.</returns>
    private static int AfterQuestions(ReadOnlySpan<byte> response)
    {
        int offset = HeaderLength;
        for (int question = BinaryPrimitives.ReadUInt16BigEndian(response[4..]); question > 0 && offset >= 0; question--)
        {
            int fields = AfterName(response, offset);
            offset = fields < 0 ? -1 : fields + 4;
        }

        return offset;
    }

    private static bool IsAnswerTo(ReadOnlySpan<byte> response, ushort id)
    {
        if (response.Length < HeaderLength)
        {
            return false;
        }

        ushort flags = BinaryPrimitives.ReadUInt16BigEndian(response[2..]);
        return BinaryPrimitives.ReadUInt16BigEndian(response) == id && (flags & ResponseFlag) != 0 && (flags & ResponseCodeMask) == 0;
    }

    /// <summary>Gives the offset just past the name at <paramref name="offset" />, whose last label may be a compression pointer.</summary>
    /// <returns>The offset, or -1 when the name runs past the message.</returns>
    private static int AfterName(ReadOnlySpan<byte> message, int offset)
    {
        while (offset < message.Length)
        {
            int length = message[offset];
            if (length == 0 || (length & CompressionPointer) == CompressionPointer)
            {
                return offset + (length == 0 ? 1 : 2);
            }

            offset += length + 1;
        }

        return -1;
    }

    /// <summary>Reads the resource record at <paramref name="offset" />, keeping its address when it is one of <paramref name="type" />.</summary>
    /// <returns>The offset of the next record, or -1 when the record runs past the message.</returns>
    private static int ReadRecord(ReadOnlySpan<byte> message, int offset, ushort type, List<IPAddress> addresses)
    {
        int fields = AfterName(message, offset);
        int data = fields + 10;
        if (fields < 0 || data > message.Length)
        {
            return -1;
        }

        int dataLength = BinaryPrimitives.ReadUInt16BigEndian(message[(fields + 8)..]);
        int end = data + dataLength;
        if (end > message.Length)
        {
            return -1;
        }

        if (IsAddressOf(message[fields..], type, dataLength))
        {
            addresses.Add(new IPAddress(message[data..end]));
        }

        return end;
    }

    /// <summary>Gives whether the record whose type field starts <paramref name="fields" /> is an Internet address of <paramref name="type" />.</summary>
    private static bool IsAddressOf(ReadOnlySpan<byte> fields, ushort type, int dataLength) =>
        BinaryPrimitives.ReadUInt16BigEndian(fields) == type
        && BinaryPrimitives.ReadUInt16BigEndian(fields[2..]) == InternetClass
        && dataLength == (type == AddressType ? 4 : 16);
}
