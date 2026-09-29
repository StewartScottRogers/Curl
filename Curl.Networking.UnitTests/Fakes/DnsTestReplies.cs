using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Curl.Networking.Fakes;

/// <summary>
/// Builds DNS replies to a query as the recorder's loopback responder builds them
/// (<c>Record-CurlExchange.ps1 -DnsPort</c>, BL-694): the query's ID and question, flags
/// <c>0x8180</c> plus the RCODE, and one record per address of the family asked for, TTL 60.
/// </summary>
public static class DnsTestReplies
{
    /// <summary>Answers <paramref name="query" /> with the addresses of the asked family.</summary>
    /// <param name="query">The query.</param>
    /// <param name="addresses">The addresses to answer with; those of the other family are left out.</param>
    /// <param name="responseCode">The RCODE; any value but 0 sends no records.</param>
    /// <param name="truncated">Whether to set TC and send no records.</param>
    /// <returns>The reply.</returns>
    public static byte[] Answer(byte[] query, IPAddress[] addresses, int responseCode = 0, bool truncated = false)
    {
        var questionEnd = QuestionEnd(query);
        var type = BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(questionEnd - 4));
        var family = type == (ushort)DnsRecordType.Aaaa ? AddressFamily.InterNetworkV6 : AddressFamily.InterNetwork;
        var records = responseCode == 0 && !truncated
            ? addresses.Where(address => address.AddressFamily == family).Select(address => Record(type, address.GetAddressBytes())).ToArray()
            : [];
        return Reply(query, questionEnd, responseCode, truncated, records);
    }

    /// <summary>Answers an SRV <paramref name="query" /> with one record per target.</summary>
    /// <param name="query">The query.</param>
    /// <param name="records">Each record's priority, weight, port and dotted target.</param>
    /// <returns>The reply.</returns>
    public static byte[] AnswerServices(byte[] query, params (ushort Priority, ushort Weight, ushort Port, string Target)[] records)
    {
        var questionEnd = QuestionEnd(query);
        return Reply(query, questionEnd, 0, false, records.Select(ServiceRecord).ToArray());
    }

    /// <summary>The query's record type.</summary>
    /// <param name="query">The query.</param>
    /// <returns>The QTYPE.</returns>
    public static DnsRecordType TypeOf(byte[] query) =>
        (DnsRecordType)BinaryPrimitives.ReadUInt16BigEndian(query.AsSpan(QuestionEnd(query) - 4));

    private static byte[] Reply(byte[] query, int questionEnd, int responseCode, bool truncated, byte[][] records)
    {
        var reply = new List<byte>(query.AsSpan(0, questionEnd).ToArray());
        reply[2] = (byte)(truncated ? 0x83 : 0x81);
        reply[3] = (byte)(0x80 | responseCode);
        for (var index = 6; index < 12; index++)
        {
            reply[index] = 0;
        }

        reply[7] = (byte)records.Length;
        foreach (var record in records)
        {
            reply.AddRange(record);
        }

        return [.. reply];
    }

    private static byte[] Record(ushort type, byte[] data) =>
        [0xC0, 0x0C, (byte)(type >> 8), (byte)type, 0, 1, 0, 0, 0, 60, 0, (byte)data.Length, .. data];

    private static byte[] ServiceRecord((ushort Priority, ushort Weight, ushort Port, string Target) record)
    {
        var target = new List<byte>();
        foreach (var label in record.Target.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            target.Add((byte)label.Length);
            target.AddRange(Encoding.ASCII.GetBytes(label));
        }

        target.Add(0);
        var data = new byte[6 + target.Count];
        BinaryPrimitives.WriteUInt16BigEndian(data, record.Priority);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(2), record.Weight);
        BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(4), record.Port);
        target.CopyTo(data, 6);
        return Record((ushort)DnsRecordType.Srv, data);
    }

    private static int QuestionEnd(byte[] query)
    {
        var index = 12;
        while (query[index] != 0)
        {
            index += 1 + query[index];
        }

        return index + 1 + 4;
    }
}
